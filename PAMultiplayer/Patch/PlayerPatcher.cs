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
        
        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPrefix]
        static void Update_Pre(ref VGPlayer __instance)
        {
            if (!StaticManager.IsMultiplayer || __instance.PlayerID != StaticManager.LocalPlayerId) return;
            
            if (StaticManager.Players.TryGetValue(StaticManager.LocalPlayer, out var player))
            {
                if (__instance.Player_Rigidbody)
                {
                    var V2 = __instance.Player_Rigidbody.transform.position;
                    if(StaticManager.IsHosting)
                        SteamManager.Inst.Server?.SendHostPosition(V2);
                    else
                        SteamManager.Inst.Client?.SendPosition(V2);
                }
            }
            else
            {
                StaticManager.Players.Add(StaticManager.LocalPlayer, VGPlayerManager.Inst.players[0]);
            }
        }

    }
}
