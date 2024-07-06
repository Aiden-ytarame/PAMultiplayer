using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppSystems.SceneManagement;
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
        PacketHandler GetHandler(PacketType type)
        {
            switch (type)
            {
                case PacketType.Damage:
                    SendMessages(Connected, data, size);
                    break;
                case PacketType.Loaded:
                    SendMessages(Connected, data, size);
                    break;
                case PacketType.Position:
                    SendMessages(Connected, data, size, SendType.Unreliable);
                    break;
                default:
                    return null;
            }

            if (PacketHandler.PacketHandlers.TryGetValue(type, out var handler))
            {
                return handler;
            }

            return null;
        }
        
        // Read PacketType
        PacketDataType dataType = (PacketDataType)Marshal.ReadInt16(data);

        if (Marshal.ReadInt16(data) == 0)
        {
            var packet = Marshal.PtrToStructure<IntNetPacket>(data);
            if(packet.PacketType == PacketType.Loaded)
                Plugin.Logger.LogError("Loaded Recieved");
            GetHandler(packet.PacketType)?.ProcessPacket(packet.SenderId, packet.data);
        }
        else
        {
            var packet = Marshal.PtrToStructure<VectorNetPacket>(data);
            GetHandler(packet.PacketType)?.ProcessPacket(packet.SenderId, packet.data);
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

    void SendMessages(IntNetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedPointer, false);
        SendMessages(Connected, unmanagedPointer, length, sendType);
        Marshal.FreeHGlobal(unmanagedPointer);
    }
    void SendMessages(VectorNetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedPointer, false);
        SendMessages(Connected, unmanagedPointer, length, sendType);
        Marshal.FreeHGlobal(unmanagedPointer);
    }

    
    void SendHostPacket(IntNetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedData, false);
        
        SendMessages(Connected, unmanagedData, length, sendType);
        
        Marshal.FreeHGlobal(unmanagedData);
    }
    
    void SendHostPacket(VectorNetPacket packet, SendType sendType = SendType.Reliable)
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
        var packet = new IntNetPacket() { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Start };
        SendMessages(packet);
    }

    public void SendCheckpointHit(int index)
    {
        _latestCheckpoint = index;
        var packet = new IntNetPacket() { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Checkpoint, data = index };
        SendMessages(packet);
        if (PacketHandler.PacketHandlers.TryGetValue(PacketType.Checkpoint, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.data);
        }
    }

    public void SendRewindToCheckpoint()
    {
        var packet = new IntNetPacket()
            { SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Rewind, data = _latestCheckpoint };
        SendMessages(packet);
        if (PacketHandler.PacketHandlers.TryGetValue(PacketType.Rewind, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.data);
        }
        
    }
    
    //this function sucks
    private void SendPlayerId(Connection connection, SteamId steamId, int id)
    {
        bool hasValue = false;
        int length = sizeof(short) + sizeof(short) + sizeof(ulong) + sizeof(float);
        IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
        try
        {
            foreach (var vgPlayerData in StaticManager.Players)
            {
                var packet = new VectorNetPacket()
                {
                    PacketType = PacketType.Spawn,
                    SenderId = vgPlayerData.Key,
                    data = new Vector2(vgPlayerData.Value.PlayerID, StaticManager.Players.Count)
                };
                // int length = Marshal.SizeOf(packet);
                //IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
            
                Marshal.StructureToPtr(packet, unmanagedPointer, hasValue);
                if (!hasValue)
                    hasValue = true;
                connection.SendMessage(unmanagedPointer, length);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        Marshal.FreeHGlobal(unmanagedPointer);
        
            var info = new VectorNetPacket()
            {
                PacketType = PacketType.Spawn,
                SenderId = steamId,
                data = new Vector2(id, 1)
            };
            SendMessages(info);
    }
    public void SendHostLoaded()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = StaticManager.LocalPlayer
        };
        SendHostPacket(packet);
    }

    public void SendHostDamage()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = StaticManager.LocalPlayer,
            data = VGPlayerManager.Inst.players[0].PlayerObject.Health
        };
        SendHostPacket(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = StaticManager.LocalPlayer,
            data = pos
        };
        SendHostPacket(packet, SendType.Unreliable);
    }
}

public class VGConnectionManager : ConnectionManager
{
    
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
        SceneLoader.Inst.LoadSceneGroup("Menu");
    }
    
    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        try
        {
            if (Marshal.ReadInt16(data) == 0)
            {
                var packet = Marshal.PtrToStructure<IntNetPacket>(data);
                if(PacketHandler.PacketHandlers.TryGetValue(packet.PacketType, out var handler))
                {
                    handler.ProcessPacket(packet.SenderId, packet.data);
                }
            }
            else
            {
                var packet = Marshal.PtrToStructure<VectorNetPacket>(data);
                if(PacketHandler.PacketHandlers.TryGetValue(packet.PacketType, out var handler))
                {
                    handler.ProcessPacket(packet.SenderId, packet.data);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError(e);
            throw;
        }
    }
    #endregion

    void SendPacket(IntNetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedData, false);
        Connection.SendMessage(unmanagedData, length, sendType);
        Marshal.FreeHGlobal(unmanagedData);
    } 
    void SendPacket(VectorNetPacket packet, SendType sendType = SendType.Reliable)
    {
        int length = Marshal.SizeOf(packet);
        IntPtr unmanagedData = Marshal.AllocHGlobal(length);
        Marshal.StructureToPtr(packet, unmanagedData, false);
        Connection.SendMessage(unmanagedData, length, sendType);
        Marshal.FreeHGlobal(unmanagedData);
    }
    public void SendLoaded()
    {
        Plugin.Logger.LogError("Sending Loaded");
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = StaticManager.LocalPlayer
        };
        SendPacket(packet);
    }

    public void SendDamage()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = StaticManager.LocalPlayer,
            data = VGPlayerManager.Inst.players[0].PlayerObject.Health
        };
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = StaticManager.LocalPlayer,
            data = pos
        };
        SendPacket(packet, SendType.Unreliable);
    }
}