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
    }
    private void Update()
    {
        InitSteamClient();
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
            SteamClient.Init(440310);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            StaticManager.LocalPlayer = SteamClient.SteamId;
            
            RequestLobbies();
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
        var result = await lobby.Join();
        Plugin.Logger.LogError(result);
        if (result != RoomEnter.Success)
        {
            if (_joinAttempts > 3)
            {
                Plugin.Logger.LogError("failed to Join lobby");
                _joinAttempts = 0;
                return;
            }
            _joinAttempts++;
            AttemptToJoin(lobby);
            return;
        }

        _joinAttempts = 0;
    }

    async void RequestLobbies()
    {
        //testing only
        var result = await new LobbyQuery().WithMaxResults(10).WithSlotsAvailable(1).RequestAsync();
        foreach (var lobby in result)
        {
           Plugin.Logger.LogInfo($"[TEST] Lobby List query entry [{lobby.Owner.Name}]"); 
        }
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