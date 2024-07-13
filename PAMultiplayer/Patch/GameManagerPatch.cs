using System;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using UnityEngine;
using TaskStatus = Il2CppSystem.Threading.Tasks.TaskStatus;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(GameManager))]
public class GameManagerPatch
{
    [HarmonyPatch(nameof(GameManager.SpawnPlayers))]
    [HarmonyPrefix]
    static void PreSpawn(ref GameManager __instance)
    {
        if (!GlobalsManager.IsMultiplayer) return;
        
        //this is a band-aid fix for the ghost nano
        var playerList = VGPlayerManager.Inst.players;
        
        if(playerList.Count > GlobalsManager.Players.Count)
            playerList.RemoveAt(  playerList.Count - 1);
        
        //this is to prevent a weird bug where the game freezes post rewind
        //pidge has acknowledged this bug.
        __instance.UnPause();
    }
 
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
        
        var netMan = new GameObject("Network");
        netMan.AddComponent<NetworkManager>();

        //this is for waiting for the Objects to load before initialing the server/client
        int index = 0;
        for (var i = 0; i < SceneLoader.Inst.manager.ExtraLoadingTasks.Count; i++)
        {
            if (SceneLoader.Inst.manager.ExtraLoadingTasks[i].Name == "Objects")
            {
                index = i;
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
                    while (SceneLoader.Inst.manager.ExtraLoadingTasks[index].Task.Status != TaskStatus.RanToCompletion)
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
                    if (waitClient != await Task.WhenAny(waitClient, Task.Delay(10000, ct.Token)))
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
                    
                    while (SceneLoader.Inst.manager.ExtraLoadingTasks[index].Task.Status != TaskStatus.RanToCompletion)
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
                    
                    while (SceneLoader.Inst.manager.ExtraLoadingTasks[index].Task.Status != TaskStatus.RanToCompletion)
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

                    if (waitClient != await Task.WhenAny(waitClient, Task.Delay(5000, ct.Token)))
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
    public static void Postfix(ref GameManager __instance)
    {
        if (!GlobalsManager.IsMultiplayer || GameManager.Inst.IsEditor) return;

        __instance.Pause(false);
        __instance.gameObject.AddComponent<LobbyScreenManager>();
        
        if (GlobalsManager.IsHosting)
        {
          //  VGPlayerManager.Inst.players[0].PlayerID = 0;
            GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayer, VGPlayerManager.Inst.players[0]);
            SteamManager.Inst.Server?.SendHostLoaded();
        }
        else if (!SteamManager.Inst.Client.Connected)
        {
            //if failed to connect
            SceneLoader.Inst.LoadSceneGroup("Menu");
        }
        else
        {
            //if connected successfully
            SteamManager.Inst.Client.SendLoaded();
       
            VGPlayerManager.Inst.players.Clear();
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value);
            }
            VGPlayerManager.Inst.RespawnPlayers();
        }
    }
}

public static class TaskExtension
{
    public static Il2CppSystem.Threading.Tasks.Task ToIl2Cpp(this Task task)
    {
        return Il2CppSystem.Threading.Tasks.Task.Run((Il2CppSystem.Action)(() => task.Wait()));
    }
}