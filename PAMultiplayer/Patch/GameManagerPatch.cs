using System;
using System.Collections;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using UnityEngine;
using VGFunctions;
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
        
        if (!GlobalsManager.IsMultiplayer) return;
        
        foreach (var mesh in Resources.FindObjectsOfTypeAll<Mesh>())
        {
            if (mesh.name == "circle")
            {
                Player_Patch.CircleMesh = mesh;
            }

            if (mesh.name == "hexagon")
            {
                Player_Patch.HexagonMesh = mesh;
            }
            if (mesh.name == "triangle")
            {
                Player_Patch.TriangleMesh = mesh;
            }
        }
        __instance.gameObject.AddComponent<NetworkManager>();

        //this is for waiting for the Objects to load before initialing the server/client

        if (GlobalsManager.IsHosting)
        {
            var newTask = new TaskData
            {
                Name = "Creating Lobby",
                Task = Task.Run(async () =>
                {
                    while (!SteamLobbyManager.Inst.InLobby)
                    {
                        await Task.Delay(100);
                    }
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask);
        }
        else
        {
            var newTask = new TaskData
            {
                Name = "Connecting to Server",
                Task = Task.Run(async () =>
                {
                    while (SteamManager.Inst.Client == null || !SteamManager.Inst.Client.Connected)
                    {
                        await Task.Delay(100);
                    }
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask);
            var newTask2 = new TaskData
            {
                Name = "Waiting for Server Info",
                Task = Task.Run(async () =>
                {
                    while (!GlobalsManager.HasLoadedAllInfo)
                    {
                        await Task.Delay(100);
                    }
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask2);
        }
    }

    private static bool paused;
    [HarmonyPatch(nameof(GameManager.Pause))]
    [HarmonyPrefix]
    static bool PrePause(ref GameManager __instance, bool _showUI)
    {
        if(!GlobalsManager.IsMultiplayer) return true;

        if (!GlobalsManager.HasStarted) return true;

        if (paused)
        {
            __instance.PauseMenuScript.ClosePauseMenu();
            __instance.SetUIVolumeWeight(0.25f);
            __instance.UnPause();
            return false;
        }
        if (_showUI)
        {
            __instance.PauseMenuScript.SetBGColor(__instance.LiveTheme.backgroundColor);
            __instance.SetUIVolumeWeight(1);
            __instance.PauseMenuScript.OpenPauseMenu();
            __instance.Paused = false;
            paused = true;
        }

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
                var item = await SteamUGC.QueryFileAsync(ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID);

                string levelCover = "null_level";
                if (item.HasValue && item.Value.Result == Result.OK)
                {
                   levelCover= item.Value.PreviewImageUrl;
                }
              
                MultiplayerDiscordManager.Instance.SetLevelPresence(state, $"{GameManager.Inst.TrackName} by {GameManager.Inst.ArtistName}", levelCover);
            }
        }
        if (!GlobalsManager.IsMultiplayer)
        {
            if (!MultiplayerDiscordManager.IsInitialized) return;
            
            if(GameManager.Inst.IsEditor)
            {
                MultiplayerDiscordManager.Instance.SetLevelPresence($"Editing {GameManager.Inst.TrackName}", "", "palogo");
            }
            else if(StoryManager.inst)
            {
                MultiplayerDiscordManager.Instance.SetLevelPresence("Playing Story Mode!", $"{GameManager.Inst.TrackName} by {GameManager.Inst.ArtistName}", "palogo");
            }
            else
            {
                setupLevelPresence("Playing Singleplayer!");
            }
            return;
        }
        
        __instance.Pause(false);
        __instance.Paused = true;
        __instance.gameObject.AddComponent<LobbyScreenManager>();
        
        VGPlayerManager.Inst.players.Clear();
        if (GlobalsManager.IsHosting)
        {
            SteamLobbyManager.Inst.CurrentLobby.SetJoinable(true);
            SteamLobbyManager.Inst.CurrentLobby.SetPublic();

            GlobalsManager.Queue.Remove(GlobalsManager.LevelId.ToString());
            if (GlobalsManager.Players.Count == 0)
            {
                //player 0 is never added, so we add it here
                var newData = new VGPlayerManager.VGPlayerData() { PlayerID = 0, ControllerID = 0 };
                VGPlayerManager.Inst.players.Add(newData);
                GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayer, newData);
            }
            else
            {
                foreach (var vgPlayerData in GlobalsManager.Players)
                {
                    VGPlayerManager.Inst.players.Add(vgPlayerData.Value);
                }
            }
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
        }
        else if (SteamManager.Inst.Client.Connected)
        {
            //if connected successfully
            //SteamManager.Inst.Client.SendLoaded();
            
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value);
            }
            VGPlayerManager.Inst.RespawnPlayers();
        }
        else
        {
            //if failed to connect to server
            SceneLoader.Inst.LoadSceneGroup("Menu");
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
         if (GlobalsManager.IsDownloading)
         {
             
             yield return new WaitUntil(new System.Func<bool>(() => !SteamWorkshopFacepunch.inst.isLoadingLevels));
             VGLevel level;
             if (level = ArcadeLevelDataManager.Inst.GetSteamLevel(GlobalsManager.LevelId))
             {
                 _level = level;
             }
             else
             {
                 //loading screen
                 //IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get());
                 var newTask = new TaskData
                 {
                     Name = "Downloading Level",
                     Task = Task.Run(async () =>
                     {
                         while (GlobalsManager.IsDownloading)
                         {
                             await Task.Delay(100);
                         }
                     }).ToIl2Cpp()
                 };
                 SceneLoader.Inst.manager.AddToLoadingTasks(newTask);
                 
                 var item = DownloadLevel();
                 
                 yield return new WaitUntil(new System.Func<bool>(() => !GlobalsManager.IsDownloading));
                 
                 var result = item.Result;
                 
                 VGLevel vgLevel = new VGLevel();
                 
                 vgLevel.InitArcadeData(result.Directory);
                 InitSteamInfo(ref vgLevel, result.Id, result.Directory, result);
                 ArcadeLevelDataManager.Inst.ArcadeLevels.Add(vgLevel);
                 
                 yield return gm.StartCoroutine(SteamWorkshopFacepunch.inst.LoadAlbumArt(result.Id, result.Directory));
                 yield return gm.StartCoroutine(SteamWorkshopFacepunch.inst.LoadMusic(result.Id, result.Directory));

                 _level = vgLevel; 
                 
                 ArcadeManager.Inst.CurrentArcadeLevel = vgLevel;
             }
         }
         gm.LoadMetadata(_level);
        
         
         yield return gm.StartCoroutine(gm.LoadAudio(_level));
         
         if (GlobalsManager.IsHosting)
         {
             SteamLobbyManager.Inst.RandSeed = Random.seed;
             SetSeed(Random.seed);
             
             if(GlobalsManager.IsReloadingLobby)
             {
                 SteamLobbyManager.Inst.CurrentLobby.SetData("LevelId", GlobalsManager.LevelId.ToString());
                 SteamLobbyManager.Inst.CurrentLobby.SetData("seed", SteamLobbyManager.Inst.RandSeed.ToString());
                 SteamManager.Inst.Server.SendNextQueueLevel(GlobalsManager.LevelId, SteamLobbyManager.Inst.RandSeed);
             }
         }
         else
         {
             SetSeed(SteamLobbyManager.Inst.RandSeed);
         }
         
         
         gm.LoadData(_level);
       
         
         yield return gm.StartCoroutine(gm.LoadBackgrounds(_level));
        
         yield return gm.StartCoroutine(gm.LoadObjects(_level));

         if (!GlobalsManager.IsReloadingLobby)
         {
             if (GlobalsManager.IsHosting)
             {
                 SteamLobbyManager.Inst.CreateLobby();
                 yield return new WaitUntil(new System.Func<bool>(() => SteamLobbyManager.Inst.InLobby));
             }
             else
             {
                 SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                 yield return new WaitUntil(new Func<bool>(() => SteamManager.Inst.Client.Connected));
                 yield return new WaitUntil(new Func<bool>(() => GlobalsManager.HasLoadedAllInfo ));
             }

         }
         
         yield return gm.StartCoroutine(gm.LoadTweens());
         
         GlobalsManager.IsReloadingLobby = false;
         gm.PauseDebounce = new Debounce();
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
        var item = await SteamUGC.QueryFileAsync(GlobalsManager.LevelId);

        if (!item.HasValue)
        {
            PAM.Logger.LogError("Level not found, is it deleted from the workshop?");
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }

        var level = item.Value;
        
        
        if(level.ConsumerApp != 440310 || level.CreatorApp != 440310) 
        {
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }
        PAM.Logger.LogInfo($"Downloading [{level.Title}] created by [{level.Owner.Name}]");
        await level.Subscribe();
        await level.DownloadAsync();
        GlobalsManager.IsDownloading = false;

        return level;
    }

    static void SetSeed(int _seed)
    {
        Random.InitState(_seed);
        ObjectManager.inst.seed = _seed;
        ObjectManager.inst.oldState = Random.state;
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

public static class TaskExtension
{
    public static Il2CppSystem.Threading.Tasks.Task ToIl2Cpp(this Task task)
    {
        return Il2CppSystem.Threading.Tasks.Task.Run(new Action(task.Wait));
    }
}