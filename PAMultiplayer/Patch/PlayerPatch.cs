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

        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static void Hit_Pre(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer || !__instance.IsLocalPlayer() || GameManager.Inst.Paused) return;
            
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
        
        [HarmonyPatch(nameof(VGPlayer.PlayParticles))]
        [HarmonyPostfix]
        static void BoostParticle_Post(ref VGPlayer __instance, VGPlayer.ParticleTypes _type)
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            if(_type != VGPlayer.ParticleTypes.Boost || !__instance.IsLocalPlayer()) return;

            if (GlobalsManager.IsHosting)
            {
                SteamManager.Inst.Server.SendHostBoost();
            }
            else
            {
                SteamManager.Inst.Client.SendBoost();
            }
        }

        
        //for changing the player's shape.
        //could change it to a hexagon so its less cursed
        public static Mesh CircleMesh;
        public static Mesh HexagonMesh;
        public static Mesh TriangleMesh;
        /// <summary>
        /// changes the nano's shape if there's more than 4 players
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.Init), new Type[] { })]
        [HarmonyPostfix]
        static void PostSpawn(ref VGPlayer __instance)
        {
            void SetPlayerMesh(VGPlayer player, Mesh mesh)
            {
                Transform playerWrapper = player.Player_Wrapper.transform;
                playerWrapper.Find("core").GetComponent<MeshFilter>().mesh = mesh;
                playerWrapper.Find("zen-marker").GetComponent<MeshFilter>().mesh = mesh; //is this needed?
                playerWrapper.Find("boost").GetComponent<MeshFilter>().mesh = mesh;
            }
            
            if (!GlobalsManager.IsMultiplayer) return;

            if (__instance.PlayerID < 4)
            {
                return;
            }

            if (__instance.PlayerID < 8)
            {
                SetPlayerMesh(__instance, CircleMesh);
            }
            else if (__instance.PlayerID < 12)
            {
                SetPlayerMesh(__instance, HexagonMesh);
            }
            else
            {
                SetPlayerMesh(__instance, TriangleMesh);
                
                Vector3 offsetRot = new Vector3(0, 0, -90);
                Transform player = __instance.Player_Wrapper.transform;
                
                player.Find("core").Rotate(offsetRot);
                player.Find("zen-marker").Rotate(offsetRot);
                player.transform.Find("boost").Rotate(offsetRot);
            }
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

[HarmonyPatch(typeof(DataManager.BeatmapTheme))]
public static class BeatmapThemePatch
{
    /// <summary>
    /// Normally every player after the 4th has the boost color of the 4th player, this is so instead it has the correct color.
    /// </summary>
    [HarmonyPatch(nameof(DataManager.BeatmapTheme.GetPlayerColor))]
    [HarmonyPrefix]
    static bool PreGetPlayerColor(DataManager.BeatmapTheme __instance, ref Color __result, int _val)
    {
        __result = __instance.playerColors[_val % 4];
        return false;
    }
}
static class PlayerIsLocalExtension
{
    public static bool IsLocalPlayer(this VGPlayer player)
    {
        return player.PlayerID == GlobalsManager.LocalPlayerObjectId;
    }
}