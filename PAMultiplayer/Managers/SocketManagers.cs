using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PAMultiplayer.Packet;
using Rewired.Data;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

//ye im stealing the VG prefix used in some PA classes
//I assume it means Vitamin Games
public class VGSocketManager : SocketManager
{
    int _latestCheckpoint = 0;

    #region SocketManagerOverrides

    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        connection.Accept();
        Plugin.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} is connecting");
    }

    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
        SendPlayerId(connection, data.Identity.SteamId, StaticManager.Players[data.Identity.SteamId].PlayerID);
        Plugin.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} has joined the game");
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo data)
    {
        base.OnDisconnected(connection, data);
        Plugin.Inst.Log.LogInfo($"Server: {data.Identity} is out of here");
    }

    public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum,
        long recvTime, int channel)
    {
        // Read PacketType
        NetPacket packet = Il2CppSystem.Runtime.InteropServices.Marshal.PtrToStructure<NetPacket>(data); //wtf is this man

        switch (packet.PacketType)
        {
            case PacketType.Damage:
                PacketHandler.HandleClientPacket(packet);
                SendMessages(Connected, data, size);
                break;
            case PacketType.Start:
                break;
            case PacketType.Loaded:
                PacketHandler.HandleClientPacket(packet);
                SendMessages(Connected, data, size);
                break;
            case PacketType.Position:
                PacketHandler.HandleClientPacket(packet);
                SendMessages(Connected, data, size, SendType.Unreliable);
                break;
            case PacketType.Rotation:
                break;
            case PacketType.Spawn:
                break;
        }
    }

    #endregion

    #region Send Messages
    void SendMessages(HashSet<Connection> connections, IntPtr ptr, int size, SendType sendType = SendType.Reliable)
    {
        foreach (var connection in connections)
        {
            connection.SendMessage(ptr, size, sendType);
        }
    }

    void SendMessages(HashSet<Connection> connections, NetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);

        IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedPointer, false);

        foreach (var connection in connections)
        {
            connection.SendMessage(unmanagedPointer, length, sendType);
        }

        Marshal.FreeHGlobal(unmanagedPointer);
    }
    
    void SendHostPacket(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedData, false);
        
        SendMessages(Connected, unmanagedData, length, sendType);
        
        Marshal.FreeHGlobal(unmanagedData);
    }
    #endregion
    public void StartLevel()
    {
        NetPacket packet = new NetPacket() { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Start };
        SendMessages(Connected, packet);
    }

    public void SendCheckpointHit(int index)
    {
        _latestCheckpoint = index;
        NetPacket packet = new NetPacket() { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Checkpoint, Data = index };
        SendMessages(Connected, packet);
        PacketHandler.HandleClientPacket(packet);
    }

    public void SendRewindToCheckpoint()
    {
        NetPacket packet = new NetPacket()
            { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Rewind, Data = _latestCheckpoint };
        SendMessages(Connected, packet);
        PacketHandler.HandleClientPacket(packet);
    }
    
    //this function sucks
    private void SendPlayerId(Connection connection, SteamId steamId, int id)
    {
        List<PlayersPacket.PlayerInfo> infoList = new();
        foreach (var vgPlayerData in StaticManager.Players)
        {
            infoList.Add(new PlayersPacket.PlayerInfo(vgPlayerData.Key, vgPlayerData.Value.PlayerID));
        }

        var packet = new NetPacket()
            {
                PacketType = PacketType.Spawn,
                SenderId = StaticManager.LocalPlayer,
                Data = new PlayersPacket(infoList.ToArray())
            };
            int length = Marshal.SizeOf(packet);

            IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(packet, unmanagedPointer, false);
            connection.SendMessage(unmanagedPointer, length);
            Marshal.FreeHGlobal(unmanagedPointer);

            var info = new PlayersPacket.PlayerInfo[] { new(steamId, id)};
            SendMessages(Connected, new NetPacket(){PacketType = PacketType.Spawn, SenderId = StaticManager.LocalPlayer, Data = new PlayersPacket(info)});
    }
    public void SendHostLoaded()
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = StaticManager.LocalPlayer
        };
        SendHostPacket(packet);
    }

    public void SendHostDamage()
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = StaticManager.LocalPlayer,
            Data = VGPlayerManager.Inst.players[0].PlayerObject.Health
        };
        SendHostPacket(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = StaticManager.LocalPlayer,
            Data = pos
        };
        SendHostPacket(packet, SendType.Unreliable);
    }
}

public class VGConnectionManager : ConnectionManager
{
    private Dictionary<string, PacketHandler> PacketHandlers = new();
    #region ConnectionManager Overrides
    
    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Plugin.Logger.LogInfo($"Client: Connecting with Steam user {info.Identity.SteamId}.");
    }

    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        Plugin.Logger.LogInfo($"Client: Connected with Steam user {info.Identity.SteamId}.");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        Plugin.Logger.LogInfo($"Client: Disconnected Steam user {info.Identity.SteamId}.");
    }
    
    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        try
        {
            NetPacket packet = Il2CppSystem.Runtime.InteropServices.Marshal.PtrToStructure<NetPacket>(data);
            PacketHandler.HandleClientPacket(packet);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
            throw;
        }
    }
    #endregion

    void SendPacket(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedData, false);
        Connection.SendMessage(unmanagedData, length, sendType);
        Marshal.FreeHGlobal(unmanagedData);
    }
    
    public unsafe void SendPacket<T>(string eventName, T data, SendType sendType = SendType.Reliable) where T : unmanaged
    {
        var length = Unsafe.SizeOf<T>();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(sizeof(int) + sizeof(ulong) + eventName.Length + sizeof(int) + length); // write total packet size
        writer.Write(eventName.Length); // write event name length
        writer.Write(Encoding.UTF8.GetBytes(eventName)); // write event name content
        writer.Write(StaticManager.LocalPlayer);
        writer.Write(length); // write struct length
        var dataArray = stackalloc byte[length];
        Marshal.StructureToPtr(data, (IntPtr)dataArray, false);
        writer.Write(new ReadOnlySpan<byte>(dataArray, length));
        Connection.SendMessage(stream.ToArray(), sendType);
    }

    public void HandleEvent(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream(data.ToArray());
        using var reader = new BinaryReader(stream);
        var packetSize = reader.ReadInt32();
        if (packetSize - 4 != data.Length)
            throw new ArgumentException(nameof(data), "Packet size does not match received data buffer size");
        var eventNameLength = reader.ReadInt32();
        var eventNameBytes = reader.ReadBytes(eventNameLength);
        var eventName = Encoding.UTF8.GetString(eventNameBytes);
        var steamid = reader.ReadUInt64();
        var dataSize = reader.ReadInt32();
        var dataBytes = reader.ReadBytes(dataSize);
        if (!PacketHandlers.TryGetValue(eventName, out var handler))
        {
            Plugin.Logger.LogWarning($"No event handler registered for event type '{eventName}'");
            return;
        }
        handler.ProcessPacket(steamid, dataBytes);
    }
    public void SendLoaded()
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = StaticManager.LocalPlayer
        };
        SendPacket(packet);
    }

    public void SendDamage()
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = StaticManager.LocalPlayer,
            Buffer = VGPlayerManager.Inst.players[0].PlayerObject.Health
        };
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = StaticManager.LocalPlayer,
        };
        SendPacket("Pos", pos, SendType.Unreliable);
    }
}