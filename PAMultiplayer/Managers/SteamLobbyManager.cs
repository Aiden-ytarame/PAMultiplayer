using System;
using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
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
    int amount = 10;
    public void CreateLobby()
    {
        SteamManager.Inst.StartServer();
        SteamMatchmaking.CreateLobbyAsync(8);
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
        CurrentLobby.Leave();
    }
    

    private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId steamId)
    {
        Plugin.Logger.LogInfo($"Created Game Server! : {lobby.Owner.Name} , {ip} , {port} , {steamId}");
    }
    
    
    private void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Left : [{friend.Name}]");
        
        LobbyScreenManager.Instance?.RemovePlayerFromLobby(friend.Id);
        RemovePlayerFromLoadList(friend.Id);
        GlobalsManager.Players.TryGetValue(friend.Id, out var player);
        player?.PlayerObject?.PlayerDeath();
        
        VGPlayerManager.Inst.players.Remove(player);
        GlobalsManager.Players.Remove(friend.Id);
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        AddPlayerToLoadList(friend.Id);
        
        LobbyScreenManager.Instance?.AddPlayerToLobby(friend.Id, friend.Name);
          
      
        if (friend.Id.IsLocalPlayer())
            return;
        
        VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
        NewData.PlayerID = amount; //by the way, this can cause problems
        NewData.ControllerID = amount;
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        if (GlobalsManager.Players.TryAdd(friend.Id, NewData))
        {
            if(GameManager.Inst && GameManager.Inst.CurGameState != GameManager.GameState.Loading)
                VGPlayerManager.Inst.players.Add(GlobalsManager.Players[friend.Id]);
        }

        VGPlayerManager.Inst.RespawnPlayers();
        amount++;
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        Plugin.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        CurrentLobby = lobby;
        InLobby = true;
        
        if (lobby.Owner.Id.IsLocalPlayer())
        {
            amount = 1;
            return;
        }

        amount = 1;
        //this could be moved to somewhere before even joining
        //but if it works, we keep
        ulong id = ulong.Parse(lobby.GetData("LevelId"));
        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == id)
            {
                GlobalsManager.HasLoadedAllInfo = false;

                
                var Enu = lobby.Members.GetEnumerator();
                while (Enu.MoveNext())
                {
                    VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
                    NewData.PlayerID = amount; //by the way, this can cause problems
                    NewData.ControllerID = amount;
                 
                    if (GlobalsManager.Players.TryAdd(Enu.Current.Id, NewData))
                    {
                      //  if(GameManager.Inst && GameManager.Inst.CurGameState != GameManager.GameState.Loading)
                      //      VGPlayerManager.Inst.players.Add(GlobalsManager.Players[Enu.Current.Id]);
                    }

                    amount++;
                }
                Enu.Dispose();
                
                SaveManager.Inst.CurrentArcadeLevel = level;
                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
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
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return;
        }
        Plugin.Logger.LogInfo($"Lobby Created!");
        
        lobby.SetPublic();
        lobby.SetJoinable(true);
        
        lobby.SetData("LevelId", SaveManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.Value.ToString());
      
        if (!LobbyScreenManager.Instance?.pauseMenu) return; //this is for the "Lobby failed to be created" message
        
        LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/buttons").gameObject.SetActive(true);
        LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/LobbyFailed").gameObject.SetActive(false);
        
        
    }

    public void StartGame()
    {
        CurrentLobby.SetJoinable(false);
        CurrentLobby.SetPrivate();
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