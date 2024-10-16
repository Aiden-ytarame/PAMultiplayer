using HarmonyLib;
using Lachee.Discord;

namespace PAMultiplayer.Patch;


[HarmonyPatch(typeof(DiscordManager))]
public static class DiscordManagerPatch
{
    [HarmonyPatch(nameof(DiscordManager.Awake))]
    [HarmonyPrefix]
    static void stopPresence(DiscordManager __instance)
    {
        __instance.gameObject.AddComponent<MultiplayerDiscordManager>();
        UnityEngine.Object.Destroy(__instance);
    }
    
}