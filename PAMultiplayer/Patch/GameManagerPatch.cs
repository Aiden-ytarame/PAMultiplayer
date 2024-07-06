using System;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using Steamworks;
using UnityEngine;
using UnityEngine.Playables;
using TaskStatus = Il2CppSystem.Threading.Tasks.TaskStatus;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(GameManager))]
public class GameManagerPatch
{
    [HarmonyPatch(nameof(GameManager.Start))]
    [HarmonyPostfix]
    static void PostStart(ref GameManager __instance)
    {
        if (__instance.IsEditor)
            return;
        
        if (!StaticManager.IsMultiplayer) return;

        var netMan = new GameObject("Network");
        netMan.AddComponent<NetworkManager>();

        
        
        //"loading client/Server" in the loading screen
        if (StaticManager.IsHosting)
        {
            var newTask = new TaskData
            {
                Name = "Loading Lobby",
                Task = Task.Run(async () =>
                {
                    var ct = new CancellationTokenSource();
                    var waitClient = Task.Run(async () =>
                    {
                        while (!SteamLobbyManager.Inst.InLobby)
                        {
                            ct.Token.ThrowIfCancellationRequested();
                            await Task.Delay(100, ct.Token);
                        }
                    }, ct.Token);

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
            int index = 0;
            for (var i = 0; i < SceneLoader.Inst.manager.ExtraLoadingTasks.Count; i++)
            {
                if (SceneLoader.Inst.manager.ExtraLoadingTasks[i].Name == "Objects")
                {
                    index = i;
                    break;
                }
            }
            var newTask = new TaskData
            {
                Name = "Loading Client",
                Task = Task.Run(async () =>
                {
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
                    while (SceneLoader.Inst.manager.ExtraLoadingTasks[index].Task.Status != TaskStatus.RanToCompletion)
                    {
                        await Task.Delay(100);
                    }
                    //SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                    var ct = new CancellationTokenSource();
                    var waitClient = Task.Run(async () =>
                    {
                        while (!StaticManager.HasLoadedAllInfo)
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
        if (StaticManager.IsMultiplayer && !GameManager.Inst.IsEditor)
        {
            SteamManager.Inst.Client?.SendLoaded();
            __instance.Pause(false);
            __instance.gameObject.AddComponent<LobbyManager>();
        
            if (StaticManager.IsHosting)
            {
                SteamManager.Inst.Server?.SendHostLoaded();
            }
            else if (!SteamManager.Inst.Client.Connected)
            {
                SceneLoader.Inst.LoadSceneGroup("Menu");
            }
            else
            {
                SteamManager.Inst.Client.SendLoaded();
            }
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