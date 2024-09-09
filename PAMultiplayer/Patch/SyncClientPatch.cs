using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PAMultiplayer.Managers;
using UnityEngine;

namespace PAMultiplayer.Patch;


/// <summary>
/// takes care of checkpoint for server only
/// </summary>
[HarmonyPatch(typeof(GameManager))]
public static class CheckpointHandler
{
    [HarmonyPatch(nameof(GameManager.CheckpointCheck))]
    [HarmonyPrefix]
    static bool CheckpointHit(ref GameManager __instance)
    {
        if (!GlobalsManager.IsMultiplayer) return true; //if single player run as normal

        if (!GlobalsManager.IsHosting) return false; //only host runs checkpoint logic.
        
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

/// <summary>
/// takes care of rewinding to the correct checkpoint
/// </summary>
[HarmonyPatch(typeof(VGPlayerManager))]
public static class RewindHandler
{
    [HarmonyPatch(nameof(VGPlayerManager.SpawnPlayers))]
    [HarmonyPrefix]
    static void ReplaceDeathAction(ref Il2CppSystem.Action<Vector3> _deathAction)
    {
        
        if (!GlobalsManager.IsMultiplayer) return;
        
        if (GlobalsManager.IsHosting)
        {
            _deathAction = new Action<Vector3>(x =>
            {
                //if any player is alive don't rewind
                foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                {
                   if(vgPlayerData.PlayerObject) //if player object exists
                       if (!vgPlayerData.PlayerObject.isDead) //if not dead
                           return; //dont rewind
                }
                SteamManager.Inst.Server?.SendRewindToCheckpoint();
            });
        }
        else
        {
            _deathAction = new Action<Vector3>(x =>
            {
                //clients do nothing on death, just wait for the server message.
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

