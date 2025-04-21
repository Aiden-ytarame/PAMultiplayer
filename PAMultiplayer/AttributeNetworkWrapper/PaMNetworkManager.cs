using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
using Network_Test.Core;
using Network_Test.Core.Rpc;
using PAMultiplayer.Managers;
using Steamworks;
using NetworkManager = Network_Test.NetworkManager;

namespace PAMultiplayer.AttributeNetworkWrapper;

public class PaMNetworkManager : NetworkManager
{
    FacepunchSocketsTransport _facepunchtransport;
    public static PaMNetworkManager PamInstance { get; private set; }

    public Dictionary<FacepunchSocketsTransport.ConnectionWrapper, int> ConnectionWrappers =>
        _facepunchtransport.ConnectionWrappers;
    
    public void Receive()
    {
        _facepunchtransport?.Receive();
    }
    
    public override void OnClientConnected(ServerNetworkConnection connection)
    {
        base.OnClientConnected(connection);
    }

    public override void StartServer()
    {
        base.StartServer();
        _facepunchtransport = (FacepunchSocketsTransport)Transport;
        PamInstance = this;
    }

    public override void ConnectToServer(string address)
    {
        base.ConnectToServer(address);
        _facepunchtransport = (FacepunchSocketsTransport)Transport;
        PamInstance = this;
    }

    public override void OnClientDisconnected()
    {
        base.OnClientDisconnected();
        PamInstance = null;
        
        if(!GlobalsManager.IsMultiplayer) return;
        
        SteamManager.Inst.EndClient();
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Menu");
        
    }

    public override void EndServer()
    {
        base.EndServer();
        PamInstance = null;
    }

    public override void OnServerClientConnected(ClientNetworkConnection connection)
    {
        base.OnServerClientConnected(connection);
        
        foreach (var keyValuePair in GlobalsManager.Players)
        {
            RegisterPlayerId(connection, keyValuePair.Key, keyValuePair.Value.VGPlayerData.PlayerID, GlobalsManager.Players.Count);
        }
        
        SteamId steamId = ulong.Parse(connection.Address);
        int id = GlobalsManager.Players[steamId].VGPlayerData.PlayerID;
        GlobalsManager.ConnIdToSteamId.Add(connection.ConnectionId, steamId);
        
        RegisterJoinedPlayerId(steamId, id);
        
    }

    public override void OnServerClientDisconnected(ClientNetworkConnection connection)
    {
        base.OnServerClientDisconnected(connection);
        GlobalsManager.ConnIdToSteamId.Remove(connection.ConnectionId);
    }
    
    private static int _amountOfInfo;

    [ClientRpc]
    public static void RegisterPlayerId(ClientNetworkConnection conn, SteamId steamID, int id, int amount)
    {
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        _amountOfInfo++;
        PAM.Logger.LogInfo($"Player Id from [{id}] Received");

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
            GlobalsManager.HasLoadedBasePlayerIds = true;
        }
    }
    
    [MultiRpc]
    public static void RegisterJoinedPlayerId(SteamId steamID, int id)
    {
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        PAM.Logger.LogInfo($"Player Id from [{id}] Received");

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
    
    
}