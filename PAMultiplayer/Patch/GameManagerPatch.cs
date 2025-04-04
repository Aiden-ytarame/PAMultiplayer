using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystems.SceneManagement;
using Newtonsoft.Json;
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
                    SteamManager.Inst.Server.SendOpenChallenge();
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
                while (SteamManager.Inst.Client == null || !SteamManager.Inst.Client.Connected)
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
        else if (SteamManager.Inst.Client.Connected)
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
                SteamManager.Inst.Client.SendRequestLobbyState();
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
                 yield return new WaitUntil(new Func<bool>(() => SteamManager.Inst.Client.Connected));
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
                     PAM.Logger.LogError(SteamLobbyManager.Inst.RandSeed);

                     if (ulong.TryParse(GlobalsManager.LevelId, out var nextQueueId))
                     {
                         SteamManager.Inst.Server.SendNextQueueLevel(nextQueueId, SteamLobbyManager.Inst.RandSeed);
                     }
                     else
                     {
                         PAM.Logger.LogError(
                             "tried sending Local Level with non numbered name, name too big or negative value. Skipping.");

                         GlobalsManager.Queue.Remove(GlobalsManager.LevelId);
                         string id = GlobalsManager.Queue[0];
                         ArcadeManager.Inst.CurrentArcadeLevel = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
                         GlobalsManager.LevelId = id;

                         GlobalsManager.IsReloadingLobby = true;
                         SteamLobbyManager.Inst.UnloadAll();

                         SceneLoader.Inst.manager.ClearLoadingTasks();
                         SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                         yield break;
                     }
                 }
                 else
                 {
                     PAM.Logger.LogError(SteamLobbyManager.Inst.RandSeed);
                     if (ulong.TryParse(GlobalsManager.LevelId, out var nextQueueId))
                     {
                         SteamManager.Inst.Server.SendNextQueueLevel(nextQueueId, SteamLobbyManager.Inst.RandSeed);
                     }
                     else
                     {
                         PAM.Logger.LogError(
                             "tried sending Local Level with non numbered name, name too big or negative value. Skipping.");
                         
                         GlobalsManager.IsReloadingLobby = true;
                         SteamLobbyManager.Inst.UnloadAll();
                         SceneLoader.Inst.manager.ClearLoadingTasks();
                         SceneLoader.Inst.LoadSceneGroup("Challenge");
                         yield break;
                     }
                 }
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
            await level.DownloadAsync();
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

