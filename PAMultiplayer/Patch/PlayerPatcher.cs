using System;
using HarmonyLib;
using PAMultiplayer.Managers;
using Rewired;
using UnityEngine;

namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_Patch
    {
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerEnter))]
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerStay))]
        [HarmonyPrefix]
        static bool PreCollision(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            if (__instance.IsLocalPlayer())
                return true;
            return false; //only collide if is local player
        }

        /// <summary>
        /// sends position to other players
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPostfix]
        static void PostUpdate(ref VGPlayer __instance)
        {
            //this could be moved out of here so it doesn't run for every player.
            if (!GlobalsManager.IsMultiplayer || !__instance.IsLocalPlayer()) return;

            return;
            if (__instance.Player_Rigidbody)
            {
                var V2 = __instance.Player_Rigidbody.transform.position;
                if (GlobalsManager.IsHosting)
                    SteamManager.Inst.Server?.SendHostPosition(V2);
                else
                    SteamManager.Inst.Client?.SendPosition(V2);
            }
        }

        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static void Hit_Pre(ref VGPlayer __instance)
        {

            if (!GlobalsManager.IsMultiplayer || !__instance.IsLocalPlayer()) return;

            if (GlobalsManager.IsHosting)
                SteamManager.Inst.Server.SendHostDamage();
            else
                SteamManager.Inst.Client.SendDamage();

        }

        /// <summary>
        /// this gives a null exception if ran without a valid playerId
        /// or that's my guess lol, whatever it is this fixes it
        /// </summary>
      
        [HarmonyPatch(nameof(VGPlayer.HandleBoost))]
        [HarmonyPrefix]
        static bool Boost_Pre(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            return __instance.IsLocalPlayer();
        }
        
        
        //for changing the player's shape.
        //could change it to a hexagon so its less cursed
        public static Mesh CircleMesh;
        
        /// <summary>
        /// changes the nano's shape if there's more than 4 players
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.Init), new Type[] { })]
        [HarmonyPostfix]
        static void PostSpawn(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return;

            if (__instance.PlayerID < 4) return;

            __instance.Player_Wrapper.transform.Find("core").GetComponent<MeshFilter>().mesh = CircleMesh;
            __instance.Player_Wrapper.transform.Find("zen-marker").GetComponent<MeshFilter>().mesh = CircleMesh; //is this needed?
            __instance.Player_Wrapper.transform.Find("boost").GetComponent<MeshFilter>().mesh = CircleMesh;
        }

        /// <summary>
        /// this returns the player controller depending on your playerId
        /// the controller 0 is the one LocalPlayer controls
        /// we force this to return controller 0 if the playerId is the one the client is supposed to control
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.RPlayer), MethodType.Getter)]
        [HarmonyPrefix]
        static bool RPlayerGetter(ref VGPlayer __instance, ref Rewired.Player __result)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            if (__instance.IsLocalPlayer())
            {
                __result = ReInput.players.GetPlayer(0);
            }
            else
            {
                __result = ReInput.players.GetPlayer(1);
            }

            return false;
        }

    }
}

static class PlayerIsLocalExtension
{
    public static bool IsLocalPlayer(this VGPlayer player)
    {
        return player.PlayerID == GlobalsManager.LocalPlayerObjectId;
    }
}