using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Packet;
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
        IPacketHandler GetHandler(PacketType type)
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
                case PacketType.Boost:
                    SendMessages(Connected, data, size, SendType.Unreliable);
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
        
        // Read PacketType
        PacketDataType dataType = (PacketDataType)Marshal.ReadInt16(data);

        if (Marshal.ReadInt16(data) == 0)
        {
            var packet = Marshal.PtrToStructure<IntNetPacket>(data);
            GetHandler(packet.PacketType)?.ProcessPacket(packet.SenderId, packet.Data);
        }
        else
        {
            var packet = Marshal.PtrToStructure<VectorNetPacket>(data);
            GetHandler(packet.PacketType)?.ProcessPacket(packet.SenderId, packet.Data);
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
        var packet = new IntNetPacket() { SenderId = GlobalsManager.LocalPlayer, PacketType = PacketType.Checkpoint, Data = index };
        SendMessages(packet);
        if (IPacketHandler.PacketHandlers.TryGetValue(PacketType.Checkpoint, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.Data);
        }
    }

    public void SendRewindToCheckpoint()
    {
        var packet = new IntNetPacket()
            { SenderId = GlobalsManager.LocalPlayer, PacketType = PacketType.Rewind, Data = _latestCheckpoint };
        SendMessages(packet);
        if (IPacketHandler.PacketHandlers.TryGetValue(PacketType.Rewind, out var handler))
        {
            handler.ProcessPacket(packet.SenderId, packet.Data);
        }
        
    }
    
    //this function sucks
    private void SendPlayerId(Connection connection, SteamId steamId, int id)
    {
        int length = sizeof(short) + sizeof(short) + sizeof(ulong) + sizeof(float) + sizeof(float);

        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            IntPtr unmanagedPointer = Marshal.AllocHGlobal(length);
            var packet = new VectorNetPacket()
            {
                PacketType = PacketType.PlayerId,
                SenderId = vgPlayerData.Key,
                Data = new Vector2(vgPlayerData.Value.PlayerID, GlobalsManager.Players.Count)
            };

            Marshal.StructureToPtr(packet, unmanagedPointer, false);
            connection.SendMessage(unmanagedPointer, length);
            
            Marshal.FreeHGlobal(unmanagedPointer);
        }

        var info = new VectorNetPacket()
        {
            PacketType = PacketType.PlayerId,
            SenderId = steamId,
            Data = new Vector2(id, 1)
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
            Data = GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health
        };
        SendHostPacket(packet);
    }
    public void SendHostPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
            Data = pos
        };
        SendHostPacket(packet, SendType.Unreliable);
    }

    public void SendHostBoost()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Boost,
            SenderId = GlobalsManager.LocalPlayer,
            Data = 0
        };
        SendHostPacket(packet, SendType.Unreliable);
    }
}

/// <summary>
/// Game Client
/// Responsible for sending/receiving messages
/// </summary>
public class PAMConnectionManager : ConnectionManager
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
        if(SceneLoader.Inst.manager.ActiveSceneGroup.GroupName == "Arcade_Level")
            SceneLoader.Inst.LoadSceneGroup("Menu");
        
        SteamLobbyManager.Inst.CurrentLobby.Leave();
    }
    
    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        try
        {
            if (Marshal.ReadInt16(data) == 0)
            {
                var packet = Marshal.PtrToStructure<IntNetPacket>(data);
                if(IPacketHandler.PacketHandlers.TryGetValue(packet.PacketType, out var handler))
                {
                    handler.ProcessPacket(packet.SenderId, packet.Data);
                }
            }
            else
            {
                var packet = Marshal.PtrToStructure<VectorNetPacket>(data);
                if(IPacketHandler.PacketHandlers.TryGetValue(packet.PacketType, out var handler))
                {
                    handler.ProcessPacket(packet.SenderId, packet.Data);
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
            Data = GlobalsManager.Players[GlobalsManager.LocalPlayer].PlayerObject.Health
        };
        SendPacket(packet);
    }

    public void SendPosition(Vector2 pos)
    {
        var packet = new VectorNetPacket()
        {
            PacketType = PacketType.Position,
            SenderId = GlobalsManager.LocalPlayer,
            Data = pos
        };
        SendPacket(packet, SendType.Unreliable);
    }
    public void SendBoost()
    {
        var packet = new IntNetPacket()
        {
            PacketType = PacketType.Boost,
            SenderId = GlobalsManager.LocalPlayer,
            Data = 0
        };
        SendPacket(packet, SendType.Unreliable);
    }
}