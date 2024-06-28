using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    //I could just move these of here
    public static class StaticManager
    {
        public static SteamId LocalPlayer;
        public static SteamId HostId;
        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        public static readonly Dictionary<SteamId, Vector2> PlayerPositions = new();
        
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false;
        public static bool IsReloadingLobby = false;
    }
}
