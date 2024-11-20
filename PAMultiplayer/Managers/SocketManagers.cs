using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Packet;
using PAMultiplayer.Patch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

//this whole file sucks
//Ive got to do some major refactoring here



/// <summary>
/// Game Server
/// Responsible for sending/receiving messages
/// </summary>
public class PAMSocketManager : SocketManager
{
    const int PACKET_SIZE = 24;
    
    #region SocketManagerOverrides
    
    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        connection.Accept();
        PAM.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} is connecting");
    }

    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
        SendPlayerId(connection, data.Identity.SteamId, GlobalsManager.Players[data.Identity.SteamId].PlayerID);
        PAM.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} has joined the game");
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        base.OnDisconnected(connection, data);
        PAM.Inst.Log.LogInfo($"Server: {data.Identity} is out of here");
    }

    public override unsafe void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum,
        long recvTime, int channel)
    {
        IPacketHandler GetHandler(PacketType type)
        {
            switch (type)
            {
                case PacketType.Damage:
                    SendMessageToAll(Connected, data, size);
                    break;
                case PacketType.Position:
                    SendMessageToAll(Connected, data, size, SendType.Unreliable);
                    break;
                case PacketType.Boost:
                    SendMessageToAll(Connected, data, size, SendType.Unreliable);
                    break;
                default:
                    return null;
            }

            if (IPacketHandler.PacketHandlers.TryGetValue(type, out var handler))
            {
                return handler;
            }

            return null;
        }
        
        Span<byte> packetSpan = new Span<byte>((void*)data, PACKET_SIZE);
        var packet = MemoryMarshal.Read<NetPacket>(packetSpan);
        
        GetHandler(packet.PacketType)?.ProcessPacket(packet.SenderId, packet.Data);
    }

    #endregion

    #region Send Messages
    void SendMessageToAll(HashSet<Connection> connections, IntPtr ptr, int size, SendType sendType = SendType.Reliable)
    {
        foreach (var connection in connections)
        {
            connection.SendMessage(ptr, size, sendType);
        }
    }

    unsafe void SendMessage(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        Span<byte> packetSpan = stackalloc byte[PACKET_SIZE];
        MemoryMarshal.Write(packetSpan, ref packet);
       
        fixed (byte* ptr = packetSpan)
        {
            SendMessageToAll(Connected, (IntPtr)ptr, PACKET_SIZE, sendType);
        }
    }
    unsafe void SendMessage(Connection connection, NetPacket packet, SendType sendType = SendType.Reliable)
    {
        Span<byte> packetSpan = stackalloc byte[PACKET_SIZE];
        MemoryMarshal.Write(packetSpan, ref packet);

        fixed (byte* ptr = packetSpan)
        {
            connection.SendMessage((IntPtr)ptr, PACKET_SIZE, sendType);
        }
    }
    
    #endregion
    public void StartLevel()
    {
        GlobalsManager.HasStarted = true;
        var packet = new NetPacket
        {
            PacketType = PacketType.Start,
            SenderId = GlobalsManager.LocalPlayer,
        };
    
        SendMessage(packet);
    }

    public void SendCheckpointHit(int index)
    {
        var packet = new NetPacket(index)
        {
            PacketType = PacketType.Checkpoint,
            SenderId = GlobalsManager.LocalPlayer,
        };
        
        SendMessage(packet);
        if (IPacketHandler.PacketHandlers.TryGetValue(PacketType.Checkpoint, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.Data);
        }
    }

    public void SendRewindToCheckpoint()
    {
        int index = 0;
        
        if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) <= 1)
        {
            var checkpoint = GameManager.Inst.GetClosestIndex(DataManager.inst.gameData.beatmapData.checkpoints,
                GameManager.Inst.CurrentSongTimeSmoothed);
        
            index = DataManager.inst.gameData.beatmapData.checkpoints.FindIndex(
                new Predicate<DataManager.GameData.BeatmapData.Checkpoint>(x => x == checkpoint).ToIL2CPP());
        }
        
        var packet = new NetPacket(index)
        {
            PacketType = PacketType.Rewind,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendMessage(packet);
        
        if (IPacketHandler.PacketHandlers.TryGetValue(PacketType.Rewind, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.Data);
        }
    }
    
    //this function sucks
    private void SendPlayerId(Connection connection, SteamId steamId, int id)
    {
        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            var packet = new NetPacket(new Vector2(vgPlayerData.Value.PlayerID, GlobalsManager.Players.Count))
            {
                PacketType = PacketType.PlayerId,
                SenderId = vgPlayerData.Key,
            };
            
            SendMessage(connection, packet);
        }

        var info = new NetPacket(new Vector2(id, 1))
        {
            PacketType = PacketType.PlayerId,
            SenderId = steamId,
        };
        
        SendMessage(info);
    }

    public void SendNextQueueLevel(ulong id, int seed)
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.nextLevel,
            SenderId = id,
        };
        var seedBytes = BitConverter.GetBytes(seed);
        packet.Data.x = BitConverter.IsLittleEndian ? BitConverter.ToSingle(seedBytes) : BitConverter.ToSingle(seedBytes.Reverse().ToArray());
        SendMessage(packet);
    }
    //yes due to a mistake the host doesn't connect to the server as client 
    //so we handle his messages from here

    public void SendHostDamage()
    {
        var packet = new NetPacket(GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health)
        {
            PacketType = PacketType.Damage,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendMessage(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        var packet = new NetPacket(pos)
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendMessage(packet, SendType.Unreliable);
    }

    public void SendHostBoost()
    {
        var packet = new NetPacket
        {
            PacketType = PacketType.Boost,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendMessage(packet, SendType.Unreliable);
    }
}

/// <summary>
/// Game Client
/// Responsible for sending/receiving messages
/// </summary>
public class PAMConnectionManager : ConnectionManager
{
    const int PACKET_SIZE = 24;
    
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
        
        SteamLobbyManager.Inst.CurrentLobby.Leave();
        if (SceneLoader.Inst.manager.ActiveSceneGroup.GroupName == "Arcade_Level")
        {
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.Inst.LoadSceneGroup("Arcade");
        }
    }

    public override unsafe void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        Span<byte> packetSpan = new Span<byte>((void*)data, PACKET_SIZE);
        var packet = MemoryMarshal.Read<NetPacket>(packetSpan);
        
        if (IPacketHandler.PacketHandlers.TryGetValue(packet.PacketType, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.Data);
        }
    }

    #endregion

    #region Send Messages
    
    unsafe void SendPacket(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        Span<byte> packetSpan = stackalloc byte[PACKET_SIZE];
        MemoryMarshal.Write(packetSpan, ref packet);

        fixed (byte* ptr = packetSpan)
        {
            Connection.SendMessage((IntPtr)ptr, PACKET_SIZE, sendType);
        }
    } 
  
    #endregion
    public void SendDamage()
    {
        var packet = new NetPacket(GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health)
        {
            PacketType = PacketType.Damage,
            SenderId = GlobalsManager.LocalPlayer,
        };
     
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        var packet = new NetPacket(pos)
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendPacket(packet, SendType.Unreliable);
    }
    public void SendBoost()
    {
        var packet = new NetPacket
        {
            PacketType = PacketType.Boost,
            SenderId = GlobalsManager.LocalPlayer,
        };
        SendPacket(packet, SendType.Unreliable);
    }
}