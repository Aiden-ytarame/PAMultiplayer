using System.Collections.Generic;
using JetBrains.Annotations;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    //I could just move these of here
    public static class StaticManager
    {
        public static SteamId LocalPlayer;
        public static int LocalPlayerId;
       
        public static readonly Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
      //  public static readonly Dictionary<SteamId, Vector2> PlayerPositions = new();
        public static bool HasLoadedAllInfo;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false;
        public static bool IsReloadingLobby = false;
    }
}
