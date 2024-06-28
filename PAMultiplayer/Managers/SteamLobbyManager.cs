using System.Collections.Generic;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

public class SteamLobbyManager : MonoBehaviour
{
    public Lobby CurrentLobby;
    public bool InLobby { get; private set; }
    public static SteamLobbyManager Inst;
    private Dictionary<SteamId, bool> _loadedPlayers = new();
    public void CreateLobby()
    {
        SteamManager.Inst.StartServer();
        SteamMatchmaking.CreateLobbyAsync(4);
    }
    private void Awake()
    {
        if (Inst)
        {
            Destroy(this);
            return;
        }
        DontDestroyOnLoad(this);
        Inst = this;
        
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberDisconnected;
    }

    private void OnApplicationQuit()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberDisconnected -= OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberDisconnected;
   
        CurrentLobby.Leave();
    }
    

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        Plugin.Logger.LogInfo($"Created Game Server! : {lobby.Owner.Name} , {ip} , {port} , {steamId}");
    }
    
    
    private void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        RemovePlayerFromLoadList(friend.Id);

        if (LobbyManager.Instance)
            LobbyManager.Instance.RemovePlayerFromLobby(friend.Id);
        
        var player = StaticManager.Players[friend.Id].PlayerObject;
        player?.PlayerDeath();
        VGPlayerManager.Inst.players.Remove(StaticManager.Players[friend.Id]);
        StaticManager.PlayerPositions.Remove(friend.Id);
        StaticManager.Players.Remove(friend.Id);
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        AddPlayerToLoadList(friend.Id);

        if (LobbyManager.Instance)
            LobbyManager.Instance.AddPlayerToLobby(friend.Id, friend.Name);
        
        if (friend.Id == StaticManager.LocalPlayer)
            return;

        VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
        NewData.PlayerID = StaticManager.Players.Count + 1; //by the way, this can cause problems
        NewData.ControllerID = StaticManager.Players.Count + 1;
            

        if (!VGPlayerManager.Inst.players.Contains(NewData))
            VGPlayerManager.Inst.players.Add(NewData);
        //StaticManager.SpawnPending = true;

        StaticManager.Players.TryAdd(friend.Id, NewData);

        if (!SceneManager.inst.isLoading && !LobbyManager.Instance)
            VGPlayerManager.inst.RespawnPlayers();
        //handle lobby screen;
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Joined Lobby hosted by [{lobby.GetData("HostId")}]");
        Plugin.Logger.LogInfo($"Lobby Id In Lobby [{lobby.Id}]");
        Plugin.Logger.LogInfo($"Owner Id [{lobby.Owner.Id}]");
        Plugin.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        Plugin.Logger.LogInfo($"Host Id from Data [{lobby.GetData("HostId")}]");
        InLobby = true;
        if (StaticManager.LocalPlayer == lobby.Owner.Id) return; 
        
        SteamManager.Inst.StartClient(StaticManager.HostId);
        //this could be moved to somewhere before even joining
        //but if it works, we keep
        ulong id = ulong.Parse(lobby.GetData("LevelId"));
        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == id)
            {
                SaveManager.Inst.CurrentArcadeLevel = level;
                Plugin.Logger.LogInfo($"Level id [{lobby.GetData("LevelId")}]");
                SceneManager.Inst.LoadScene("Arcade Level");
                return;
            }
        }
        Plugin.Logger.LogError($"You did not have the lobby's level downloaded!");
        lobby.Leave();
        InLobby = false;

    }


    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Plugin.Logger.LogError($"Failed to create lobby : Result [{result}]");
            lobby.Leave();
            SceneManager.Inst.LoadScene("Menu");
            return;
        }
        Plugin.Logger.LogInfo($"Lobby Created!");
        Plugin.Logger.LogInfo(lobby.Owner.Id);
        Plugin.Logger.LogInfo(lobby.Id);
        
        CurrentLobby = lobby;
        lobby.SetPublic();
        lobby.SetJoinable(true);
        
        lobby.SetData("LevelId", SaveManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.Value.ToString());
        lobby.SetData("HostId", SteamClient.SteamId.ToString()); //owner.id is broken as shit
        if (!LobbyManager.Instance.pauseMenu) return; //this is for the "Lobby failed to be created" message
        
        LobbyManager.Instance.pauseMenu.transform.Find("Content/buttons").gameObject.SetActive(true);
        LobbyManager.Instance.pauseMenu.transform.Find("Content/LobbyFailed").gameObject.SetActive(false);
    }

    public void StartGame()
    {
        CurrentLobby.SetJoinable(false);
    }

    public void LeaveLobby()
    {
        if(InLobby)
            CurrentLobby.Leave();
        InLobby = false;
    }
    
    public void AddPlayerToLoadList(SteamId playerSteamId)
    {
        _loadedPlayers.TryAdd(playerSteamId, false);
    }

    public void RemovePlayerFromLoadList(SteamId player)
    {
        _loadedPlayers.Remove(player);
    }

    public void SetLoaded(SteamId playerSteamId)
    {
        _loadedPlayers[playerSteamId] = true;
    }
    public bool IsEveryoneLoaded => !_loadedPlayers.ContainsValue(false);
}