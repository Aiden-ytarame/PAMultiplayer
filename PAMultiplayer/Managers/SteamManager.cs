using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Utils;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using PAMultiplayer.Packet;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Inst { get; private set; }
    public VGSocketManager Server;
    public VGConnectionManager Client;
    private void Awake()
    {
        if (Inst)
        {
            Destroy(this);
            return;
        }
        DontDestroyOnLoad(this);
        Inst = this;
        
        SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void Update()
    {
        if(Server != null)
            Server.Receive();
        if(Client != null)
            Client.Receive();
    }

    private void OnApplicationQuit()
    {
        EndServer();
        EndClient();
    }
    void InitSteamClient()
    {
        try
        {
            if (!SteamClient.IsValid || !SteamClient.IsLoggedOn)
            {
                SteamClient.Init(440310);
                StaticManager.LocalPlayer = SteamClient.SteamId;
            }

        }
        catch(Exception e)
        {
            Plugin.Inst.Log.LogError(e);
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        InitSteamClient();
        StaticManager.IsHosting = false;
        StaticManager.IsMultiplayer = true;
        Plugin.Logger.LogInfo($"Joining friend's lobby owned by [{lobby.Owner.Id}]");
        SteamMatchmaking.JoinLobbyAsync(steamId);
    }

    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        InitSteamClient();
        Plugin.Logger.LogInfo($"Invite received from [{friend.Name}]");
        //handle invite dialog
    }

    public void StartClient(SteamId targetSteamId)
    {
        Plugin.Logger.LogInfo("Starting client.");

        Client = SteamNetworkingSockets.ConnectRelay<VGConnectionManager>(targetSteamId);
    }

    public void EndClient()
    {
        Client.Close();
        SteamLobbyManager.Inst.CurrentLobby.Leave();
        StaticManager.IsMultiplayer = false;
        StaticManager.IsHosting = false;
    }
    public void StartServer()
    {
        Server = SteamNetworkingSockets.CreateRelaySocket<VGSocketManager>();
    }

    public void EndServer()
    {
        Server.Close();
        Server.Socket.Close();
    }
    
}
//ye im stealing the VG prefix used in some PA classes
//I assume it means Vitamin Games
public class VGSocketManager : SocketManager
{
    public int LatestCheckpoint = 0;
    
    public override void OnConnecting(Connection connection, ConnectionInfo data)
    {
        base.OnConnecting(connection, data);
        Plugin.Inst.Log.LogInfo($"Server: {data.Identity.SteamId} is connecting");
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
        NetPacket packet = Marshal.PtrToStructure<NetPacket>(data);

        switch (packet.PacketType)
        {
            case PacketType.Damage:
                SendMessages(Connected, data, size);
                break;
            case PacketType.Start:
                break;
            case PacketType.Loaded:
                SendMessages(Connected, data, size);
                break;
            case PacketType.Position:
                SendMessages(Connected, data, size, SendType.Unreliable);
                break;
            case PacketType.Rotation:
                break;
            case PacketType.Spawn:
                break;
        }
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
        SendMessages(Connected, new NetPacket(){SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Start});
    }

    public void SendCheckpointHit(int index)
    {
        LatestCheckpoint = index;
        SendMessages(Connected, new NetPacket(){SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Checkpoint, Data = index});
    }

    public void SendRewindToCheckpoint(int index)
    {
        SendMessages(Connected, new NetPacket(){SenderId = StaticManager.LocalPlayer, PacketType = PacketType.Rewind, Data = index});
    }
}

public class VGConnectionManager : ConnectionManager
{
    public override void OnConnecting(ConnectionInfo info)
    {
        Plugin.Logger.LogInfo($"Client: Connecting with Steam user {info.Identity.SteamId}.");
    }

    public override void OnConnected(ConnectionInfo info)
    {
        Plugin.Logger.LogInfo($"Client: Connected with Steam user {info.Identity.SteamId}.");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        //  InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);
        
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
        NetPacket packet = Marshal.PtrToStructure<NetPacket>(data);

        switch (packet.PacketType)
        {
            case PacketType.Damage:
                Plugin.Inst.Log.LogWarning($"Damaging player {packet.SenderId}");
                
                if (packet.SenderId == StaticManager.LocalPlayer) return;
           
                VGPlayer player = StaticManager.Players[packet.SenderId].PlayerObject;
                if (!player) return;
                player.Health = (int)packet.Data;
                player.PlayerHit();
                break;
            
            case PacketType.Start:
                LobbyManager.Instance.StartLevel();
                break;
            
            case PacketType.Loaded:
                if (packet.SenderId == StaticManager.LocalPlayer) return; 
                
                SteamLobbyManager.Inst.SetLoaded(packet.SenderId);
                if(LobbyManager.Instance)
                {
                    LobbyManager.Instance.SetPlayerLoaded(packet.SenderId);
                }
                break;
            
            case PacketType.Position:
                if (packet.SenderId == StaticManager.LocalPlayer)
                    return;
                
                SteamId sender = packet.SenderId;
                Vector2 pos = (Vector2)packet.Data;
                if (StaticManager.Players.TryGetValue(packet.SenderId, out var playerData))
                {
                    if (playerData.PlayerObject)
                    {
                        if (!StaticManager.PlayerPositions.ContainsKey(sender))
                        {
                            StaticManager.PlayerPositions.Add(sender, Vector2.zero);
                            return;
                        }

                        StaticManager.PlayerPositions[sender] = pos;
                    }
                }
                break;
            
            case PacketType.Rotation:
                break;
            
            case PacketType.Spawn:
                break;
            case PacketType.Checkpoint:
                Plugin.Logger.LogInfo($"Checkpoint [{(int)packet.Data}] Received");
                GameManager.Inst.playingCheckpointAnimation = true;
                VGPlayerManager.Inst.RespawnPlayers();
                
                GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation((int)packet.Data));
                break;
            case PacketType.Rewind:
                Plugin.Logger.LogInfo($"Rewind to Checkpoint [{(int)packet.Data}] Received");
                VGPlayerManager.Inst.players.ForEach(new Action<VGPlayerManager.VGPlayerData>(x =>
                {
                    if (!x.PlayerObject.isDead)
                    {
                        x.PlayerObject.Health = 1;
                        x.PlayerObject.PlayerHit();
                        //forcekill() fucked up the game.
                    }
                }));
                GameManager.Inst.RewindToCheckpoint((int)packet.Data);
                break;
        }
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

[HarmonyPatch(typeof(SystemManager))]
public class SystemManager_Patch
{
    [HarmonyPatch(nameof(SystemManager.Awake))]
    [HarmonyPostfix]
    static void AddSteamManager(ref SystemManager __instance)
    {
        __instance.gameObject.AddComponent<SteamManager>();
        __instance.gameObject.AddComponent<SteamLobbyManager>();
    }
}