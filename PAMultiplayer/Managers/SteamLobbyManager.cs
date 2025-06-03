using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
using Newtonsoft.Json;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers.MenuManagers;
using PAMultiplayer.Patch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PAMultiplayer.Managers;

/// <summary>
/// handles the steam lobby callbacks
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public enum LobbyState : ushort
    {
        Lobby,
        Playing,
        Challenge
    }
    
    public Lobby CurrentLobby;
    public bool InLobby { get; private set; }
    public static SteamLobbyManager Inst;
    
    
    private Dictionary<SteamId, bool> _loadedPlayers = new();
    public int RandSeed = 0;
    
    public void CreateLobby()
    {
        SteamManager.Inst.StartServer();

        int count = LobbyCreationManager.Instance.PlayerCount;
        SteamMatchmaking.CreateLobbyAsync(count);
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
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
        
        SteamMatchmaking.OnChatMessage += OnChatMessage;
    }

    private void OnChatMessage(Lobby lobby, Friend friend, string message)
    {
        if (!DataManager.inst.GetSettingBool("MpChatEnabled", true))
        {
            return;
        }
        
        if (!GlobalsManager.Players.TryGetValue(friend.Id, out var player))
        {
            return;
        }

        if (!player.VGPlayerData.PlayerObject.IsValidPlayer())
        {
            return;
        }
        
        if (message.Length > 25)
        {
            message = message.Substring(0, 25);
        }
        
        //ADD BACK player.VGPlayerData.PlayerObject.SpeechBubble?.DisplayText(message.Replace('_',' '), 5);
        player.VGPlayerData.PlayerObject.Player_Text.DisplayText(message.Replace('_',' '), 5);
    }

    private void OnLobbyDataChanged(Lobby lobby)
    {
        if (LobbyScreenManager.Instance)
        {
            LobbyScreenManager.Instance.UpdateQueue();
        }
        
        if (bool.TryParse(lobby.GetData("LinkedMod"), out var linkedMod))
        {
            DataManager.inst.UpdateSettingBool("mp_linkedHealth", linkedMod);
        }
        
        if (ushort.TryParse(lobby.GetData("LobbyState"), out var lobbyState))
        {
            GlobalsManager.LobbyState = (LobbyState)lobbyState;
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
            string hex = VGPlayerManager.Inst.GetPlayerColorHex(player.VGPlayerData.PlayerID);
            if (hex == "#FFFFFF")
            {
                hex = "FFFFFF";
            }
            VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{friend.Name}</color>] Disconnected", 2.5f);
            
            VGPlayerManager.Inst.players.Remove(player.VGPlayerData);
            GlobalsManager.Players.Remove(friend.Id);
            
            VGPlayer playerObj = player.VGPlayerData.PlayerObject;

            if (playerObj)
            {
                playerObj.DeathEvent?.Invoke(playerObj.Player_Wrapper.position);
                playerObj.ClearEvents();
                playerObj.PlayerDeath(0);
            }


            if (!GlobalsManager.IsHosting)
            {
                return;
            }
            
            if (PaMNetworkManager.PamInstance.SteamIdToNetId.TryGetValue(friend.Id, out var netId))
            {
                PaMNetworkManager.PamInstance.KickClient(netId);
            }
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
        int nextId = 1;
        foreach (var player in GlobalsManager.Players)
        {
            usedIds.Add(player.Value.VGPlayerData.PlayerID);
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
        
        string hex = VGPlayerManager.Inst.GetPlayerColorHex(newData.PlayerID);
        if (hex == "#FFFFFF")
        {
            hex = "FFFFFF";
        }
        
        VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{friend.Name}</color>] Joined", 2.5f);

        if(GlobalsManager.Players.TryAdd(friend.Id, new PlayerData(newData, friend.Name)))
        {
            //do not add new players if on loading screen 
            if (GameManager.Inst && GameManager.Inst.CurGameState != GameManager.GameState.Loading && GlobalsManager.LobbyState != LobbyState.Playing)
            {
                VGPlayerManager.Inst.players.Add(GlobalsManager.Players[friend.Id].VGPlayerData);
            }
        }
        else
        {
            GlobalsManager.Players[friend.Id].SetName(friend.Name);
        }
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        PAM.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        PAM.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        CurrentLobby = lobby;
        InLobby = true;
        int _playerAmount = 0;

        if (lobby.Owner.Id.IsLocalPlayer())
        {
            AddPlayerToLoadList(lobby.Owner.Id);
            return;
        }

        foreach (var lobbyMember in lobby.Members)
        {
            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = _playerAmount; //by the way, this can cause problems
            NewData.ControllerID = _playerAmount;

            GlobalsManager.Players.Add(lobbyMember.Id, new PlayerData(NewData, lobbyMember.Name));

            AddPlayerToLoadList(lobbyMember.Id);
            if(CurrentLobby.GetMemberData(lobbyMember, "IsLoaded") == "1")
            {
                SetLoaded(lobbyMember.Id);
            }
            _playerAmount++;
        }

        GlobalsManager.HasLoadedExternalInfo = false;
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        if (ushort.TryParse(lobby.GetData("LobbyState"), out var lobbyState))
        {
            GlobalsManager.LobbyState = (LobbyState)lobbyState;
        }
        else
        {
            CurrentLobby.Leave();
            PAM.Logger.LogFatal("No lobby state specified!");
            return;
        }

        if (GlobalsManager.LobbyState != LobbyState.Challenge)
        {
            string levelId = lobby.GetData("LevelId");
            if (!string.IsNullOrEmpty(levelId))
            {
                GlobalsManager.LevelId = levelId;
            }
            else
            {
                CurrentLobby.Leave();
                PAM.Logger.LogFatal("Invalid LevelId! something went very wrong.");
                return;
            }
        }
        
        //modifiers
        if (int.TryParse(lobby.GetData("HealthMod"), out var healthMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", healthMod);
        }
        else
        {
            DataManager.inst.UpdateSettingEnum("ArcadeHealthMod", 0);
            PAM.Logger.LogInfo("No Health Mod specified.");
        }
        //
        if (int.TryParse(lobby.GetData("SpeedMod"), out var speedMod))
        {
            DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", speedMod);
        }
        else
        {
            DataManager.inst.UpdateSettingEnum("ArcadeSpeedMod", 0);
            PAM.Logger.LogInfo("No Speed Mod specified.");
        }
        //
        if (bool.TryParse(lobby.GetData("LinkedMod"), out var linkedMod))
        {
            DataManager.inst.UpdateSettingBool("mp_linkedHealth", linkedMod);
        }
        else
        {
            DataManager.inst.UpdateSettingBool("mp_linkedHealth", false);
            PAM.Logger.LogInfo("No Linked Mod specified.");
        }
     
        
        if(int.TryParse(lobby.GetData("seed"), out int seed))
        {
            RandSeed = seed;
        }
        else
        {
            RandSeed = Random.seed;
            PAM.Logger.LogFatal("Failed to parse random seed.");
        }
        PAM.Logger.LogInfo($"SEED : {RandSeed}");

        GlobalsManager.Queue.Clear();

        var level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.LevelId);
        if (level != null)
        {
            ArcadeManager.Inst.CurrentArcadeLevel = level;
            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            return;
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
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.inst.LoadSceneGroup("Menu");
            return;
        }
        PAM.Logger.LogInfo($"Lobby Created!");
        
        _loadedPlayers = new();
        CurrentLobby = lobby;
        InLobby = true;
        
        if (LobbyCreationManager.Instance.IsPrivate)
        {
            lobby.SetFriendsOnly();
        }
        else
        {
            lobby.SetPublic();
        }

        lobby.SetJoinable(true);
        
        VGLevel currentLevel = ArcadeManager.Inst.CurrentArcadeLevel;
        GlobalsManager.LevelId = currentLevel.SteamInfo != null ?  currentLevel.SteamInfo.ItemID.Value.ToString() : currentLevel.name;
        lobby.SetData("LevelId", GlobalsManager.LevelId);
        lobby.SetData("seed", RandSeed.ToString());

        if (GlobalsManager.IsChallenge)
        {
            lobby.SetData("LobbyState", ((ushort)LobbyState.Challenge).ToString());
        }
        else
        {
            lobby.SetData("LobbyState", ((ushort)LobbyState.Lobby).ToString());
        }

        List<string> levelNames = new();
        foreach (var id in GlobalsManager.Queue)
        {
            VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
            levelNames.Add(level.TrackName);
        }
        lobby.SetData("LevelQueue", JsonConvert.SerializeObject(levelNames));
        lobby.SetData("HealthMod", DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0).ToString());
        lobby.SetData("LinkedMod", DataManager.inst.GetSettingBool("mp_linkedHealth", false).ToString());
        lobby.SetData("SpeedMod", DataManager.inst.GetSettingEnum("ArcadeSpeedMod", 0).ToString());
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
        _loadedPlayers[playerSteamId] = true;
    }
    
    public void LeaveLobby()
    {
        InLobby = false;
        CurrentLobby.Leave();
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