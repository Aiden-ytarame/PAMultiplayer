using System.Collections.Generic;
using Steamworks;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// Holds global variables like Local player steamId and Player list
    /// </summary>
    public static class GlobalsManager
    {
        public static SteamId LocalPlayer;
        public static int LocalPlayerObjectId;
        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        
        public static bool HasLoadedAllInfo;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false;
        public static bool IsReloadingLobby = false;
        public static bool HasStarted = false;
        public static ulong LevelId;
        public static bool IsDownloading = false;
    }
}
