using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AttributeNetworkWrapperV2;
using Newtonsoft.Json;
using PaApi;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Patch;
using PAMultiplayer.UI;
using Steamworks;
using Steamworks.Data;
using Systems.SceneManagement;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PAMultiplayer.Managers;

/// <summary>
/// handles the steam lobby callbacks
/// </summary>
public partial class SteamLobbyManager : MonoBehaviour
{
    public enum LobbyState : ushort
    {
        Lobby,
        Playing,
        Challenge,
        Max
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
        if (!Settings.Chat.Value)
        {
            return;
        }
        
        if (message.Length > 25)
        {
            message = message.Substring(0, 25);
        }

        string messageFix = message.Replace('_', ' ');

        if (!friend.IsMe)
        {
            DebugController.inst.AddLog($"<b>{friend.Name}:</b> {messageFix}");
        }
        
        if (!GlobalsManager.Players.TryGetValue(friend.Id, out var player) ||
            !player.VGPlayerData.PlayerObject.IsValidPlayer())
        {
            return;
        }
        
        player.VGPlayerData.PlayerObject.Player_Text.DisplayText(messageFix, 5);
    }

    private void OnLobbyDataChanged(Lobby lobby)
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
        
        AudioManager.Inst?.PlaySound("UI_Glitch", 1);
        
        RemovePlayerFromLoadList(friend.Id);
        
        if(LobbyScreenManager.Instance)
            LobbyScreenManager.Instance.RemovePlayerFromLobby(friend.Id);
        
        if(MultiplayerDiscordManager.IsInitialized)
            MultiplayerDiscordManager.Instance.UpdatePartySize(lobby.MemberCount);
        

        if (GlobalsManager.Players.TryGetValue(friend.Id, out var player))
        {
            string hex = VGPlayerManager.Inst.GetPlayerColorHex(player.VGPlayerData.PlayerID);
            VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{friend.Name}</color>] Disconnected", 2.5f);
            
            VGPlayerManager.Inst.players.Remove(player.VGPlayerData);
            GlobalsManager.Players.Remove(friend.Id);
            
            VGPlayer playerObj = player.VGPlayerData.PlayerObject;

            if (playerObj)
            {
                playerObj.GetDeathEvent()?.Invoke(playerObj.Player_Wrapper.position);
                playerObj.ClearEvents();
                playerObj.PlayerDeath(0);
            }

            PointsManager.Inst?.PlayerLeft(friend.Id);

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
        
        AudioManager.Inst?.PlaySound("UI_Subtract", 1);
        
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
        lobby.Refresh();
        
        if (!lobby.Owner.Id.IsLocalPlayer() && lobby.GetData("AlphaMultiplayer") != "true")
        {
            lobby.Leave();
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.inst.LoadSceneGroup("Menu");
            SteamManager.Inst.EndClient();
            ErrorScreen.CreateErrorScreen($"Tried to join invalid Lobby [<b>{lobby.Id}</b>] by [<b>{lobby.Owner.Name}</b>]!\n\nTry again in a few seconds, if it doesnt work it may be a different MP version, or another mod");

            PAM.Logger.LogError($"Tried to join invalid lobby by [{lobby.Owner.Name}]");
            return;
        }
        
        PAM.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        CurrentLobby = lobby;
        InLobby = true;
        
        int playerAmount = 0;

        if (lobby.Owner.Id.IsLocalPlayer())
        {
            AddPlayerToLoadList(lobby.Owner.Id);
            SetupLobby(lobby);
            return;
        }
        
        foreach (var lobbyMember in lobby.Members)
        {
            VGPlayerManager.VGPlayerData newData = new VGPlayerManager.VGPlayerData();
            newData.PlayerID = playerAmount; //by the way, this can cause problems
            newData.ControllerID = playerAmount;

            GlobalsManager.Players.Add(lobbyMember.Id, new PlayerData(newData, lobbyMember.Name));

            AddPlayerToLoadList(lobbyMember.Id);
            if(CurrentLobby.GetMemberData(lobbyMember, "IsLoaded") == "1")
            {
                SetLoaded(lobbyMember.Id);
            }
            playerAmount++;
        }

        GlobalsManager.HasLoadedExternalInfo = false;
        GlobalsManager.HasLoadedBasePlayerIds = false;
        GlobalsManager.HasLoadedMainLobbyInfo = false;
        GlobalsManager.HasLoadedMidLobbyInfo = false;
        
        GlobalsManager.Queue.Clear();
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }


    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            PAM.Logger.LogError($"Failed to create lobby.. Result [{result}]");
            ErrorScreen.CreateErrorScreen($"Failed to create lobby\nSteam result: {result}");

            lobby.Leave();
            SteamManager.Inst.EndServer();
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.inst.LoadSceneGroup("Menu");
            return;
        }
        
        PAM.Logger.LogInfo("Lobby Created!");
    }

    private void SetupLobby(Lobby lobby)
    {
        _loadedPlayers = new();
        
        lobby.SetData("AlphaMultiplayer", "true");

        if (GlobalsManager.IsChallenge)
        {
            lobby.SetData("LobbyState", ((ushort)LobbyState.Challenge).ToString());
            PaMNetworkManager.CallRpc_Multi_UpdateLobbyState((byte)LobbyState.Challenge);
        }
        else
        {
            VGLevel currentLevel = ArcadeManager.Inst.CurrentArcadeLevel;
            GlobalsManager.LevelId = currentLevel.SteamInfo != null ?  currentLevel.SteamInfo.ItemID.Value.ToString() : currentLevel.name;
            
            lobby.SetData("LevelId", GlobalsManager.LevelId);
            lobby.SetData("seed", RandSeed.ToString());
            lobby.SetData("LobbyState", ((ushort)LobbyState.Lobby).ToString());
            PaMNetworkManager.CallRpc_Multi_UpdateLobbyState((byte)LobbyState.Lobby);
        }
      
        lobby.SetData("LevelQueue", JsonConvert.SerializeObject(GlobalsManager.GetQueueLevelNames()));
        lobby.SetData("HealthMod", DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0).ToString());
        lobby.SetData("LinkedMod", DataManager.inst.GetSettingBool("mp_linkedHealth", false).ToString());
        lobby.SetData("SpeedMod", DataManager.inst.GetSettingEnum("ArcadeSpeedMod", 0).ToString());
        
        if (LobbyCreationManager.Instance.IsPrivate)
        {
            lobby.SetFriendsOnly();
        }
        else
        {
            lobby.SetPublic();
        }

        lobby.SetJoinable(true);
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
        if (!InLobby)
        {
            return;
        }
        
        InLobby = false;
        CurrentLobby.Leave();
    }

    public void UnloadAll()
    {
        foreach (var key in _loadedPlayers.Keys.ToList())
        {
            _loadedPlayers[key] = false;
        }
    }

    public bool IsPlayerLoaded(SteamId playerSteamId)
    {
        return _loadedPlayers.GetValueOrDefault(playerSteamId, false);
    }

    public int LoadedPlayerCount()
    {
        return _loadedPlayers.Count(x => x.Value);
    }
    
    public bool  IsEveryoneLoaded => !_loadedPlayers.ContainsValue(false);
}