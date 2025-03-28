using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Packet;
using PAMultiplayer.Patch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using MemoryStream = System.IO.MemoryStream;

namespace PAMultiplayer.Managers;

//this whole file sucks
//Ive got to do some major refactoring here


/// <summary>
/// Game Server
/// Responsible for sending/receiving messages
/// </summary>
public class PAMSocketManager : SocketManager
{
    public struct ConnectionWrapper(Connection connection, ulong connectionId)
    {
        public Connection Connection = connection;
        public ulong ConnectionId = connectionId;
    }
    
    private const int MAX_PACKET_SIZE = 52;
    
    private readonly MemoryStream _stream;
    private readonly MemoryStream _outStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    
    public readonly Dictionary<ConnectionWrapper, int> ConnectionWrappers = new();
    private int _playerCounter;

    public PAMSocketManager()
    {
        _stream = new MemoryStream(MAX_PACKET_SIZE);
        _outStream = new MemoryStream(MAX_PACKET_SIZE);
        _reader = new BinaryReader(_stream);
        _writer = new BinaryWriter(_outStream);
        
        _stream.SetLength(MAX_PACKET_SIZE);
        _outStream.SetLength(MAX_PACKET_SIZE);
    }

    ~PAMSocketManager()
    {
        _reader.Dispose();
        _writer.Dispose();
        _stream.Dispose();
        _outStream.Dispose();
    }
    
    #region SocketManagerOverrides
    
    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        connection.Accept();
        PAM.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} is connecting");
    }

    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
        ConnectionWrappers.TryAdd(new ConnectionWrapper(connection, data.Identity.SteamId), _playerCounter++);
        SendPlayerId(connection, data.Identity.SteamId, GlobalsManager.Players[data.Identity.SteamId].VGPlayerData.PlayerID);
        
        PAM.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} has joined the game");
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        base.OnDisconnected(connection, data);
        foreach (var keyValuePair in ConnectionWrappers)
        {
            if (keyValuePair.Key.Connection == connection)
            {
                ConnectionWrappers.Remove(keyValuePair.Key);
                break;
            }
        }
        PAM.Inst.Log.LogInfo($"Server: {data.Identity} is out of here");
    }

    public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size,
        long messageNum,
        long recvTime, int channel)
    {
        // Checks the size, if smaller than min logs into the terminal and returns false.
        bool CheckSize(int _size, int minSize)
        {
            if (_size < minSize)
            {
                PAM.Logger.LogWarning("Packet size too small");
                return false;
            }

            return true;
        }

        byte[] WriteSteamId(PacketType packetType, out int _size)
        {
            _writer.Write((ushort)packetType);
            _writer.Write(identity.SteamId);
            _writer.Write(_stream.GetBuffer(), 2, size - 2);

            _size = size + 8;
            return _outStream.GetBuffer();
        }

        if (size > MAX_PACKET_SIZE)
        {
            PAM.Logger.LogWarning(
                $"Received more bytes than expected, [{size}] bytes from [{identity.SteamId}], closing connection");
            connection.Close();
            return;
        }

        if (size < 2)
        {
            PAM.Logger.LogWarning($"Received too little bytes, [{size}] bytes from [{identity.SteamId}]");
            return;
        }

        Marshal.Copy(data, _stream.GetBuffer(), 0, size);
        _stream.Position = 0;
        _outStream.Position = 0;
   
  
        PacketType packetType = (PacketType)_reader.ReadUInt16();
       
        switch (packetType)
        {
            case PacketType.Damage:
                if (!CheckSize(size, 6))
                {
                    connection.Close();
                    return;
                }

                int health = _reader.ReadInt32();
                if (DataManager.inst.GetSettingBool("mp_linkedHealth", false))
                {
                    if (GlobalsManager.LocalPlayerObj.Health >= health)
                    {
                        SendDamageAll(health, identity.SteamId);
                    }

                    return;
                }

            {
                if (GlobalsManager.Players.TryGetValue(identity.SteamId, out var player))
                {
                    if (!player.VGPlayerData.PlayerObject.IsValidPlayer()) return;
                    player.VGPlayerData.PlayerObject.Health = health;
                    player.VGPlayerData.PlayerObject.PlayerHit();
                }
                
                SendMessage(Connected, WriteSteamId(packetType, out int _size), _size);
            }
                break;
            case PacketType.Position:
                if (!CheckSize(size, 10))
                {
                    connection.Close();
                    return;
                }
            
                if (GlobalsManager.Players.TryGetValue(identity.SteamId, out var playerData))
                {
                    if (playerData.VGPlayerData.PlayerObject)
                    {
                        VGPlayer player = GlobalsManager.Players[identity.SteamId].VGPlayerData.PlayerObject;

                        if (!player) return;

                        Transform rb = player.Player_Wrapper;
                        Vector2 pos = new Vector2(_reader.ReadSingle(), _reader.ReadSingle());
                        
                        var rot = pos - (Vector2)rb.position;
                        rb.position = pos;
                        if (rot.sqrMagnitude > 0.0001f)
                        {
                            rot.Normalize();
                            player.p_lastMoveX = rot.x;
                            player.p_lastMoveY = rot.y;
                        }
                    }
                }

            {
                SendMessage(Connected, WriteSteamId(packetType, out int _size), _size, SendType.Unreliable);
            }
                break;
            case PacketType.Boost:
            {
                if (GlobalsManager.Players.TryGetValue(identity.SteamId, out var player))
                {
                    if (player.VGPlayerData.PlayerObject && !player.VGPlayerData.PlayerObject.isDead)
                    {
                        player.VGPlayerData.PlayerObject.PlayParticles(VGPlayer.ParticleTypes.Boost);
                    }
                  
                }
                
                SendMessage(Connected, WriteSteamId(packetType, out int _size), _size, SendType.Unreliable);
            }
                break;
            case PacketType.CheckLevelId:
                if (!ChallengeManager.Inst)
                {
                    break;
                }
                if (!CheckSize(size, 2))
                {
                    connection.Close();
                    return;
                }
                
                int levelsCount = _reader.ReadUInt16();
                PAM.Logger.LogInfo($"Client [{identity.SteamId}] has asked for [{levelsCount}] levels");
                for (int i = 0; i < levelsCount; i++)
                {

                    ulong levelId = _reader.ReadUInt64();

                    if (!ChallengeManager.Inst.GetVGLevel(levelId, out var songData))
                    {
                        PAM.Logger.LogFatal("client asked for level id not in the picked challenge levels");
                        continue;
                    }

                    void SendSegment(ArraySegment<short> segment, ushort last)
                    {
                        Packet.Packet packet = new Packet.Packet(PacketType.ChallengeAudioData);
                        packet.Write(levelId);
                        packet.Write(last);
                        packet.Write(songData.Item2);
                        packet.Write(songData.Item3);
                        packet.Write(segment);
                        SendMessage(connection, packet);
                    }

                    const int separator = 131000;
                    int offset = 0;
                    while (true)
                    {
                        if (offset + separator < songData.Item1.Length)
                        {
                            ArraySegment<short> segment = new(songData.Item1, offset, separator);
                            SendSegment(segment, 0);
                            offset += separator + 1;
                        }
                        else
                        {
                            ArraySegment<short> segment = new ArraySegment<short>(songData.Item1, offset,
                                Mathf.FloorToInt(songData.Item1.Length - offset));
                            SendSegment(segment, 1);
                            break;
                        }
                    }
                }

                break;
            case PacketType.ChallengeVote:
            {
                if (!CheckSize(size, 8))
                {
                    connection.Close();
                    return;
                }
                ulong levelId = _reader.ReadUInt64();
                if (ChallengeManager.Inst)
                {
                    ChallengeManager.Inst.PlayerVote(identity.SteamId, levelId);
                }
            }
                break;
            default:
                return;
        }
    }

    #endregion

    #region Send Messages
    void SendMessage(HashSet<Connection> connections, byte[] data, int size, SendType sendType = SendType.Reliable)
    {
        foreach (var connection in connections)
        {
            connection.SendMessage(data, 0, size, sendType);
        }
    }

    void SendMessage(Packet.Packet packet, SendType sendType = SendType.Reliable)
    {
        SendMessage(Connected, packet.GetData(out var size), size, sendType);
    }
    void SendMessage(Connection connection, Packet.Packet packet, SendType sendType = SendType.Reliable)
    {
        connection.SendMessage(packet.GetData(out var size), 0,size, sendType);
    }
    
    #endregion
    public void SendStartLevel()
    {
        using var packet = new Packet.Packet(PacketType.Start);
        SendMessage(packet);
    }
    
    public void SendOpenChallenge()
    {
        using var packet = new Packet.Packet(PacketType.OpenChallenge);
        SendMessage(packet);
    }
    
    public void SendChallengeVoteWinner(ulong levelId)
    {
        using var packet = new Packet.Packet(PacketType.ChallengeVote);
        packet.Write(levelId);
        SendMessage(packet);
    }

    public void SendCheckForLevelId(List<VGLevel> levelIds)
    {
        if (levelIds.Count != 6)
        {
            PAM.Logger.LogFatal("Just tried to send a different amount of level ids for challenge mode, dropping");
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return;
        }
        using var packet = new Packet.Packet(PacketType.CheckLevelId);
        foreach (var levelId in levelIds)
        {
            packet.Write(levelId.SteamInfo.ItemID);
        }
        SendMessage(packet);
    }
    public void SendCheckpointHit(int index)
    {
        using var packet = new Packet.Packet(PacketType.Checkpoint);
        packet.Write(index);
        SendMessage(packet);
    }

    public void SendRewindToCheckpoint(int index)
    {
        using var packet = new Packet.Packet(PacketType.Rewind);
        packet.Write(index);
        SendMessage(packet);
    }
    
    //this function sucks
    private void SendPlayerId(Connection connection, SteamId steamId, int id)
    {
        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            using var packet = new Packet.Packet(PacketType.PlayerId);
            packet.Write(vgPlayerData.Key);
            packet.Write(vgPlayerData.Value.VGPlayerData.PlayerID);
            packet.Write(GlobalsManager.Players.Count);
            
            SendMessage(connection, packet);
        }

        using var info = new Packet.Packet(PacketType.PlayerId);
        info.Write(steamId);
        info.Write(id);
        info.Write(1);
        SendMessage(info);
    }

    public void SendNextQueueLevel(ulong id, int seed)
    {
        using var newPacket = new Packet.Packet(PacketType.NextLevel);
        newPacket.Write(id);
        newPacket.Write(seed);
        
        SendMessage(newPacket);
    }
    //yes due to a mistake the host doesn't connect to the server as client 
    //so we handle his messages from here

    public void SendHostDamage()
    {
        using var packet = new Packet.Packet(PacketType.Damage);
        packet.Write(GlobalsManager.LocalPlayerId);
        packet.Write(GlobalsManager.LocalPlayerObj.Health);
        
        SendMessage(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        using var packet = new Packet.Packet(PacketType.Position);
        packet.Write(GlobalsManager.LocalPlayerId);
        packet.Write(pos);
        
        SendMessage(packet, SendType.Unreliable);
    }

    public void SendHostBoost()
    {
        using var packet = new Packet.Packet(PacketType.Boost);
        packet.Write(GlobalsManager.LocalPlayerId);
        
        SendMessage(packet, SendType.Unreliable);
    }

    public void SendDamageAll(int healthPreHit, ulong hitPlayerId)
    {
        using var packet = new Packet.Packet(PacketType.DamageAll);
        packet.Write(hitPlayerId);
        packet.Write(healthPreHit);
        
        SendMessage(packet);
        
       
        if (DataManager.inst.GetSettingBool("MpLinkedHealthPopup", true))
        {
            if (GlobalsManager.Players.TryGetValue(hitPlayerId, out var playerData))
            {
                string hex = VGPlayerManager.Inst.GetPlayerColorHex(playerData.VGPlayerData.PlayerID);
                VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{playerData.Name}</color>] got hit!", 1f);
            }
        }
       
        
        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            VGPlayer player = vgPlayerData.Value.VGPlayerData.PlayerObject;
            if (player.IsValidPlayer())
            {
                if (player.Health < healthPreHit)
                {
                    PAM.Inst.Log.LogWarning($"Old message");
                    continue;
                }

                if (player.IsLocalPlayer())
                {
                    Player_Patch.IsDamageAll = true;
                }
                
                player.Health = healthPreHit;
                player.PlayerHit();
            }
        }
    }

    public bool TryToKickPlayer(int id)
    {
        foreach (var connection in ConnectionWrappers)
        {
            if (connection.Value == id)
            {
                return connection.Key.Connection.Close();
            }
        }

        return false;
    }
    
}

/// <summary>
/// Game Client
/// Responsible for sending/receiving messages
/// </summary>
public class PAMConnectionManager : ConnectionManager
{
    const int MAX_PACKET_SIZE = 524288;
    
    private readonly MemoryStream _stream;
    private readonly BinaryReader _reader;


    public PAMConnectionManager()
    {
        _stream = new MemoryStream(24);
        _reader = new BinaryReader(_stream);
        
        _stream.SetLength(24);
    }

    ~PAMConnectionManager()
    {
        _reader.Dispose();
        _stream.Dispose();
    }
    #region ConnectionManager Overrides
    
    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        PAM.Logger.LogInfo($"Client: Connecting with Steam user {info.Identity.SteamId}.");
    }

    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        PAM.Logger.LogInfo($"Client: Connected with Steam user {info.Identity.SteamId}.");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        PAM.Logger.LogInfo($"Client: Disconnected Steam user {info.Identity.SteamId}.");
        
        SteamManager.Inst.EndClient();
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Arcade");
    }

    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        
        if (size < 2)
        {
            PAM.Logger.LogWarning("Received too little data");
            return;
        }

        if (size > MAX_PACKET_SIZE)
        {
            PAM.Logger.LogWarning("Received too much data");
            return;
        }
        
        _stream.Position = 0;
        _stream.SetLength(size);
        Marshal.Copy(data, _stream.GetBuffer(), 0, size);
        
        PacketType packetType = (PacketType)_reader.ReadUInt16();
        
        if (PacketHandler.PacketHandlers.TryGetValue(packetType, out var handler))
        {
            handler.TryProcessPacket(_reader, size - 2);
        }
    }

    #endregion

    #region Send Messages
    
    void SendPacket(Packet.Packet packet, SendType sendType = SendType.Reliable)
    {
        Connection.SendMessage(packet.GetData(out var size), 0, size, sendType);
    } 
  
    #endregion
    public void SendDamage(int healthPreHit)
    {
        using var packet = new Packet.Packet(PacketType.Damage);
        packet.Write(healthPreHit);
     
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        using var packet = new Packet.Packet(PacketType.Position);
        packet.Write(pos);
        SendPacket(packet, SendType.Unreliable);
    }
    public void SendBoost()
    {
        using var packet = new Packet.Packet(PacketType.Boost);
        SendPacket(packet, SendType.Unreliable);
    }
    
    public void SendChallengeVote(ulong levelId)
    {
        using var packet = new Packet.Packet(PacketType.ChallengeVote);
        packet.Write(levelId);
        SendPacket(packet);
    }

    public void SendCheckLevelID(List<ulong> unknownIds)
    {
        using var packet = new Packet.Packet(PacketType.CheckLevelId);
        packet.Write((ushort)unknownIds.Count); //saving a whole 2 bytes!!!!!
        foreach (var unknownId in unknownIds)
        {
            packet.Write(unknownId);
        }
     
        SendPacket(packet);
    }
}
