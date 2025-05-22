using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystems.SceneManagement;
using AttributeNetworkWrapper.Core;
using Newtonsoft.Json;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers;
using PAMultiplayer.Managers.MenuManagers;
using SimpleJSON;
using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using LobbyState = PAMultiplayer.Managers.SteamLobbyManager.LobbyState;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;


namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(GameManager))]
public class GameManagerPatch
{
    //sets the loading screen awaits
    [HarmonyPatch(nameof(GameManager.Start))]
    [HarmonyPostfix]
    static void PostStart(ref GameManager __instance)
    {
        if (__instance.IsEditor)
            return;

        Transform pauseUi = PauseUIManager.Inst.transform.Find("Pause Menu");
        Transform restartButton = pauseUi.Find("Restart");
        Transform skipButton = pauseUi.Find("Skip Queue Level");

        if (!skipButton)
        {
            skipButton = Transform.Instantiate(restartButton, pauseUi);
            skipButton.name = "Skip Queue Level";
            skipButton.SetSiblingIndex(2);
            
            UIStateManager.Inst.RefreshTextCache(skipButton.Find("Title Wrapper/Title").GetComponent<TextMeshProUGUI>(),
                "Skip Level");
            UIStateManager.Inst.RefreshTextCache(skipButton.Find("Content/Text").GetComponent<TextMeshProUGUI>(),
                "Skips the current level in queue");
            
            PauseUIManager.Inst.PauseMenu.AllViews["main"].Elements.Add(skipButton.GetComponent<UI_Button>());
            
            restartButton.GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
            {
                GlobalsManager.IsReloadingLobby = true;
            }));
        }

       

        var button = skipButton.GetComponent<MultiElementButton>();
        button.onClick = new();
        button.onClick.AddListener(new Action(() =>
        {
            PauseUIManager.Inst.CloseUI();
            GlobalsManager.IsReloadingLobby = true;
            if (GlobalsManager.IsMultiplayer)
            {
                SteamLobbyManager.Inst.UnloadAll();
            }

            if (GlobalsManager.IsChallenge)
            {
                if (GlobalsManager.IsMultiplayer && GlobalsManager.IsHosting)
                {
                    Multi_OpenChallenge();
                }
                SceneLoader.Inst.LoadSceneGroup("Challenge");
                return;
            }
           
            string id = GlobalsManager.Queue[0];
            ArcadeManager.Inst.CurrentArcadeLevel = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
            GlobalsManager.LevelId = id;

            

            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            PAM.Logger.LogInfo("Skipping to next level in queue!");
        }));


        skipButton.gameObject.SetActive(GlobalsManager.Queue.Count >= 2 || GlobalsManager.IsChallenge);

        if (!GlobalsManager.IsMultiplayer)
        {
            restartButton.gameObject.SetActive(true);
            return;
        }

        restartButton.gameObject.SetActive(false);


        if (Player_Patch.CircleMesh == null)
        {
            foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                if (mesh.name == "circle")
                {
                    Player_Patch.CircleMesh = mesh;
                }

                if (mesh.name == "full_arrow")
                {
                    Player_Patch.ArrowMesh = mesh;
                }

                if (mesh.name == "triangle")
                {
                    Player_Patch.TriangleMesh = mesh;
                }
            }
        }

        __instance.gameObject.AddComponent<NetworkManager>();
        __instance.StartCoroutine(FetchExternalData().WrapToIl2Cpp());

        //this is for waiting for the Objects to load before initialing the server/client

        if (GlobalsManager.IsHosting)
        {
            SceneLoader.Inst.manager.AddToLoadingTasks("Creating Lobby", Task.Run(async () =>
            {
                while (!SteamLobbyManager.Inst.InLobby)
                {
                    await Task.Delay(100);
                }
            }).ToIl2Cpp());
        }
        else
        {
            SceneLoader.Inst.manager.AddToLoadingTasks("Connecting to Server", Task.Run(async () =>
            {
                while (PaMNetworkManager.PamInstance == null || !AttributeNetworkWrapper.NetworkManager.Instance.TransportActive)
                {
                    await Task.Delay(100);
                }
            }).ToIl2Cpp());
            
            SceneLoader.Inst.manager.AddToLoadingTasks("Server Info", Task.Run(async () =>
            {
                while (!GlobalsManager.HasLoadedAllInfo)
                {
                    await Task.Delay(100);
                }
            }).ToIl2Cpp());

            skipButton.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(nameof(GameManager.OnDestroy))]
    [HarmonyPostfix]
    static void PostDestroy()
    {
        //if you go to next level in a queue and there was a camera parented object on level end, it stays FOREVER.
        //this makes sure that doesnt happen.
        for (int i = 0; i < CameraDB.Inst.CameraParentedRoot.childCount; i++)
        {
            Object.Destroy(CameraDB.Inst.CameraParentedRoot.GetChild(i).gameObject);
        }
        
        if (GlobalsManager.IsReloadingLobby)
        {
            return;
        }

        GlobalsManager.IsChallenge = false;
        GlobalsManager.IsMultiplayer = false;
    }

    /// <summary>
    /// This is just a test for funsies, used to change special colors without updating the mod.
    /// </summary>
    static IEnumerator FetchExternalData()
    {
        //we could just have this json file on the mp repo,
        //but I dont want to have a bunch of commits just changing the name colors.
        //we also can make this repo not a github page but eh, why not 
        UnityWebRequest webRequest =
            UnityWebRequest.Get("https://aiden-ytarame.github.io/Test-static-files/ColoredNames.json");
        yield return webRequest.SendWebRequest();

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            PAM.Logger.LogError("Failed to fetch external data :(");
            yield break;
        }

        JSONNode nameColors = JSON.Parse(webRequest.downloadHandler.text);
        Dictionary<ulong, string> newColors = new();

        foreach (var playerColor in nameColors)
        {
            if (!ulong.TryParse(playerColor.Key, out var id))
            {
                continue;
            }

            string color = playerColor.Value["color"];
            if (color != null)
            {
                PAM.Logger.LogInfo($"Loaded custom color for { playerColor.Value["name"].Value}");
                newColors.Add(id, color);
            }
        }

        LobbyScreenManager.SpecialColors = newColors;
        GlobalsManager.HasLoadedExternalInfo = true;
    }


    //wtf is this
    private static bool paused;
    [HarmonyPatch(nameof(GameManager.Pause))]
    [HarmonyPrefix]
    static bool PrePause(ref GameManager __instance, bool _showUI)
    {
        if(!GlobalsManager.IsMultiplayer) return true;

        if (!GlobalsManager.HasStarted) return true;

        if (paused)
        {
            __instance.UnPause();
            paused = false;
            return false;
        }
      
        __instance.Paused = false;
        paused = true;
        
        return false;
    }
    
    [HarmonyPatch(nameof(GameManager.UnPause))]
    [HarmonyPostfix]
    static void PostUnPause()
    {
        if(!GlobalsManager.IsMultiplayer) return;
        
        paused = false;
    }

    [HarmonyPatch(nameof(GameManager.OnDestroy))]
    [HarmonyPostfix]
    static void PostOnDestroy()
    {
        if (!MultiplayerDiscordManager.IsInitialized) return;
        
        MultiplayerDiscordManager.Instance.SetMenuPresence();
    }
    
    [HarmonyPatch(nameof(GameManager.PlayGame))]
    [HarmonyPostfix]
    static void PostPlay(GameManager __instance)
    {
        async void setupLevelPresence(string state)
        {
            if (ArcadeManager.Inst.CurrentArcadeLevel)
            {
                var item = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo != null ? await SteamUGC.QueryFileAsync(ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID) : new Item();

                string levelCover = "null_level";
                if (item.HasValue && item.Value.Result == Result.OK)
                {
                   levelCover= item.Value.PreviewImageUrl;
                }
              
                MultiplayerDiscordManager.Instance.SetLevelPresence(state, $"{GameManager.Inst.TrackName} by {GameManager.Inst.ArtistName}", levelCover);
            }
        }
        GlobalsManager.IsReloadingLobby = false;
        GlobalsManager.Queue.Remove(GlobalsManager.LevelId);
        
        //setup discord presence on singleplayer
        if (!GlobalsManager.IsMultiplayer)
        {
            if (!MultiplayerDiscordManager.IsInitialized) return;
            
            if(GameManager.Inst.IsEditor)
            {
                MultiplayerDiscordManager.Instance.SetLevelPresence($"Editing {GameManager.Inst.TrackName}", "", "palogo");
            }
            else if(!__instance.IsArcade)
            {
                MultiplayerDiscordManager.Instance.SetLevelPresence("Playing Story Mode!", $"{GameManager.Inst.TrackName} by {GameManager.Inst.ArtistName}", "palogo");
            }
            else
            {
                setupLevelPresence("Playing Singleplayer!");
            }
            return;
        }

        if (GlobalsManager.LobbyState != LobbyState.Playing)
        {
            //setup lobby screen
            __instance.Pause(false);
            __instance.Paused = true;
            __instance.gameObject.AddComponent<LobbyScreenManager>();
        }
        else
        {
            __instance.Pause(false);
            __instance.Paused = true;
            GlobalsManager.HasStarted = true;
        }


        //camera jiggle in multiplayer is very, very bad sometimes
        EventManager.inst.HasJiggle = false;

        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            vgPlayerData.PlayerObject?.PlayerDeath(0);
        }
        VGPlayerManager.Inst.players.Clear();
        
        //add players to playerManager
        if (GlobalsManager.IsHosting)
        {
            if (GlobalsManager.Players.Count == 0)
            {
                //player 0 is never added, so we add it here
                var newData = new VGPlayerManager.VGPlayerData() { PlayerID = 0, ControllerID = 0 };
                VGPlayerManager.Inst.players.Add(newData);
                GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayerId, new PlayerData(newData, SteamClient.Name));
            }
            else
            {
                foreach (var vgPlayerData in GlobalsManager.Players)
                {
                    VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
                }
            }
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
        }
        else if (AttributeNetworkWrapper.NetworkManager.Instance != null)
        {
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
            }
            
            if (GlobalsManager.LobbyState != LobbyState.Playing)
            {
                GlobalsManager.HasLoadedLobbyInfo = true;
                VGPlayerManager.Inst.RespawnPlayers();
            }
            else
            {
                Server_RequestLobbyState(null);
            }
        }
        else
        {
            //if failed to connect to server
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return;
        }
        
        if (MultiplayerDiscordManager.IsInitialized)
        {
            setupLevelPresence("Playing Multiplayer!");
        }
    }

    [ServerRpc]
    public async static void Server_RequestLobbyState(ClientNetworkConnection conn)
    {
        try
        {
            var endScreen = Object.FindFirstObjectByType<LevelEndScreen>();
            if (!endScreen)
            {
                return;
            }

            while (GameManager.Inst.CurGameState == GameManager.GameState.Reversing)
            {
                await Task.Delay(200);
            }

            List<ulong> playerIds = new();
            List<short> healths = new();
            foreach (var playerDataPair in GlobalsManager.Players)
            {
                playerIds.Add(playerDataPair.Key);
                if (playerDataPair.Value.VGPlayerData.PlayerObject)
                {
                    healths.Add((short)playerDataPair.Value.VGPlayerData.PlayerObject.Health);
                }
                else
                {
                    healths.Add(0);
                }
            }

            Client_LobbyState(conn, endScreen.Hits.Count, GameManager.Inst.CurrentSongTime, playerIds,
                healths.ToArray());

            if (!conn.TryGetSteamId(out SteamId id))
            {
                return;
            }

            if (!VGPlayerManager.Inst.players.Contains(GlobalsManager.Players[id].VGPlayerData))
            {
                VGPlayerManager.Inst.players.Add(GlobalsManager.Players[id].VGPlayerData);
            }

            Multi_LatePlayerJoin(id);
        }
        catch (Exception e)
        {
            PAM.Logger.LogError(e);
        }
    }

    
    [ClientRpc]
    public static void Client_LobbyState(ClientNetworkConnection conn, int hitCount, float currentTime,
        List<ulong> playerIds, Span<short> healths) //weird types is cuz they already have writers, ill fix later
    {
        LevelEndScreen.ActionMoment actionMoment = new();  
        actionMoment.position = Vector3.zero;
        actionMoment.time = currentTime;
        
        var endScreen = Object.FindFirstObjectByType<LevelEndScreen>();//.Hits
        if (!endScreen)
        {
            return;
        }
        for (int i = 0; i < hitCount; i++)
        {
            endScreen.Hits.Add(actionMoment);
        }
        
        if (GlobalsManager.HasLoadedLobbyInfo)
        {
            return;
        }
        GlobalsManager.HasLoadedLobbyInfo = true;
        
        GameManager.Inst.UnPause();
        
        AudioManager.Inst.CurrentAudioSource.time = currentTime;
        VGPlayerManager.inst.RespawnPlayers();
        
        for (var i = 0; i < playerIds.Count; i++)
        {
            var id = playerIds[i];
            var health = healths[i];

            if (GlobalsManager.Players.TryGetValue(id, out var player))
            {
                player.VGPlayerData.PlayerObject.Health = health;
                if (health <= 0)
                {
                    Object.Destroy(player.VGPlayerData.PlayerObject);
                }
            }
        }
    }

    [MultiRpc]
    public static void Multi_LatePlayerJoin(SteamId playerId)
    {
        if (!VGPlayerManager.Inst.players.Contains(GlobalsManager.Players[playerId].VGPlayerData))
        {
            VGPlayerManager.Inst.players.Add(GlobalsManager.Players[playerId].VGPlayerData);
        }
    }
    /// <summary>
    /// download the levels if not downloaded
    /// and wait for the loading screen to end (game doesnt do that by default)
    /// note: the custom loading screen awaits were removed cuz they crashed the game
    /// may add it back later
    /// </summary>
    static IEnumerator CustomLoadGame(VGLevel _level)
    {
         GameManager gm = GameManager.Inst;
         gm.LoadTimer = new();
         gm.LoadTimer.Start();
         
         if (!GlobalsManager.IsReloadingLobby)
         {
             if (GlobalsManager.IsHosting)
             {
                 SteamLobbyManager.Inst.CreateLobby();
                 yield return new WaitUntil(new Func<bool>(() => SteamLobbyManager.Inst.InLobby));
                 SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState", ((ushort)LobbyState.Lobby).ToString());
             }
             else
             {
                 SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                 yield return new WaitUntil(new Func<bool>(() => AttributeNetworkWrapper.NetworkManager.Instance.TransportActive));
                 yield return new WaitUntil(new Func<bool>(() => GlobalsManager.HasLoadedAllInfo ));
                 
                 if (GlobalsManager.LobbyState == LobbyState.Challenge)
                 {
                     SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
                     SceneLoader.Inst.manager.AddToLoadingTasks("Challenge Level Vote", Task.Run(async () =>
                     {
                         while (GlobalsManager.LobbyState == LobbyState.Challenge)
                         {
                             await Task.Delay(100);
                         }
                     }).ToIl2Cpp());
                     
                     yield return new WaitUntil(new Func<bool>(() => GlobalsManager.LobbyState != LobbyState.Challenge));
                     
                     yield break;
                 }
                 
                 if (GlobalsManager.LobbyState == LobbyState.Playing)
                 {
                     GlobalsManager.HasLoadedLobbyInfo = false;
                     GlobalsManager.JoinedMidLevel = true;
                     SceneLoader.Inst.manager.AddToLoadingTasks("Lobby State", Task.Run(async () =>
                     {
                         while (!GlobalsManager.HasLoadedLobbyInfo)
                         {
                             await Task.Delay(100);
                         }
                     }).ToIl2Cpp()); 
                 }
                 else
                 {
                     GlobalsManager.JoinedMidLevel = false;
                 }
             }
         }
        
         if (GlobalsManager.IsDownloading)
         {
             VGLevel levelTest;
             do
             {
                 levelTest = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.LevelId);
                 if (levelTest)
                 {
                     break;
                 }

                 yield return new WaitForSeconds(1);
             } while (SteamWorkshopFacepunch.inst.isLoadingLevels);
                 
             if (levelTest)
             {
                 GlobalsManager.IsDownloading = false;
                 _level = levelTest;
             }
             else
             {
                 //loading screen
                 SceneLoader.Inst.manager.AddToLoadingTasks("Downloading Level", Task.Run(async () =>
                 {
                     while (GlobalsManager.IsDownloading)
                     {
                         await Task.Delay(100);
                     }
                 }).ToIl2Cpp());
                 
                 var item = DownloadLevel();
                 
                 yield return new WaitUntil(new Func<bool>(() => !GlobalsManager.IsDownloading));
                 
                 var result = item.Result;

                 if (result.Id == 0)
                 {
                     yield break; //this prob doesnt need to be here
                 }
                 
                 VGLevel vgLevel = new VGLevel();
                 
                 vgLevel.InitArcadeData(result.Directory);
                 InitSteamInfo(ref vgLevel, result.Id, result.Directory, result);
                 
                 ArcadeLevelDataManager.Inst.ArcadeLevels.Add(vgLevel);
                 
                 yield return gm.StartCoroutine(FileManager.inst.LoadAlbumArt(result.Id.ToString(), result.Directory));
                 yield return gm.StartCoroutine(FileManager.inst.LoadMusic(result.Id.ToString(), result.Directory));
                 
                 _level = vgLevel; 
                 
                 ArcadeManager.Inst.CurrentArcadeLevel = vgLevel;
             }
         }
         gm.LoadMetadata(_level);
         
         yield return gm.StartCoroutine(gm.LoadAudio(_level));
         
         if (GlobalsManager.IsHosting)
         {
             bool skipLevel = true;
             if (ulong.TryParse(GlobalsManager.LevelId, out var nextQueueId))
             {
                 var result = SteamUGC.QueryFileAsync(nextQueueId);

                 while (!result.IsCompleted)
                 {
                     yield return new WaitForUpdate();
                 }
                         
                 var levelItem = result.Result;
                 bool allowHiddenLevel = DataManager.inst.GetSettingBool("MpAllowNonPublicLevels", false);
                 
                 if (levelItem.HasValue && levelItem.Value.Result == Result.OK)
                 {
                     //not public, friends only or private means unlisted which is allowed.
                     if (levelItem.Value.IsPublic || allowHiddenLevel || (!levelItem.Value.IsFriendsOnly && !levelItem.Value.IsPrivate))
                     {
                         skipLevel = false;
                     }
                 }
                 else if (allowHiddenLevel)
                 {
                     skipLevel = false;
                 }
             }
             
             if (skipLevel)
             {
                 PAM.Logger.LogError(
                     "tried playing local or non public level while [Allow hidden levels] is disabled");

                 GlobalsManager.IsReloadingLobby = true;
                 SteamLobbyManager.Inst.UnloadAll();
             
                 SceneLoader.Inst.manager.ClearLoadingTasks();
                 yield return new WaitForSeconds(1); //without this, the loading screen breaks
                 
                 if (GlobalsManager.IsChallenge)
                 {
                     Multi_OpenChallenge();
                     SceneLoader.Inst.LoadSceneGroup("Challenge");
                 }
                 else
                 {
                     GlobalsManager.Queue.Remove(GlobalsManager.LevelId);

                     if (GlobalsManager.Queue.Count == 0)
                     {
                         SceneLoader.Inst.LoadSceneGroup("Arcade");
                         SteamManager.Inst.EndServer();
                         yield break;
                     }
                     
                     string id = GlobalsManager.Queue[0];
                     ArcadeManager.Inst.CurrentArcadeLevel = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
                     GlobalsManager.LevelId = id;
                 }
                 
                 SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                 yield break;
             }
             
             SteamLobbyManager.Inst.RandSeed = Random.seed;
             ObjectManager.inst.seed = Random.seed;
             RNGSync.seed = SteamLobbyManager.Inst.RandSeed;
             if (GlobalsManager.IsReloadingLobby)
             {
                 SteamLobbyManager.Inst.CurrentLobby.SetData("LevelId", GlobalsManager.LevelId);
                 SteamLobbyManager.Inst.CurrentLobby.SetData("seed", SteamLobbyManager.Inst.RandSeed.ToString());
                 SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState", ((ushort)LobbyState.Lobby).ToString());

                 if (!GlobalsManager.IsChallenge)
                 {
                     List<string> levelNames = new();
                     foreach (var id in GlobalsManager.Queue)
                     {
                         VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
                         levelNames.Add(level.TrackName);
                     }

                     SteamLobbyManager.Inst.CurrentLobby.SetData("LevelQueue", JsonConvert.SerializeObject(levelNames));
                 }

                 PAM.Logger.LogError(SteamLobbyManager.Inst.RandSeed);
                 Multi_NextQueueLevel(nextQueueId, SteamLobbyManager.Inst.RandSeed);
             }
         }
         else
         {
             if(int.TryParse(SteamLobbyManager.Inst.CurrentLobby.GetData("seed"), out int seed))
             {
                 SteamLobbyManager.Inst.RandSeed = seed;
             }
             RNGSync.seed = SteamLobbyManager.Inst.RandSeed;
             ObjectManager.inst.seed = SteamLobbyManager.Inst.RandSeed;
         }
         
         try
         {
             gm.LoadData(_level);
         }
         catch (Exception e)
         {
             PAM.Logger.LogFatal("LEVEL FAILED TO LOAD, going back to menu");
             PAM.Logger.LogDebug(e);
        
             GlobalsManager.IsReloadingLobby = false;
             
             if (!GlobalsManager.IsHosting)
             {
                 SteamManager.Inst.EndClient();
             }
           
             SceneLoader.Inst.manager.ClearLoadingTasks();
             SceneLoader.Inst.LoadSceneGroup("Menu");
             yield break;
         }
     

         yield return gm.StartCoroutine(gm.LoadBackgrounds(_level));
         yield return gm.StartCoroutine(gm.LoadObjects(_level));
         
         
         yield return gm.StartCoroutine(gm.LoadTweens());
         
         var comparision = DelegateSupport.ConvertDelegate<Il2CppSystem.Comparison<DataManager.GameData.BeatmapData.Checkpoint>>(
             new Comparison<DataManager.GameData.BeatmapData.Checkpoint>((x, y) => x.time.CompareTo(y.time)));
         
         DataManager.inst.gameData.beatmapData.checkpoints.Sort(comparision);
         
         if (VGPlayerManager.Inst.players.Count == 0)
         {
             VGPlayerManager.Inst.players.Add(new VGPlayerManager.VGPlayerData(){PlayerID = 0, ControllerID = 0});
         }
         gm.PlayGame();
    }

    [MultiRpc]
    public static void Multi_NextQueueLevel(ulong levelID, int seed)
    {
        PAM.Logger.LogInfo($"New random seed : {seed}");

        GlobalsManager.LevelId = levelID.ToString();
        SteamLobbyManager.Inst.RandSeed = seed;
        GlobalsManager.IsReloadingLobby = true;
        GlobalsManager.LobbyState = LobbyState.Lobby;
        GlobalsManager.HasLoadedLobbyInfo = true;
        
        DataManager.inst.StartCoroutine(NextQueueLevelIEnu(levelID, seed).WrapToIl2Cpp()); //task crashes game here 
    }

    static IEnumerator NextQueueLevelIEnu(ulong levelID, int seed)
    {
        SceneLoader.Inst.manager.ClearLoadingTasks();
        yield return new WaitForSeconds(1);
        
        VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.LevelId);
        if (level)
        {
            ArcadeManager.Inst.CurrentArcadeLevel = level;
            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            yield break;
        }

        GlobalsManager.IsDownloading = true;
        PAM.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }
    
    [MultiRpc]
    public static void Multi_OpenChallenge()
    {
        SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
        GlobalsManager.IsReloadingLobby = true;
        GlobalsManager.HasLoadedLobbyInfo = true;
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Challenge");
    }
    static void InitSteamInfo(ref VGLevel _level, PublishedFileId _id, string _folder, Item _item)
    {
        if (string.IsNullOrEmpty(_folder)) return;
   
        VGLevel.LevelDataBase data = new()
        {
            LevelID = _id.ToString(),
            LocalFolder = _folder
        };

        _level.LevelData = data;
        _level.BaseLevelData = data;
        _level.SteamInfo = new VGLevel.SteamData(){ ItemID = _id};
        
    }

    static async Task<Item> DownloadLevel()
    {
        void FailLoad(string errorMessage)
        {
            PAM.Logger.LogError(errorMessage);
            GlobalsManager.IsDownloading = false;
           
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.Inst.LoadSceneGroup("Menu");
        }
        
        if (!ulong.TryParse(GlobalsManager.LevelId, out var id))
        {
            FailLoad("Invalid level ID to download");
            return new Item();
        }
        
        var item = await SteamUGC.QueryFileAsync(id);
       
        if (!item.HasValue)
        {
            FailLoad("Level not found, is it deleted from the workshop?");
            return new Item();
        }

        var level = item.Value;
        
        if(level.ConsumerApp != 440310 || level.CreatorApp != 440310) 
        {
            FailLoad("Workshop item is not from PA");
            return new Item();
        }
        PAM.Logger.LogInfo($"Downloading [{level.Title}] created by [{level.Owner.Name}]");

       
        if (string.IsNullOrEmpty(level.Directory))
        {
            await level.Subscribe();
            
            for (int i = 0; i < 3; i++)
            {
                if (await level.DownloadAsync())
                {
                    break;
                }
                
                PAM.Logger.LogWarning($"Failed to to download level, retrying.. [{i+1}/3]");
                await Task.Delay(1000);
            }
        }

        if(!level.IsInstalled) 
        {
            FailLoad("Failed to download level");
            return new Item();
        }
        
        GlobalsManager.IsDownloading = false;
        
        return level;
    }
    
    //this is patched manually in Plugin.cs
    public static bool OverrideLoadGame(ref bool __result)
    {
        if (!GameManager.Inst.IsArcade || !GlobalsManager.IsMultiplayer)
            return true;
        
        GameManager.Inst.StartCoroutine(CustomLoadGame(ArcadeManager.Inst.CurrentArcadeLevel).WrapToIl2Cpp());
        
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(ObjectManager))]
public static class RNGSync
{
    public static int seed;

    [HarmonyPatch(nameof(ObjectManager.CreateObjectData))]
    [HarmonyPostfix]
    public static void test1(int _i,  ref ObjectHelpers.GameObjectRef __result)
    {
        Random.InitState(seed);
        __result.sequence.randomState = Random.state;
        seed = Random.RandomRangeInt(int.MinValue, int.MaxValue);
    }
    
}


public static class TaskExtension
{
    public static Il2CppSystem.Threading.Tasks.Task ToIl2Cpp(this Task task)
    {
        return Il2CppSystem.Threading.Tasks.Task.Run(new Action(task.Wait));
    }
}

