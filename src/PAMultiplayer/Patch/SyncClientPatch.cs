using System;
using HarmonyLib;
using AttributeNetworkWrapperV2;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers;
using UnityEngine;

namespace PAMultiplayer.Patch;

/// <summary>
/// takes care of checkpoint for server only
/// </summary>
[HarmonyPatch(typeof(GameManager))]
public static partial class CheckpointHandler
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
                CallRpc_Multi_CheckpointHit(tmpIndex);
            }
        }
        return false;
    }

    [MultiRpc]
    private static void Multi_CheckpointHit(int index)
    {
        if (!GameManager.Inst || index <= GameManager.Inst.currentCheckpointIndex)
        {
            return;
        }
        
        PAM.Logger.LogInfo($"Checkpoint [{index}] Received");
        
        GameManager.Inst.playingCheckpointAnimation = true;
   
        Vector3 pos = Vector3.zero;
        if (index >= 0 && index < DataManager.inst.gameData?.beatmapData?.checkpoints.Count)
        {
            pos = DataManager.inst.gameData.beatmapData.checkpoints[index].pos;
        }
        VGPlayerManager.Inst.EnqueueRespawn(pos);
        
        VGPlayerManager.Inst.HealPlayers();
        GameManager.Inst.currentCheckpointIndex = index;
      
        GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation(index));
        
        for (var i = 0; i < GlobalsManager.HitsQueue.Count; i++)
        {
            HitInfo hitInfo = GlobalsManager.HitsQueue[i];

            if (hitInfo.Checkpoint != index)
            {
                continue;
            }
            
            if (hitInfo.All)
            {
                PlayerPatch.DamageAll(hitInfo.Health, hitInfo.Checkpoint, hitInfo.Id);
            }
            else
            {
                PlayerPatch.Multi_PlayerDamaged(hitInfo.Id, hitInfo.Health, hitInfo.Checkpoint);
            }
            
            GlobalsManager.HitsQueue.RemoveAt(i);
        }
    }
}

/// <summary>
/// takes care of rewinding to the correct checkpoint
/// </summary>
[HarmonyPatch(typeof(VGPlayerManager))]
public static partial class RewindHandler
{
    [HarmonyPatch(nameof(VGPlayerManager.SpawnPlayers))]
    [HarmonyPrefix]
    static void ReplaceDeathAction(ref Action<Vector3, int, Vector2> _deathAction)
    {
        if (!GlobalsManager.IsMultiplayer) return;
        //TODO: add last stand and hot swap if added to arcade
        if (GlobalsManager.IsHosting)
        {
            _deathAction = (x, _, _) =>
            {
                if (!GameManager.Inst.AreAllPlayersDead())
                {
                    return;
                }

                int index = 0;
        
                if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) <= 1)
                {
                    index = GameManager.Inst.currentCheckpointIndex;
                }
                
                CallRpc_Multi_RewindToCheckpoint(index, PaMNetworkManager.PamInstance?.LobbyInfo.RewindCounter + 1 ?? 0);
            };
        }
        else
        {
            _deathAction = (x, _, _) =>
            {
                //clients do nothing on death, just wait for the server message.
            };
        }
    }

    [MultiRpc]
    public static void Multi_RewindToCheckpoint(int index, uint counter)
    {
        if (PaMNetworkManager.PamInstance?.LobbyInfo.HasLoadedAllLobbyInfo != true)
        {
            return;
        }

        GlobalsManager.HitsQueue.Clear();
        
        PAM.Logger.LogInfo($"Rewind to Checkpoint [{index}] Received");
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
        PaMNetworkManager.PamInstance?.LobbyInfo.RewindCounter = counter;
    }
}

