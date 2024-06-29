using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PAMultiplayer.Packet;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

//ye im stealing the VG prefix used in some PA classes
//I assume it means Vitamin Games
public class VGSocketManager : SocketManager
{
    int _latestCheckpoint = 0;

    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        var result = connection.Accept();
        Plugin.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} is connecting || Result [{result}]");
    }

    public override void OnConnected(Connection connection, ConnectionInfo data)
    {
        base.OnConnected(connection, data);
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
        GetDataFromPacket(data, size);
    }

    
    void GetDataFromPacket(IntPtr data, int size)
    {
        // Read PacketType
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        NetPacket packet = Marshal.PtrToStructure<NetPacket>(dataHandle.AddrOfPinnedObject());

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
        dataHandle.Free();
    }

    void SendMessages(List<Connection> connections, IntPtr ptr, int size, SendType sendType = SendType.Reliable)
    {
        foreach (var connection in connections)
        {
            connection.SendMessage(ptr, size, sendType);
        }
    }

    void SendMessages(List<Connection> connections, NetPacket packet, SendType sendType = SendType.Reliable)
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
        NetPacket packet = new NetPacket() { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Rewind, Data = _latestCheckpoint };
        SendMessages(Connected, packet);
        PacketHandler.HandleClientPacket(packet);
    }
    
    void SendHostPacket(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        
        Marshal.StructureToPtr(packet, unmanagedData, false);
        
        SendMessages(Connected, unmanagedData, length, sendType);
        
        Marshal.FreeHGlobal(unmanagedData);
        
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

    void SendPacket(NetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        
        Marshal.StructureToPtr(packet, unmanagedData, false);
        
        Connection.SendMessage(unmanagedData, length, sendType);
        
        Marshal.FreeHGlobal(unmanagedData);
        
    }
    
    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        NetPacket packet = Marshal.PtrToStructure<NetPacket>(dataHandle.AddrOfPinnedObject());
        PacketHandler.HandleClientPacket(packet);
        dataHandle.Free();
        
        //this is dumb man, game crashes otherwise
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
            Data = VGPlayerManager.Inst.players[0].PlayerObject.Health
        };
        SendPacket(packet);
    }
    public void SendPosition(Vector2 pos)
    {
        var packet = new NetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = StaticManager.LocalPlayer,
            Data = pos
        };
        SendPacket(packet, SendType.Unreliable);
    }
}