using System;
using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
using Lachee.Discord;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers;

/// <summary>
/// handles the steam lobby callbacks
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public Lobby CurrentLobby;
    public bool InLobby { get; private set; }
    public static SteamLobbyManager Inst;
    private Dictionary<SteamId, bool> _loadedPlayers = new();

    public int RandSeed = 0;
    
    //used to prevent 2 players having the same id
    //I should scrap this and make and instead find an open id.
    int _playerAmount = 1;
    public void CreateLobby()
    {
        SteamManager.Inst.StartServer();
        SteamMatchmaking.CreateLobbyAsync(16);
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
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        
        SteamMatchmaking.OnLobbyMemberDisconnected += OnLobbyMemberDisconnected;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberDisconnected;
        
        SteamMatchmaking.OnLobbyMemberDataChanged += OnLobbyMemberDataChanged;
    }

    private void OnLobbyMemberDataChanged(Lobby lobby, Friend friend)
    {
        //data changed always means loaded
        LobbyScreenManager.Instance?.SetPlayerLoaded(friend.Id);
        SetLoaded(friend.Id);
    }

    private void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Left : [{friend.Name}]");
        
        AudioManager.Inst?.PlaySound("glitch", 1);
        
        if(LobbyScreenManager.Instance)
            LobbyScreenManager.Instance.RemovePlayerFromLobby(friend.Id);
        
        if(MultiplayerDiscordManager.IsInitialized)
            MultiplayerDiscordManager.Instance.UpdatePartySize(lobby.MemberCount);
        
        RemovePlayerFromLoadList(friend.Id);

        if (GlobalsManager.Players.TryGetValue(friend.Id, out var player))
        {
            VGPlayerManager.Inst.players.Remove(player);
            GlobalsManager.Players.Remove(friend.Id);
            player.PlayerObject?.ClearEvents();
            player.PlayerObject?.PlayerDeath(0);
        }
        
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        AudioManager.Inst?.PlaySound("Subtract", 1);
        
        AddPlayerToLoadList(friend.Id);
        
        if(LobbyScreenManager.Instance)
            LobbyScreenManager.Instance.AddPlayerToLobby(friend.Id, friend.Name);
        
        if(MultiplayerDiscordManager.IsInitialized)
            MultiplayerDiscordManager.Instance.UpdatePartySize(lobby.MemberCount);

        HashSet<int> usedIds = new();
        int nextId = 0;
        foreach (var player in VGPlayerManager.Inst.players)
        {
            usedIds.Add(player.PlayerID);
        }

        while (true)
        {
            if (usedIds.Contains(nextId))
            {
                nextId++;
                continue;
            }

            break;
        }

        VGPlayerManager.VGPlayerData newData = new()
        {
            PlayerID = nextId,
            ControllerID = nextId
        };
        
        GlobalsManager.Players.TryAdd(friend.Id, newData);
        {
            //do not add new players if on loading screen 
            if (GameManager.Inst && GameManager.Inst.CurGameState != GameManager.GameState.Loading)
            {
                VGPlayerManager.Inst.players.Add(GlobalsManager.Players[friend.Id]);
            }
        }
        VGPlayerManager.Inst.RespawnPlayers();
        _playerAmount++;
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        Plugin.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        CurrentLobby = lobby;
        InLobby = true;
        _playerAmount = 0;
        
        if (lobby.Owner.Id.IsLocalPlayer()) return;

        foreach (var lobbyMember in lobby.Members)
        {
            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = _playerAmount; //by the way, this can cause problems
            NewData.ControllerID = _playerAmount;

            GlobalsManager.Players.Add(lobbyMember.Id, NewData);

            _playerAmount++;
        }

        GlobalsManager.HasLoadedAllInfo = false;
     
        if (ulong.TryParse(lobby.GetData("LevelId"), out var levelId))
        {
            GlobalsManager.LevelId = levelId;
        }
        else
        {
            CurrentLobby.Leave();
            Plugin.Logger.LogFatal("Invalid LevelId! something went very wrong.");
            return;
        }
        
        if (int.TryParse(lobby.GetData("HealthMod"), out var healthMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", healthMod);
        }
        else
        {
            Plugin.Logger.LogInfo("No Health Mod specified.");
        }
        
        if (int.TryParse(lobby.GetData("SpeedMod"), out var speedMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", speedMod);
        }
        else
        {
            Plugin.Logger.LogInfo("No Speed Mod specified.");
        }
        
        Plugin.Logger.LogInfo($"SEED : {lobby.GetData("seed")}");

        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == GlobalsManager.LevelId)
            {
                ArcadeManager.Inst.CurrentArcadeLevel = level;
                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                return;
            }
        }

        GlobalsManager.IsDownloading = true;
        Plugin.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }


    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Plugin.Logger.LogError($"Failed to create lobby : Result [{result}]");
            lobby.Leave();
        }
        Plugin.Logger.LogInfo($"Lobby Created!");

        _loadedPlayers = new();
        InLobby = true;
        GlobalsManager.LevelId = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.Value;
        lobby.SetData("LevelId", GlobalsManager.LevelId.ToString());
        lobby.SetData("seed", RandSeed.ToString());
        lobby.SetData("HealthMod", DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0).ToString());
        lobby.SetData("SpeedMod", DataManager.inst.GetSettingEnum("ArcadeSpeedMod", 0).ToString());
        
        //this actually might not need to exist
        //since we should go back to the menu on lobby failed
        //but I never tested this so we keep just in case
        if (!LobbyScreenManager.Instance?.pauseMenu) return; //this is for the "Lobby failed to be created" message
        
        LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/buttons").gameObject.SetActive(true);
        LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/LobbyFailed").gameObject.SetActive(false);
    }

    public void HideLobby()
    {
        CurrentLobby.SetJoinable(false);
        CurrentLobby.SetPrivate();
    }
    
    public void LeaveLobby()
    {
        CurrentLobby.Leave();
        InLobby = false;
    }
    
    public void AddPlayerToLoadList(SteamId playerSteamId)
    {
        _loadedPlayers.TryAdd(playerSteamId, false);
    }

    public void RemovePlayerFromLoadList(SteamId player)
    {
        _loadedPlayers?.Remove(player);
    }

    public void SetLoaded(SteamId playerSteamId)
    {
        if(_loadedPlayers.ContainsKey(playerSteamId))
            _loadedPlayers[playerSteamId] = true;
    }
    public bool IsEveryoneLoaded => !_loadedPlayers.ContainsValue(false);
}