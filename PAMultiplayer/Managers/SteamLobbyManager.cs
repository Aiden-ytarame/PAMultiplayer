using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
using Newtonsoft.Json;
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
        SteamMatchmaking.OnLobbyDataChanged += OnOnLobbyDataChanged;
    }

    private void OnOnLobbyDataChanged(Lobby lobby)
    {
        if (LobbyScreenManager.Instance)
        {
            LobbyScreenManager.Instance.UpdateQueue();
        }
    }

    private void OnLobbyMemberDataChanged(Lobby lobby, Friend friend)
    {
        //data changed always means loaded
        
        if (lobby.GetMemberData(friend, "IsLoaded") != "1") return;

        SetLoaded(friend.Id);
        
        if (LobbyScreenManager.Instance)
        {
            LobbyScreenManager.Instance.SetPlayerLoaded(friend.Id);
        }
    }

    private void OnLobbyMemberDisconnected(Lobby lobby, Friend friend)
    {
        PAM.Logger.LogInfo($"Member Left : [{friend.Name}]");
        
        AudioManager.Inst?.PlaySound("glitch", 1);
        
        RemovePlayerFromLoadList(friend.Id);
        
        if(LobbyScreenManager.Instance)
            LobbyScreenManager.Instance.RemovePlayerFromLobby(friend.Id);
        
        if(MultiplayerDiscordManager.IsInitialized)
            MultiplayerDiscordManager.Instance.UpdatePartySize(lobby.MemberCount);
        

        if (GlobalsManager.Players.TryGetValue(friend.Id, out var player))
        {
            VGPlayerManager.Inst.players.Remove(player);
            GlobalsManager.Players.Remove(friend.Id);
            
            VGPlayer playerObj = player.PlayerObject;
            
            if (!playerObj) return;
            
            playerObj.DeathEvent?.Invoke(playerObj.Player_Wrapper.position);
            playerObj.ClearEvents();
            playerObj.PlayerDeath(0);
        }
        
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        PAM.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        AudioManager.Inst?.PlaySound("Subtract", 1);
        
        AddPlayerToLoadList(friend.Id);
        
        if(LobbyScreenManager.Instance)
            LobbyScreenManager.Instance.AddPlayerToLobby(friend.Id, friend.Name);
        
        if(MultiplayerDiscordManager.IsInitialized)
            MultiplayerDiscordManager.Instance.UpdatePartySize(lobby.MemberCount);

        HashSet<int> usedIds = new();
        int nextId = 0;
        foreach (var player in GlobalsManager.Players)
        {
            usedIds.Add(player.Value.PlayerID);
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
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        PAM.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        PAM.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        CurrentLobby = lobby;
        InLobby = true;
        int _playerAmount = 0;
        
        if (lobby.Owner.Id.IsLocalPlayer()) return;

        foreach (var lobbyMember in lobby.Members)
        {
            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = _playerAmount; //by the way, this can cause problems
            NewData.ControllerID = _playerAmount;

            GlobalsManager.Players.Add(lobbyMember.Id, NewData);

            AddPlayerToLoadList(lobbyMember.Id);
            if(CurrentLobby.GetMemberData(lobbyMember, "IsLoaded") == "1")
            {
                SetLoaded(lobbyMember.Id);
            }
            _playerAmount++;
        }

        GlobalsManager.HasLoadedExternalInfo = false;
        GlobalsManager.HasLoadedBasePlayerIds = false;
     
        if (ulong.TryParse(lobby.GetData("LevelId"), out var levelId))
        {
            GlobalsManager.LevelId = levelId;
        }
        else
        {
            CurrentLobby.Leave();
            PAM.Logger.LogFatal("Invalid LevelId! something went very wrong.");
            return;
        }
        
        if (int.TryParse(lobby.GetData("HealthMod"), out var healthMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", healthMod);
        }
        else
        {
            PAM.Logger.LogInfo("No Health Mod specified.");
        }
        
        if (int.TryParse(lobby.GetData("SpeedMod"), out var speedMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", speedMod);
        }
        else
        {
            PAM.Logger.LogInfo("No Speed Mod specified.");
        }
        
        if(int.TryParse(lobby.GetData("seed"), out int seed))
        {
            RandSeed = seed;
        }
        else
        {
            RandSeed = UnityEngine.Random.seed;
            PAM.Logger.LogFatal("Failed to parse random seed.");
        }
        PAM.Logger.LogInfo($"SEED : {RandSeed}");

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
        PAM.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }


    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            PAM.Logger.LogError($"Failed to create lobby : Result [{result}]");
            lobby.Leave();
        }
        PAM.Logger.LogInfo($"Lobby Created!");

        _loadedPlayers = new();
        InLobby = true;
        GlobalsManager.LevelId = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.Value;
        lobby.SetData("LevelId", GlobalsManager.LevelId.ToString());
        lobby.SetData("seed", RandSeed.ToString());

        List<string> levelNames = new();
        foreach (var id in GlobalsManager.Queue)
        {
            VGLevel level = ArcadeLevelDataManager.Inst.GetSteamLevel(ulong.Parse(id));
            levelNames.Add(level.TrackName);
        }
        lobby.SetData("LevelQueue", JsonConvert.SerializeObject(levelNames));
        
        lobby.SetData("HealthMod", DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0).ToString());
        lobby.SetData("SpeedMod", DataManager.inst.GetSettingEnum("ArcadeSpeedMod", 0).ToString());
        
        //this actually might not need to exist
        //since we should go back to the menu on lobby failed
        //but I never tested this so we keep just in case
     //   if (!LobbyScreenManager.Instance?.pauseMenu) return; //this is for the "Lobby failed to be created" message
        
      //  LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/buttons").gameObject.SetActive(true);
   //     LobbyScreenManager.Instance.pauseMenu.transform.Find("Content/LobbyFailed").gameObject.SetActive(false);
    }
    
    private void AddPlayerToLoadList(SteamId playerSteamId)
    {
        _loadedPlayers.TryAdd(playerSteamId, false);
    }

    private void RemovePlayerFromLoadList(SteamId player)
    {
        _loadedPlayers?.Remove(player);
    }

    private void SetLoaded(SteamId playerSteamId)
    {
        if(_loadedPlayers.ContainsKey(playerSteamId))
            _loadedPlayers[playerSteamId] = true;
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

    public void UnloadAll()
    {
        foreach (var keyValuePair in _loadedPlayers)
        {
            _loadedPlayers[keyValuePair.Key] = false;
        }
    }

    public bool GetIsPlayerLoaded(SteamId playerSteamId)
    {
        return _loadedPlayers.GetValueOrDefault(playerSteamId, false);
    }
    public bool IsEveryoneLoaded => !_loadedPlayers.ContainsValue(false);
}