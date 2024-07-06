using System.Collections.Generic;
using Steamworks;

namespace PAMultiplayer.Managers
{
    //I could just move these of here
    public static class StaticManager
    {
        public static SteamId LocalPlayer;

        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        public static bool HasLoadedAllInfo;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false;
        public static bool IsReloadingLobby = false;
    }
}
