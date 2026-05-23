using System.Threading.Tasks;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;

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
