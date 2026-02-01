using System;
using AttributeNetworkWrapperV2;
using HarmonyLib;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using WrapperNetworkManager = AttributeNetworkWrapperV2.NetworkManager;
namespace PAMultiplayer.Managers;

/// <summary>
/// manages accepting invites and initializing steam.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Inst { get; private set; }

    private void Awake()
    {
        RpcHandler.TryGetRpcInvoker(1, out var invoker);
        if (Inst)
        {
            Destroy(this);
            return;
        }
        DontDestroyOnLoad(this);
        Inst = this;
        
        SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        InitSteamClient();
    }

    private void Update()
    {
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
            SteamClient.Init(440310, false);
            SteamNetworkingUtils.InitRelayNetworkAccess();
            SteamNetworkingUtils.ConnectionTimeout = 5000;//honestly I dont remember why I set these here
            SteamNetworkingUtils.Timeout = 6000;
            SteamNetworkingUtils.SendRateMax = 524288;
            SteamNetworkingUtils.SendBufferSize = 10485760; //this is set to 10mb~ due to sending audio to clients in challenge mode.
            
            GlobalsManager.LocalPlayerId = SteamClient.SteamId;
            PAM.Logger.LogInfo("Steam Initialized");
        }
        catch(Exception)
        {
            PAM.Logger.LogError("failed to initialize steam");
        }
        
    }

    
    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
    {
        //tylobby.Refresh();
        
        if (lobby.GetData("AlphaMultiplayer") != "true")
        {
            PAM.Logger.LogError($"Tried to join invalid lobby [{lobby.Id.ToString()}]");
            return;
        }
        
        GlobalsManager.IsHosting = false;
        GlobalsManager.IsMultiplayer = true;
        PAM.Logger.LogInfo($"Joining friend's lobby owned by [{steamId}]");
        PAM.Logger.LogError($"Lobby Id [{lobby.Id.ToString()}]");
        
        lobby.Join();
    }
    
    private void OnLobbyInvite(Friend friend, Lobby lobby)
    {
        PAM.Logger.LogInfo($"Invite received from [{friend.Name}]");
        //handle invite dialog
    }
    
    public void StartClient(SteamId targetSteamId)
    {
        PAM.Logger.LogInfo($"Starting client. Connection to [{targetSteamId}]");
        PaMNetworkManager netManager = new PaMNetworkManager();
        netManager.Init(new FacepunchSocketsTransport());
        
        netManager.ConnectToServer(targetSteamId.ToString());
    }

    public void EndClient()
    {
        GlobalsManager.IsReloadingLobby = false;
        GlobalsManager.IsMultiplayer = false;
        GlobalsManager.IsHosting = false;
        GlobalsManager.JoinedMidLevel = false;
        GlobalsManager.HasLoadedLobbyInfo = true;
        
        SteamLobbyManager.Inst.LeaveLobby();
        WrapperNetworkManager.Instance?.Disconnect();
    }
    public void StartServer()
    {
        PAM.Logger.LogInfo("Starting Server.");
        PaMNetworkManager netManager = new PaMNetworkManager();
        
        if (!netManager.Init(new FacepunchSocketsTransport()))
        {
            throw new Exception("Tried to initialize network manager while another one already runs");
        }
        
        netManager.StartServer(true);
    }

    public void EndServer()
    {
        GlobalsManager.IsReloadingLobby = false;
        GlobalsManager.IsMultiplayer = false;
        GlobalsManager.IsHosting = false;
        
        SteamLobbyManager.Inst?.LeaveLobby();
        WrapperNetworkManager.Instance?.EndServer();
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
        PAM.Logger.LogInfo("Adding Steam Stuff");
        __instance.gameObject.AddComponent<SteamManager>();
        __instance.gameObject.AddComponent<SteamLobbyManager>();
    }
}


public static class SteamIdLocalExtension
{
    public static bool IsLocalPlayer(this SteamId id)
    {
        return id == GlobalsManager.LocalPlayerId;
    }
}