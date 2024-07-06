using HarmonyLib;
using PAMultiplayer;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_UpdatePatch
    {
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerEnter))]
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerStay))]
        [HarmonyPrefix]
        static bool PreCollision(ref VGPlayer __instance)
        {
            if (StaticManager.IsMultiplayer && __instance.PlayerID != StaticManager.LocalPlayerId)
                return false;
            
            return true; //only collide if is local player
        }

        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static void Hit_Pre(ref VGPlayer __instance)
        {
            if (StaticManager.IsMultiplayer && __instance.PlayerID == StaticManager.LocalPlayerId) 
            {
                if(StaticManager.IsHosting)
                    SteamManager.Inst.Server.SendHostDamage();
                else 
                    SteamManager.Inst.Client.SendDamage();
                
            }
        }

        [HarmonyPatch(nameof(VGPlayer.HandleBoost))]
        [HarmonyPrefix]
        static bool Boost_Pre(ref VGPlayer __instance)
        {
            if (!StaticManager.IsMultiplayer) return true;
            if (__instance.IsLocalPlayer()) return false;
            return true;
            if (__instance.IsLocalPlayer()) return true;

            return false;
        }
    }
}


static class PlayerIsLocalExtension
{
    public static bool IsLocalPlayer(this VGPlayer player)
    {
        return VGPlayerManager.Inst.players[0].PlayerID == player.PlayerID;
    }
}