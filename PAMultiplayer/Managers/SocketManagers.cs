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


/// <summary>
/// Game Server
/// Responsible for sending/receiving messages
/// </summary>
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
        SendPlayerId(connection, data.Identity.SteamId, GlobalsManager.Players[data.Identity.SteamId].PlayerID);
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
        var packet = new IntNetPacket() { SenderId = GlobalsManager.LocalPlayer, PacketType = PacketType.Start };
        SendMessages(packet);
    }

    public void SendCheckpointHit(int index)
    {
        _latestCheckpoint = index;
        var packet = new IntNetPacket() { SenderId = GlobalsManager.LocalPlayer, PacketType = PacketType.Checkpoint, data = index };
        SendMessages(packet);
        if (PacketHandler.PacketHandlers.TryGetValue(PacketType.Checkpoint, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.data);
        }
    }

    public void SendRewindToCheckpoint()
    {
        var packet = new IntNetPacket()
            { SenderId = GlobalsManager.LocalPlayer, PacketType = PacketType.Rewind, data = _latestCheckpoint };
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
        int length = sizeof(short) + sizeof(short) + sizeof(ulong) + sizeof(float) + sizeof(float);

        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
            var packet = new VectorNetPacket()
            {
                PacketType = PacketType.Spawn,
                SenderId = vgPlayerData.Key,
                data = new Vector2(vgPlayerData.Value.PlayerID, GlobalsManager.Players.Count)
            };

            Marshal.StructureToPtr(packet, unmanagedPointer, false);
            //   if (!hasValue)
            //      hasValue = true;
            connection.SendMessage(unmanagedPointer, length);
            Marshal.FreeHGlobal(unmanagedPointer);
        }


        var info = new VectorNetPacket()
        {
            PacketType = PacketType.Spawn,
            SenderId = steamId,
            data = new Vector2(id, 1)
        };
        SendMessages(info);
    }

    //yes due to a mistake the host doesn't connect to the server as client 
    //so we handle his messages from here
    public void SendHostLoaded()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = GlobalsManager.LocalPlayer
        };
        SendHostPacket(packet);
    }

    public void SendHostDamage()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = GlobalsManager.LocalPlayer,
            data = GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health
        };
        SendHostPacket(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
            data = pos
        };
        SendHostPacket(packet, SendType.Unreliable);
    }
}

/// <summary>
/// Game Client
/// Responsible for sending/receiving messages
/// </summary>
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

    #region Send Messages
    
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
    #endregion
    public void SendLoaded()
    {
        Plugin.Logger.LogError("Sending Loaded");
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Loaded,
            SenderId = GlobalsManager.LocalPlayer
        };
        SendPacket(packet);
    }

    public void SendDamage()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Damage,
            SenderId = GlobalsManager.LocalPlayer,
            data = GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health
        };
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
            data = pos
        };
        SendPacket(packet, SendType.Unreliable);
    }
}