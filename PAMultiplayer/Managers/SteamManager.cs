using System;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

/// <summary>
/// manages accepting invites and initializing steam.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Inst { get; private set; }
    public PAMSocketManager Server;
    public PAMConnectionManager Client;

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
        InitSteamClient();
        
        if (SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    private void OnApplicationQuit()
    {
        EndServer();
        EndClient();
    }
    public void InitSteamClient()
    {
        if (SteamClient.IsValid)
            return;
            
        try
        {
            Plugin.Inst.Log.LogInfo("Steam Initialized");
            SteamClient.Init(440310, false);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            SteamNetworkingUtils.ConnectionTimeout = 5000;
            SteamNetworkingUtils.Timeout = 6000;
   
            GlobalsManager.LocalPlayer = SteamClient.SteamId;
        }
        catch(Exception)
        {
            Plugin.Inst.Log.LogError("failed to initialize steam");
        }
    }

    
    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        GlobalsManager.IsHosting = false;
        GlobalsManager.IsMultiplayer = true;
        Plugin.Logger.LogInfo($"Joining friend's lobby owned by [{steamId}]");
        Plugin.Logger.LogError($"Lobby Id [{lobby.Id.ToString()}]");
        
        lobby.Join();
    }
    
    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Invite received from [{friend.Name}]");
        //handle invite dialog
    }

    public void StartClient(SteamId targetSteamId)
    {
        Plugin.Logger.LogInfo($"Starting client. Connection to [{targetSteamId}]");
        Client = SteamNetworkingSockets.ConnectRelay<PAMConnectionManager>(targetSteamId);
    }

    public void EndClient()
    {
        Client?.Close();
        SteamLobbyManager.Inst.LeaveLobby();
        if (GlobalsManager.IsReloadingLobby)
        {
            GlobalsManager.IsReloadingLobby = false;
            return;
        }
        
        GlobalsManager.IsMultiplayer = false;
        GlobalsManager.IsHosting = false;
    }
    public void StartServer()
    {
        Plugin.Logger.LogInfo("Starting Server.");
        Server = SteamNetworkingSockets.CreateRelaySocket<PAMSocketManager>();
        
        //if the server port doesn't change it fails to create a socket
        //why? don't ask me man
    }

    public void EndServer()
    {
        SteamLobbyManager.Inst.LeaveLobby();
        Server?.Close();
        
        if (GlobalsManager.IsReloadingLobby)
        {
            GlobalsManager.IsReloadingLobby = false;
            return;
        }
        GlobalsManager.IsMultiplayer = false;
        GlobalsManager.IsHosting = false;
    }
    
}


//adds steam related stuff 
[HarmonyPatch(typeof(SystemManager))]
public class SystemManagerPatch
{
    [HarmonyPatch(nameof(SystemManager.Awake))]
    [HarmonyPostfix]
    static void AddSteamManager(ref SystemManager __instance)
    {
        __instance.transform.Find("Discord")?.gameObject.SetActive(false);
        Plugin.Logger.LogError("Adding Steam Stuff");
        __instance.gameObject.AddComponent<SteamManager>();
        __instance.gameObject.AddComponent<SteamLobbyManager>();
    }
}


public static class SteamIdLocalExtension
{
    public static bool IsLocalPlayer(this SteamId id)
    {
        return id == GlobalsManager.LocalPlayer;
    }
}