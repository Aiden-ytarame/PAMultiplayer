using HarmonyLib;
using Lachee.Discord;
using PAMultiplayer;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Patch;


[HarmonyPatch(typeof(DiscordManager))]
public static class DiscordManagerPatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPrefix]
    static void stopPresence(DiscordManager __instance)
    {
        __instance.gameObject.AddComponent<MultiplayerDiscordManager>();
        UnityEngine.Object.Destroy(__instance);
    }
    
}