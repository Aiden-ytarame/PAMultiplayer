using System.Collections.Generic;
using Steamworks;

namespace PAMultiplayer.Managers
{
    public struct PlayerData(VGPlayerManager.VGPlayerData vgPlayerData, string name)
    {
        public string Name = name;
        public VGPlayerManager.VGPlayerData VGPlayerData = vgPlayerData;

        public void SetName(string name)
        {
            Name = name;
        }
    }
    /// <summary>
    /// Holds global variables like Local player steamId and Player list
    /// </summary>
    public static class GlobalsManager
    {
        public static VGPlayer LocalPlayerObj => Players[LocalPlayerId].VGPlayerData?.PlayerObject;
        public static SteamId LocalPlayerId;
        public static int LocalPlayerObjectId;
        public static readonly Dictionary<SteamId, PlayerData> Players = new();
        
        public static List<string> Queue = new();
        
        public static string LevelId;
        public static bool IsMultiplayer = false;
        public static bool IsHosting = false;
        
        public static bool HasLoadedAllInfo => HasLoadedExternalInfo && HasLoadedBasePlayerIds;

        public static bool HasLoadedExternalInfo;
        public static bool HasLoadedBasePlayerIds;
        
        public static bool IsReloadingLobby = false;
        public static bool HasStarted = false;
        public static bool IsDownloading = false;
        public static bool IsChallenge = false;
    }
}
