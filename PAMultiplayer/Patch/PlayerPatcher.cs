using HarmonyLib;
using PAMultiplayer.Managers;
using Rewired;

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
            if (!GlobalsManager.IsMultiplayer) return true;

            if (__instance.IsLocalPlayer())
                return true;
            
            return false; //only collide if is local player
        }
        
        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPostfix]
        static void PostUpdate(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer || __instance.PlayerID != GlobalsManager.LocalPlayerObjectId) return;
        
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

            if (GlobalsManager.IsMultiplayer && __instance.IsLocalPlayer()) 
            {
                if(GlobalsManager.IsHosting)
                    SteamManager.Inst.Server.SendHostDamage();
                else 
                    SteamManager.Inst.Client.SendDamage();
            }
        }

        [HarmonyPatch(nameof(VGPlayer.HandleBoost))]
        [HarmonyPrefix]
        static bool Boost_Pre(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            return __instance.IsLocalPlayer();
        }
        
        [HarmonyPatch(nameof(VGPlayer.RPlayer), MethodType.Getter)]
        [HarmonyPostfix]
        static void RPlayerGetter(ref VGPlayer __instance, ref Rewired.Player __result)
        {
            if (!GlobalsManager.IsMultiplayer) return;

            if (__instance.PlayerID == GlobalsManager.LocalPlayerObjectId)
            {
                __result = ReInput.players.GetPlayer(0);
            }
            else
            {
                __result = ReInput.players.GetPlayer(1);
            }
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