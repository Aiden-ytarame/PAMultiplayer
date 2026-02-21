using HarmonyLib;
using Steamworks;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(SteamClient))]
public static class SteamPatch
{
    //otherwise it throws and nothing loads
    [HarmonyPatch(nameof(SteamClient.Init))]
    [HarmonyPrefix]
    private static bool PreInit()
    {
        return !SteamClient.IsValid;
    }
}