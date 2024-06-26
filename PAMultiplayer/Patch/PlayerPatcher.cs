
using System;
using HarmonyLib;
using PAMultiplayer.Managers;
using Steamworks;
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
            if (__instance.PlayerID == 0 && __instance.CanTakeDamage) 
            {
                if (StaticManager.IsMultiplayer)
                {
                    SteamManager.Inst.Client.SendDamage();
                }
                return true;
            }
            if (StaticManager.DamageQueue.Count != 0 && StaticManager.DamageQueue.Contains(__instance.PlayerID))
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
            if (!StaticManager.IsMultiplayer) return;
            
        

            if (StaticManager.Players == null)
                return;

            if (StaticManager.Players.TryGetValue(StaticManager.LocalPlayer, out var player))
            {
                if (__instance.PlayerID == 0)
                {
                    if (__instance.Player_Rigidbody)
                    {
                        if (!player.PlayerObject)
                            player.PlayerObject = __instance;

                        var V2 = __instance.Player_Rigidbody.transform.position;
                        SteamManager.Inst.Client.SendPosition(V2);
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

            if (!StaticManager.IsMultiplayer) return;
            
            var netMan = new GameObject("Network");
            netMan.AddComponent<NetworkManager>();
        }
    }
}
