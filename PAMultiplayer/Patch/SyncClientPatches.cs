using System;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PAMultiplayer.Managers;
using UnityEngine;
namespace PAMultiplayer.Patch;


[HarmonyPatch(typeof(GameManager))]
public static class CheckpointHandler
{
    [HarmonyPatch(nameof(GameManager.CheckpointCheck))]
    [HarmonyPrefix]
    static bool CheckpointHit(ref GameManager __instance)
    {
        if (!StaticManager.IsMultiplayer) return true; //if single player run as normal

        if (!StaticManager.IsHosting) return false; //only host runs checkpoint logic.
        
        float songTime = __instance.CurrentSongTimeSmoothed;
        var activated = __instance.checkpointsActivated;
        if (__instance.checkpointsActivated.Length > 0 && DataManager.inst.gameData.beatmapData.checkpoints.Count > 0)
        {
            int countIndex = -1;
            int tmpIndex = DataManager.inst.gameData.beatmapData.checkpoints.FindIndex(new Predicate<DataManager.GameData.BeatmapData.Checkpoint>(x =>
            {
                countIndex++;

                if (x.time <= songTime && !activated[countIndex])
                    return true;
                
                return false;
            }).ToIL2CPP());

            if (tmpIndex != -1)
            {
                SteamManager.Inst.Server.SendCheckpointHit(tmpIndex);
            }
        }
        return false;
    }
}

[HarmonyPatch(typeof(VGPlayerManager))]
public static class RewindHandler
{
    [HarmonyPatch(nameof(VGPlayerManager.SpawnPlayers))]
    [HarmonyPrefix]
    static void ReplaceDeathAction(ref Il2CppSystem.Action<Vector3> _deathAction)
    {
        if (!StaticManager.IsMultiplayer) return;
        if (StaticManager.IsHosting)
        {
            _deathAction = new Action<Vector3>(x =>
            {
                SteamManager.Inst.Server.SendRewindToCheckpoint(SteamManager.Inst.Server.LatestCheckpoint);
            });
        }
        else
        {
            _deathAction = new Action<Vector3>(x =>
            {
                
            });
        }
    }
}

internal static class PredicateExtension
{
    //this is here so I dont have to call ConvertDelegate manually every time. will likely re-use this in other mods
    public static Il2CppSystem.Predicate<T> ToIL2CPP<T>(this System.Predicate<T> predicate)
    {
        return DelegateSupport.ConvertDelegate<Il2CppSystem.Predicate<T>>(predicate);
    }
}