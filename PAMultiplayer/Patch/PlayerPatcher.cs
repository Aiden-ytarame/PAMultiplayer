
using System;
using HarmonyLib;
using UnityEngine;


namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_UpdatePatch
    {

        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static bool Hit_Pre(ref VGPlayer __instance)
        {
            if (!__instance.CanTakeDamage) //weird, I thought this was checked in the function!
                return false;

            if (__instance.PlayerID == 0) 
            {
                if (StaticManager.IsMultiplayer)
                {
                    StaticManager.Client.SendDamage();
                }
                return true;
            }
            if (StaticManager.DamageQueue.Contains(__instance.PlayerID))
            {
                StaticManager.DamageQueue.Remove(__instance.PlayerID);
                return true;
            }
            return false;

        }


        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPrefix]
        static void Update_Pre(ref VGPlayer __instance)
        {   
            if (!StaticManager.IsMultiplayer)
            {
                return;
            }
           
            /*
            if (StaticManager.SpawnPending)
            {
                StaticManager.SpawnPending = false;
                Plugin.Instance.Log.LogError(VGPlayerManager.Inst.players.Count);
                VGPlayerManager.Inst.RespawnPlayers();
                GameManager.Inst.RewindToCheckpoint(0, true);
            }

            if (StaticManager.DamageQueue.Contains(__instance.PlayerID))
            {
                Plugin.Instance.Log.LogWarning("CONTAINS");
                __instance.PlayerHit();
            }
            */
        

            if (StaticManager.Players == null)
                return;

            if (StaticManager.Players.ContainsKey(StaticManager.LocalPlayer))
            {
                if (__instance.PlayerID == 0)
                {
                    if (__instance.Player_Rigidbody)
                    {
                        if (!StaticManager.Players[StaticManager.LocalPlayer].PlayerObject)
                            StaticManager.Players[StaticManager.LocalPlayer].PlayerObject = __instance;

                        var V2 = __instance.Player_Rigidbody.transform.position;
                        StaticManager.Client.SendPosition(V2.x, V2.y);
                    }

                }
            }
            else
            {
                if (__instance.PlayerID == 0)
                    StaticManager.Players.Add(StaticManager.LocalPlayer, VGPlayerManager.Inst.players[0]);
            }

        }

    }
    [HarmonyPatch(typeof(GameManager))]
    public class AddNetManager
    {
        [HarmonyPatch(nameof(GameManager.Start))]
        [HarmonyPostfix]
        static void PostStart(ref GameManager __instance)
        {
            if (__instance.IsEditor)
                return;

            var netMan = new GameObject("Network");
            netMan.AddComponent<NetworkManager>();
        }
    }
}
