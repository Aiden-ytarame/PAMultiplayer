using System.Collections.Generic;
using Steamworks;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// Holds global variables like Local player steamId and Player list
    /// </summary>
    public static class GlobalsManager
    {
        public static VGPlayer LocalPlayerObj => Players[LocalPlayerId]?.PlayerObject;
        public static SteamId LocalPlayerId;
        public static int LocalPlayerObjectId;
        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        public static List<string> Queue = new();
        
        public static ulong LevelId;
        public static bool IsMultiplayer = false;
        public static bool IsHosting = false;
        
        public static bool HasLoadedAllInfo => HasLoadedExternalInfo && HasLoadedBasePlayerIds;

        public static bool HasLoadedExternalInfo;
        public static bool HasLoadedBasePlayerIds;
        
        public static bool IsReloadingLobby = false;
        public static bool HasStarted = false;
        public static bool IsDownloading = false;
    }
}
