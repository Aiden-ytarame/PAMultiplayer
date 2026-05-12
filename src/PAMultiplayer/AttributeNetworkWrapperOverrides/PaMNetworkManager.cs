using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AttributeNetworkWrapperV2;
using BepInEx.Bootstrap;
using PAMultiplayer.Managers;
using PAMultiplayer.UI;
using Steamworks;
using Systems.SceneManagement;
using NetworkManager = AttributeNetworkWrapperV2.NetworkManager;

namespace PAMultiplayer.AttributeNetworkWrapperOverrides;

public partial class PaMNetworkManager : NetworkManager
{
    public delegate void ClientModVersionReceived(ulong steamId, string guid, Version version);
    public delegate void MultiplayerStateChanged(bool hosting);
    public delegate void PlayerStateChanged(ulong id);
    
    /// <summary>
    /// Invoked whenever multiplayer starts as either a host or client.
    /// </summary>
    public static event MultiplayerStateChanged OnMultiplayerStart;
    /// <summary>
    /// Invoked whenever multiplayer ends as either a host or client.
    /// </summary>
    public static event MultiplayerStateChanged OnMultiplayerEnd;
    
    /// <summary>
    /// Invoked whenever a player joins the server. Also invoked on clients
    /// </summary>
    public event PlayerStateChanged OnPlayerJoin;
    
    /// <summary>
    /// Invoked whenever a player leaves the server. Also invoked on clients
    /// </summary>
    public event PlayerStateChanged OnPlayerLeave;
    
    /// <summary>
    /// Returns the mod guid and version after asking the client. Returns all 0's if not installed
    /// </summary>
    public event ClientModVersionReceived OnClientModVersionReceived;
    
    
    FacepunchSocketsTransport _facepunchtransport;
    public static PaMNetworkManager PamInstance { get; private set; }

    public Dictionary<ulong, int> SteamIdToNetId =>
        _facepunchtransport.SteamIdToNetId;
    
    public void Receive()
    {
        _facepunchtransport?.Receive();
    }
    
    public override void StartServer(bool serverIsPeer)
    {
        base.StartServer(serverIsPeer);
        _facepunchtransport = (FacepunchSocketsTransport)Transport;
        if (serverIsPeer)
        {
            ClientConnections.Clear();
            ServerSelfPeerConnection = new ClientNetworkConnection(_facepunchtransport.GetNextConnectionId(), GlobalsManager.LocalPlayerId.ToString());
            
            _facepunchtransport.SteamIdToNetId.Add(GlobalsManager.LocalPlayerId, ServerSelfPeerConnection.ConnectionId);
            _facepunchtransport.IDToConnection.Add(ServerSelfPeerConnection.ConnectionId, null);
            GlobalsManager.ConnIdToSteamId.Add(ServerSelfPeerConnection.ConnectionId, GlobalsManager.LocalPlayerId);
            ClientConnections.Add(ServerSelfPeerConnection.ConnectionId, ServerSelfPeerConnection);
        }
        PamInstance = this;
        OnMultiplayerStart?.Invoke(true);
    }

    public override void ConnectToServer(string address)
    {
        PamInstance = this;
        base.ConnectToServer(address);
        _facepunchtransport = (FacepunchSocketsTransport)Transport;
        OnMultiplayerStart?.Invoke(false);
    }

    public override void OnClientDisconnected()
    {
        base.OnClientDisconnected();
        PamInstance = null;
        Shutdown();

        if(!GlobalsManager.IsMultiplayer) return;
        
        SteamManager.Inst.EndClient();
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Menu");
        OnMultiplayerEnd?.Invoke(false);
    }

    public override void EndServer()
    {
        PAM.Logger.LogFatal("EndServer");
        base.EndServer();
        GlobalsManager.ConnIdToSteamId.Clear();
        PamInstance = null;
        Shutdown();
        OnMultiplayerEnd?.Invoke(true);
    }

    public override void OnServerClientConnected(ClientNetworkConnection connection)
    {
        base.OnServerClientConnected(connection);

        if (!ulong.TryParse(GlobalsManager.LevelId, out var level))
        {
            PAM.Logger.LogError("Tried to send local level to client");
            ErrorScreen.CreateErrorScreen("Tried to send local level to client, disconnecting...\n\nHost another level and try again");
            connection.Disconnect();
        }
        
        CallRpc_Client_SetMainLobbyData(connection, level, SteamLobbyManager.Inst.RandSeed, (byte)GlobalsManager.LobbyState, (byte)DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0), (byte)DataManager.inst.GetSettingEnum("ArcadeSpeedMod", 0), DataManager.inst.GetSettingBool("mp_linkedHealth", false));
        foreach (var keyValuePair in GlobalsManager.Players)
        {
            CallRpc_Client_RegisterPlayerId(connection, keyValuePair.Key, keyValuePair.Value.VGPlayerData.PlayerID, GlobalsManager.Players.Count);
        }
        
        SteamId steamId = ulong.Parse(connection.Address);
        int id = GlobalsManager.Players[steamId].VGPlayerData.PlayerID;
        GlobalsManager.ConnIdToSteamId.Add(connection.ConnectionId, steamId);
        
        CallRpc_Multi_RegisterJoinedPlayerId(steamId, id);
        
        PAM.Logger.LogInfo($"Player {connection.Address} joined game server.");
    }

    public override void OnServerClientDisconnected(ClientNetworkConnection connection)
    {
        base.OnServerClientDisconnected(connection);
        GlobalsManager.ConnIdToSteamId.Remove(connection.ConnectionId);
        CallRpc_Multi_PlayerLeft(ulong.Parse(connection.Address));
        
        PAM.Logger.LogInfo($"Player {connection.Address} left game server.");
    }

    public int GetPing()
    {
        return _facepunchtransport?.GetPing() ?? 9999;
    }
    
    private static int _amountOfInfo;

    [ClientRpc]
    private static void Client_RegisterPlayerId(SteamId steamID, int id, int amount)
    {
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        _amountOfInfo++;
        PAM.Logger.LogInfo($"Player Id from [{id}] Received, {steamID}//{amount}");

        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (steamID.IsLocalPlayer())
                GlobalsManager.LocalPlayerObjectId = id;
            
            player.VGPlayerData.PlayerID = id;
        }
        else
        {
            VGPlayerManager.VGPlayerData newData = new()
            {
                PlayerID = id,
                ControllerID = id
            };
            GlobalsManager.Players.Add(steamID, new PlayerData(newData, "placeHolder"));
        }
        
        if (_amountOfInfo >= amount)
        {
            _amountOfInfo = 0;
            PAM.Logger.LogInfo($"Player Id from [{id}] Received");
            GlobalsManager.HasLoadedBasePlayerIds = true;
        }
    }
    
    [MultiRpc]
    private static void Multi_RegisterJoinedPlayerId(SteamId steamID, int id)
    {
        PamInstance?.OnPlayerJoin?.Invoke(steamID);
        
        if (GlobalsManager.IsHosting)
        {
            return;
        }
        
        PAM.Logger.LogInfo($"Multi Player Id from [{id}] Received");

        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (steamID.IsLocalPlayer())
                GlobalsManager.LocalPlayerObjectId = id;
            
            player.VGPlayerData.PlayerID = id;
        }
        else
        {
            VGPlayerManager.VGPlayerData newData = new()
            {
                PlayerID = id,
                ControllerID = id
            };
            GlobalsManager.Players.Add(steamID, new PlayerData(newData, "placeHolder"));
        }
    }

    [ClientRpc]
    private static void Client_SetMainLobbyData(ulong levelId, int seed, byte state, byte healthMod, byte speedMod, bool linked)
    {
        if (state < (byte)SteamLobbyManager.LobbyState.Max)
        {
            GlobalsManager.LobbyState = (SteamLobbyManager.LobbyState)state;
        }

        if (GlobalsManager.LobbyState != SteamLobbyManager.LobbyState.Challenge)
        {
            GlobalsManager.LevelId = levelId.ToString();
        }
        
        //modifiers
        DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", healthMod);
        DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", speedMod);
        DataManager.inst.UpdateSettingBool("mp_linkedHealth", linked);
        SteamLobbyManager.Inst.RandSeed = seed;
        PAM.Logger.LogInfo($"SEED : {seed}");

        GlobalsManager.HasLoadedMainLobbyInfo = true;
    }
    
    [MultiRpc]
    public static void Multi_UpdateLobbyState(byte state)
    {
        if (state < (ushort)SteamLobbyManager.LobbyState.Max)
        {
            GlobalsManager.LobbyState = (SteamLobbyManager.LobbyState)state;
        }
    }
    
    [MultiRpc]
    public static void Multi_UpdateModifier(byte modifier, byte value)
    {
        switch (modifier)
        {
            case 0:
                DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", value);
                break;
            case 1:
                DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", value);
                break;
            case 2:
                DataManager.inst.UpdateSettingBool("mp_linkedHealth", value == 1);
                break;
            default:
                PAM.Logger.LogError($"");
                break;
        }
    }

    
    [MultiRpc]
    private static void Multi_PlayerLeft(SteamId steamId)
    {
        PamInstance?.OnPlayerLeave?.Invoke(steamId);
    }
    
    [ServerRpc]
    private static void Server_SendModVer(ClientNetworkConnection conn, Version version, string guid)
    {
        if (conn.TryGetSteamId(out var steamId))
        {
            PamInstance?.OnClientModVersionReceived?.Invoke(steamId, guid, version);
        }
    }
    
    /// <summary>
    /// Request client to respond if the specified mod is present.
    /// <see cref="OnClientModVersionReceived"/> is invoked with the client's response.
    /// </summary>
    /// <seealso cref="OnClientModVersionReceived"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [ClientRpc]
    public static void Client_AskForMod(string modGuid)
    {
        PAM.Logger.LogInfo($"Mod requested [{modGuid}]");
        if (Chainloader.PluginInfos.TryGetValue(modGuid, out var pluginInfo))
        {
            PAM.Logger.LogInfo($"Mod found");
            CallRpc_Server_SendModVer(pluginInfo.Metadata.Version, modGuid);
            return;
        }
        
        PAM.Logger.LogWarning($"Mod [{modGuid}] was requested by the host, but it was not found.");
        CallRpc_Server_SendModVer(new Version(0,0,0,0), modGuid);
    }

   
    /// <summary>
    /// Kicks the client due to a mod being required
    /// </summary>
    /// <param name="modGuid"></param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [ClientRpc]
    public static void Client_MissingMod(string modGuid)
    {
        PAM.Logger.LogError($"Mod [{modGuid}] is missing or has incorrect version, host requested client to disconnect");
        ErrorScreen.CreateErrorScreen($"Required mod missing, GUID: {modGuid}\n\nDisconnecting...");

        PamInstance.Shutdown();
        PamInstance = null;
        
        if(!GlobalsManager.IsMultiplayer) return;
        
        SteamManager.Inst.EndClient();
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Menu");
    }
}