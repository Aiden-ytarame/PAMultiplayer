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
        
        LobbyManager.Instance?.RemovePlayerFromLobby(friend.Id);
        RemovePlayerFromLoadList(friend.Id);
        var player = StaticManager.Players[friend.Id].PlayerObject;
        player?.PlayerDeath();
        VGPlayerManager.Inst.players.Remove(StaticManager.Players[friend.Id]);
        StaticManager.Players.Remove(friend.Id);
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
    {
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        AddPlayerToLoadList(friend.Id);

        if (!LobbyManager.Instance) return;
        
        LobbyManager.Instance.AddPlayerToLobby(friend.Id, friend.Name);
          
      
        if (friend.Id == StaticManager.LocalPlayer)
            return;

        int offset = 10;
        if (StaticManager.IsHosting)
            offset = 1;
        
        VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
        NewData.PlayerID = StaticManager.Players.Count + offset; //by the way, this can cause problems
        NewData.ControllerID = StaticManager.Players.Count + 2;
        Plugin.Logger.LogInfo($"Member Joined : [{friend.Name}]");
        
        if (StaticManager.Players.TryAdd(friend.Id, NewData))
        {
            VGPlayerManager.Inst.players.Add(StaticManager.Players[friend.Id]);
        }
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        Plugin.Logger.LogInfo($"Joined Lobby hosted by [{lobby.Owner.Name}]");
        Plugin.Logger.LogInfo($"Level Id [{lobby.GetData("LevelId")}]");
        CurrentLobby = lobby;
        InLobby = true;
      
        if (StaticManager.LocalPlayer == lobby.Owner.Id) return; 
        
        //this could be moved to somewhere before even joining
        //but if it works, we keep
        ulong id = ulong.Parse(lobby.GetData("LevelId"));
        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == id)
            {
                StaticManager.HasLoadedAllInfo = false;
                
                
                VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
                NewData.PlayerID = StaticManager.Players.Count + 10; //by the way, this can cause problems
                NewData.ControllerID = StaticManager.Players.Count + 10;

                var Enu = lobby.Members.GetEnumerator();
                while (Enu.MoveNext())
                {
                    if (StaticManager.Players.TryAdd(Enu.Current.Id, NewData))
                    {
                        if (StaticManager.LocalPlayer == Enu.Current.Id) continue;
                        VGPlayerManager.Inst.players.Add(StaticManager.Players[Enu.Current.Id]);
                    }
                }
                Enu.Dispose();

                SteamManager.Inst.StartClient(lobby.Owner.Id);
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
      
        if (!LobbyManager.Instance?.pauseMenu) return; //this is for the "Lobby failed to be created" message
        
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