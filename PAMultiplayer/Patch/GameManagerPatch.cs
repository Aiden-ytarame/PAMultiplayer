using System;
using System.Collections;
using System.Threading;
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
using TaskStatus = Il2CppSystem.Threading.Tasks.TaskStatus;

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
      
        var test = SceneLoader.Inst;
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

        TaskData objectTask = new();
       foreach (var taskData in SceneLoader.Inst.manager.ExtraLoadingTasks)
        {
            if (taskData.Name == "Objects")
            {
                objectTask = taskData;
                break;
            }
        }
        
        //"loading client/Server" in the loading screen
        if (GlobalsManager.IsHosting)
        {
            var newTask = new TaskData
            {
                Name = "Loading Lobby",
                Task = Task.Run(async () =>
                {
                    //waiting for objects to load
                    while (objectTask.Task.Status != TaskStatus.RanToCompletion)
                    {
                        await Task.Delay(100);
                    }
                    
                    SteamLobbyManager.Inst.CreateLobby();
                    
                    var ct = new CancellationTokenSource();
                    var waitClient = Task.Run(async () =>
                    {
                        while (!SteamLobbyManager.Inst.InLobby)
                        {
                            ct.Token.ThrowIfCancellationRequested();
                            await Task.Delay(100, ct.Token);
                        }
                    }, ct.Token);

                    //if lobby isn't created in 10 seconds we close everything
                    if (waitClient != await Task.WhenAny(waitClient, Task.Delay(5000, ct.Token)))
                    {
                        ct.Cancel();
                        SteamManager.Inst.EndServer();
                        Plugin.Logger.LogError("Failed To Connect Lobby");
                    }
                    ct.Dispose();
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask);
        }
        else
        {
            
            var newTask = new TaskData
            {
                Name = "Loading Client",
                Task = Task.Run(async () =>
                {
                    //same as the first task
                    
                    while (objectTask.Task.Status != TaskStatus.RanToCompletion)
                    {
                        await Task.Delay(100);
                    }
                    
                    SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                    
                    var ct = new CancellationTokenSource();
                    var waitClient = Task.Run(async () =>
                    {
                        while (!SteamManager.Inst.Client.Connected)
                        {
                            ct.Token.ThrowIfCancellationRequested();
                            await Task.Delay(100, ct.Token);
                        }
                    }, ct.Token);

                    if (waitClient != await Task.WhenAny(waitClient, Task.Delay(5000, ct.Token)))
                    {
                        ct.Cancel();
                        SteamManager.Inst.EndClient();
                        Plugin.Logger.LogError("Failed To Connect Client");
                    }

                    ct.Dispose();
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask);
            
            
            //task for awaiting host send player ids
            var newTask2 = new TaskData
            {
                Name = "Awaiting Server Info",
                Task = Task.Run(async () =>
                {
                    //same as the first task
                    
                    while (objectTask.Task.Status != TaskStatus.RanToCompletion)
                    {
                        await Task.Delay(100);
                    }
                    //SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                    var ct = new CancellationTokenSource();
                    var waitClient = Task.Run(async () =>
                    {
                        while (!GlobalsManager.HasLoadedAllInfo)
                        {
                            ct.Token.ThrowIfCancellationRequested();
                            await Task.Delay(100, ct.Token);
                        }
                    }, ct.Token);

                    if (waitClient != await Task.WhenAny(waitClient, Task.Delay(10000, ct.Token)))
                    {
                        ct.Cancel();
                        SteamManager.Inst.EndClient();
                        Plugin.Logger.LogError("Failed To receive server info");
                    }
                  
                    
                    ct.Dispose();
                }).ToIl2Cpp()
            };
            SceneLoader.Inst.manager.AddToLoadingTasks(newTask2);
        }
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
            VGPlayerManager.Inst.players.Add(new VGPlayerManager.VGPlayerData(){PlayerID = 0, ControllerID = 0});
            
            GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayer, VGPlayerManager.Inst.players[0]);
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
    /// we add this to wait for loading screen to end
    /// the game doesn't do that by default
    /// </summary>
    static IEnumerator CustomLoadGame(VGLevel _level)
    {
         GameManager gm = GameManager.Inst;
      
         if (GlobalsManager.IsDownloading)
         {
             yield return new WaitUntil(new Func<bool>(() => !ArcadeManager.Inst.isLoading));
             VGLevel level;
             if (level = ArcadeLevelDataManager.Inst.GetSteamLevel(GlobalsManager.LevelId))
             {
                 _level = level;
             }
             else
             {
                 var item = DownloadLevel();
                
                 yield return new WaitUntil(new Func<bool>(() => !GlobalsManager.IsDownloading));
                 
                 var result = item.Result;
                 
                 VGLevel vgLevel = new VGLevel();
                 
                 vgLevel.InitArcadeData(result.Directory);
                 InitSteamInfo(ref vgLevel, result.Id, result.Directory, result);
                 ArcadeLevelDataManager.Inst.ArcadeLevels.Add(vgLevel);
                 
                 //yield return gm.StartCoroutine(SteamWorkshopFacepunch.inst.LoadAlbumArt(result.Id, result.Directory));
                 yield return gm.StartCoroutine(SteamWorkshopFacepunch.inst.LoadMusic(result.Id, result.Directory));

                 _level = vgLevel;
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
         yield return gm.StartCoroutine(gm.LoadTweens());

         gm.PauseDebounce = new Debounce();
         
         yield return new WaitUntil(new Func<bool>(() => !SceneLoader.Inst.isLoading));
         
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
        var newTask2 = new TaskData
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
        SceneLoader.Inst.manager.AddToLoadingTasks(newTask2);
        
        var item = await SteamUGC.QueryFileAsync(GlobalsManager.LevelId);

        if (!item.HasValue)
        {
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }

        var level = item.Value;
        
        if(level.ConsumerApp != 440310 || level.CreatorApp != 440310) 
        {
            SceneLoader.Inst.LoadSceneGroup("Menu");
            return new Item();
        }
            
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
        
        GameManager.Inst.StartCoroutine(CustomLoadGame(SaveManager.Inst.CurrentArcadeLevel).WrapToIl2Cpp());
        __result = false;
        return false;
    }
    
    
}

public static class TaskExtension
{
    public static Il2CppSystem.Threading.Tasks.Task ToIl2Cpp(this Task task)
    {
        return Il2CppSystem.Threading.Tasks.Task.Run((Il2CppSystem.Action)(() => task.Wait()));
    }
}