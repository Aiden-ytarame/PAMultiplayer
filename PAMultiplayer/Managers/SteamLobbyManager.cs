using System;
using System.Threading.Tasks;
using PAMultiplayer.Patch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

public class SteamLobbyManager : MonoBehaviour
{
    public Lobby CurrentLobby;
    public static SteamLobbyManager Inst;
    
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
        StaticManager.LobbyInfo.RemovePlayerFromLoadList(friend.Id);

        if (LobbyManager.Instance)
            LobbyManager.Instance.RemovePlayerFromLobby(friend.Id);
        
        var player = StaticManager.Players[friend.Id].PlayerObject;
        player.PlayerDeath();
        VGPlayerManager.Inst.players.Remove(StaticManager.Players[friend.Id]);
        StaticManager.PlayerPositions.Remove(friend.Id);
        StaticManager.Players.Remove(friend.Id);
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        StaticManager.LobbyInfo.AddPlayerToLoadList(friend.Id);

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
        Plugin.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Id}]");
        SteamManager.Inst.StartClient(lobby.Owner.Id);

        if (StaticManager.LocalPlayer == lobby.Owner.Id) return; //if not host

        bool match = false;
        ulong id = ulong.Parse(lobby.GetData("LevelId"));
        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == id)
            {
                match = true;
                SaveManager.Inst.CurrentArcadeLevel = level;
                break;
            }
        }

        if (!match)
        {
            Plugin.Logger.LogError($"You did not have the lobby's level downloaded!");
            lobby.Leave();
            return;
        }
        
        SceneManager.Inst.LoadScene("ArcadeLevel");
        
        //handle lobby screen
    }
    

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Plugin.Logger.LogError($"Failed to create lobby : Result [{result}]");
            return;
        }
        Plugin.Logger.LogInfo($"Lobby Created!");
        CurrentLobby = lobby;
        lobby.SetPublic();
        lobby.SetJoinable(true);
        
        lobby.SetData("LevelId", SaveManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.Value.ToString());
    }

    public void StartGame()
    {
        CurrentLobby.SetJoinable(false);
    }
}