using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    private void Start()
    {
        InitSteamClient();
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
        if (SteamClient.IsValid)
            return;
        try
        {
            Plugin.Inst.Log.LogInfo("Steam Initialized");
            SteamClient.Init(440310);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            StaticManager.LocalPlayer = SteamClient.SteamId;
        }
        catch(Exception e)
        {
            Plugin.Inst.Log.LogError(e);
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        StaticManager.IsHosting = false;
        StaticManager.IsMultiplayer = true;
        Plugin.Logger.LogInfo($"Joining friend's lobby owned by [{steamId}]");
        Plugin.Logger.LogError($"Lobby Id [{lobby.Id.ToString()}]");
        StaticManager.HostId = steamId;
        SteamMatchmaking.JoinLobbyAsync(lobby.Id);
    }

    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Invite received from [{friend.Name}]");
        //handle invite dialog
    }

    public void StartClient(SteamId targetSteamId)
    {
        Plugin.Logger.LogInfo($"Starting client. Connection to [{targetSteamId}]");

        Client = SteamNetworkingSockets.ConnectRelay<VGConnectionManager>(targetSteamId);
        Plugin.Logger.LogInfo(Client.Connected);
    }

    public void EndClient()
    {
        Client?.Close();
        SteamLobbyManager.Inst.LeaveLobby();

        if (StaticManager.IsReloadingLobby)
        {
            StaticManager.IsReloadingLobby = false;
            return;
        }
        StaticManager.IsMultiplayer = false;
        StaticManager.IsHosting = false;
    }
    public void StartServer()
    {
        Plugin.Logger.LogInfo("Starting Server.");
        Server = SteamNetworkingSockets.CreateRelaySocket<VGSocketManager>();
    }

    public void EndServer()
    {
        Server.Close();
        Server.Socket.Close();
    }
    
}


[HarmonyPatch(typeof(SystemManager))]
public class SystemManager_Patch
{
    [HarmonyPatch(nameof(SystemManager.Awake))]
    [HarmonyPostfix]
    static void AddSteamManager(ref SystemManager __instance)
    {
        Plugin.Logger.LogError("Adding Steam Stuff");
        __instance.gameObject.AddComponent<SteamManager>();
        __instance.gameObject.AddComponent<SteamLobbyManager>();
    }
}