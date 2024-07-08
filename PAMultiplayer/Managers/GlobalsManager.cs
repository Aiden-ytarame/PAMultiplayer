using System.Collections.Generic;
using Steamworks;

namespace PAMultiplayer.Managers
{
    public static class GlobalsManager
    {
        public static SteamId LocalPlayer;
        public static int LocalPlayerObjectId;
        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        
        public static bool HasLoadedAllInfo;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false;
        public static bool IsReloadingLobby = false;
        
    }
}
