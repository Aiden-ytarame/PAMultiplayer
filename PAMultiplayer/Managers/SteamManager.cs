using System;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

public class SteamManager : MonoBehaviour
{
    public static SteamManager Inst { get; private set; }
    public VGSocketManager Server;
    public VGConnectionManager Client;

    private int _serverPort = 0;
    private int _joinAttempts;
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

        return;
        SteamNetworkingUtils.OnDebugOutput += (output, s) =>
        {
            Plugin.Logger.LogWarning($"Networking Debug Message : level [{output}], Message [{s}]");
        };

        Dispatch.OnDebugCallback += (type, s, arg3) =>
        {
            Plugin.Logger.LogWarning($"Steam Debug Message : type [{type}], Message [{s}]");
        };
        
        Dispatch.OnException += (e) =>
        {
            Plugin.Logger.LogError($"Steam Exception : Message [{e}]");
        };
    }
    private void Update()
    {
        InitSteamClient();
        if(SteamClient.IsValid)
            SteamClient.RunCallbacks();
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
            SteamNetworkingUtils.DebugLevel = NetDebugOutput.Warning;
            SteamNetworkingUtils.ConnectionTimeout = 5000;
   
            StaticManager.LocalPlayer = SteamClient.SteamId;
            
        }
        catch(Exception e)
        {
            Plugin.Inst.Log.LogError("failed to initialize steam");
        }
    }

    
    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        StaticManager.IsHosting = false;
        StaticManager.IsMultiplayer = true;
        Plugin.Logger.LogInfo($"Joining friend's lobby owned by [{steamId}]");
        Plugin.Logger.LogError($"Lobby Id [{lobby.Id.ToString()}]");
        AttemptToJoin(lobby);
    }

    async void AttemptToJoin(Lobby lobby)
    {
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

        Client = SteamNetworkingSockets.ConnectRelay<VGConnectionManager>(targetSteamId);
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
        Server = SteamNetworkingSockets.CreateRelaySocket<VGSocketManager>(_serverPort);
        //if the server port doesn't change it fails to create a socket
        //why? don't ask me man
        _serverPort++;
    }

    public void EndServer()
    {
        SteamLobbyManager.Inst.LeaveLobby();
        Server.Close();
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
