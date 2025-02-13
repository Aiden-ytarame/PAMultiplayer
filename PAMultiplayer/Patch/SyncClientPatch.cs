using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PAMultiplayer.Managers;
using UnityEngine;
using Random = UnityEngine.Random;

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

        if (!GlobalsManager.IsHosting || DataManager.inst.gameData.beatmapData == null) return false; //only host runs checkpoint logic.
        
        float songTime = __instance.CurrentSongTimeSmoothed;
     
        if (DataManager.inst.gameData.beatmapData.checkpoints.Count > 0)
        {
            int tmpIndex = -1;
            
            for (var i = 0; i < DataManager.inst.gameData.beatmapData.checkpoints.Count; i++)
            {
                if(i <= __instance.currentCheckpointIndex)
                    continue;
                
                if (DataManager.inst.gameData.beatmapData.checkpoints[i].time <= songTime)
                {
                    tmpIndex = i;
                    break;
                }
            }
            
            if (tmpIndex != -1)
            {
                __instance.currentCheckpointIndex = tmpIndex;
                SteamManager.Inst.Server.SendCheckpointHit(tmpIndex);
                
                GameManager.Inst.playingCheckpointAnimation = true;
                VGPlayerManager.Inst.RespawnPlayers();
                VGPlayerManager.Inst.HealPlayers();

                GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation(tmpIndex));
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
                   if(vgPlayerData.PlayerObject.IsValidPlayer()) //if player object exist
                       return; //dont rewind
                }
                
                int index = 0;
        
                if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) <= 1)
                {
                    index = GameManager.Inst.currentCheckpointIndex;
                }
                
                SteamManager.Inst.Server?.SendRewindToCheckpoint(index);
                
                foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                {
                    if (vgPlayerData.PlayerObject.IsValidPlayer())
                    {
                        vgPlayerData.PlayerObject.Health = 0;
                        vgPlayerData.PlayerObject.ClearEvents();
                        vgPlayerData.PlayerObject.PlayerDeath();
                    }
                }
                GameManager.Inst.RewindToCheckpoint(index);
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

