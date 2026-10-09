using HarmonyLib;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(NetSession))]
internal static class NetSessionPatch
{
    [HarmonyPatch(nameof(NetSession.Update))]
    [HarmonyPrefix]
    static bool PreUpdate()
    {
        return false;
    }
}