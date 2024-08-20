using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppSystem;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using Steamworks;
using Steamworks.Data;
using Steamworks.Ugc;
using UnityEngine;
using VGFunctions;
using Action = System.Action;
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
                break;
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
    [HarmonyPatch(nameof(GameManager.PlayGame))]
    [HarmonyPostfix]
    static void PostPlay(ref GameManager __instance)
    {
        if (!GlobalsManager.IsMultiplayer || GameManager.Inst.IsEditor) return;
        
        __instance.Pause(false);
        __instance.gameObject.AddComponent<LobbyScreenManager>();
        
        VGPlayerManager.Inst.players.Clear();
        if (GlobalsManager.IsHosting)
        {
            SteamLobbyManager.Inst.CurrentLobby.SetJoinable(true);
            SteamLobbyManager.Inst.CurrentLobby.SetPublic();
           
            //player 0 is never added, so we add it here
            var newData = new VGPlayerManager.VGPlayerData() { PlayerID = 0, ControllerID = 0 };
            VGPlayerManager.Inst.players.Add(newData);
            GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayer, newData);
            
            SteamManager.Inst.Server?.SendHostLoaded();
        }
        else if (SteamManager.Inst.Client.Connected)
        {
            //if connected successfully
            SteamManager.Inst.Client.SendLoaded();
            
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
             yield return new WaitUntil(new System.Func<bool>(() => !ArcadeManager.Inst.isLoading));
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
         }
         else
         {
             int newSeed = int.Parse(SteamLobbyManager.Inst.CurrentLobby.GetData("seed"));
             SetSeed(newSeed);
         }
   
         gm.LoadData(_level);
       
         
         yield return gm.StartCoroutine(gm.LoadBackgrounds(_level));
        
         yield return gm.StartCoroutine(gm.LoadObjects(_level));
         
         
         if (GlobalsManager.IsHosting)
         {
             SteamLobbyManager.Inst.CreateLobby();
             yield return new WaitUntil(new System.Func<bool>(() => SteamLobbyManager.Inst.InLobby));
         }
         else
         {
             SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
             yield return new WaitUntil(new System.Func<bool>(() => SteamManager.Inst.Client.Connected));
             yield return new WaitUntil(new System.Func<bool>(() => GlobalsManager.HasLoadedAllInfo ));
         }
         
        
         yield return gm.StartCoroutine(gm.LoadTweens());
         
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
            Plugin.Logger.LogError("Level not found, is it deleted from the workshop?");
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }

        var level = item.Value;
        
        
        if(level.ConsumerApp != 440310 || level.CreatorApp != 440310) 
        {
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }
        Plugin.Logger.LogInfo($"Downloading [{level.Title}] created by [{level.Owner.Name}]");
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
    
    //attempting to fix rewind bug
    //patched manually in Plugin.cs
    public static void PostRewind(ref GameManager __instance, bool __result)
    {
        //if rewind ended and playback time is not normal
        if (!__result && AudioManager.Inst.AudioPlaybackSpeed < GameManager.Inst.GetSongSpeed)
        {
            Plugin.Logger.LogError($"Weird Audio Playback Speed : [{AudioManager.Inst.AudioPlaybackSpeed}], Defaulting to Song Speed.");
            AudioManager.Inst.AudioPlaybackSpeed = GameManager.Inst.GetSongSpeed;
        }
    }
}

public static class TaskExtension
{
    public static Il2CppSystem.Threading.Tasks.Task ToIl2Cpp(this Task task)
    {
        return Il2CppSystem.Threading.Tasks.Task.Run(new Action(task.Wait));
    }
}