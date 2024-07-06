using HarmonyLib;
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
            if (StaticManager.IsMultiplayer && __instance.IsLocalPlayer())
                return true;
            
            return false; //only collide if is local player
        }

        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static void Hit_Pre(ref VGPlayer __instance)
        {
            if (StaticManager.IsMultiplayer && __instance.IsLocalPlayer()) 
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

            return __instance.IsLocalPlayer();
        }
    }
}


static class PlayerIsLocalExtension
{
    public static bool IsLocalPlayer(this VGPlayer player)
    {
        return VGPlayerManager.Inst.players[0].PlayerObject.Equals(player);
    }
}