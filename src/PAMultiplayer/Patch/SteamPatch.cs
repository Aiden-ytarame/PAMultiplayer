using HarmonyLib;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers;
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


[HarmonyPatch(typeof(SteamWrapper))]
public static class SteamWrapperPatch
{
    [HarmonyPatch(nameof(SteamWrapper.SubmitArcadeLeaderboardScore))]
    [HarmonyPrefix]
    static bool PreSubmitArcadeLeaderboardScore()
    {
        if (!GlobalsManager.IsMultiplayer)
        {
            return true;
        }

        if (PaMNetworkManager.PamInstance?.LobbyInfo.JoinedMidLevel == true)
        {
            PaMNetworkManager.PamInstance?.LobbyInfo.JoinedMidLevel = false;
            return false;
        }

        /*if (PointsManager.Inst)
        {
            PointsManager.PlayerRank rank = PointsManager.Inst.GetLocalRank();
            _score = rank.Score;
            if (_scores.Length == 3)
            {
                _scores[0] = rank.Hits;
                _scores[1] = rank.Cc;
                _scores[2] = rank.Boosts;
            }
        }*/
        
        return true;
    }
}