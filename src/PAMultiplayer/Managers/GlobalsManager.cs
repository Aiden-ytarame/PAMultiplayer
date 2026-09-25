using System;
using System.Collections.Generic;
using PAMultiplayer.Data;
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

    public struct HitInfo(ulong id, int health, int checkpoint, bool all = false)
    {
        public ulong Id = id;
        public bool All = all;
        public int Health = health;
        public int Checkpoint = checkpoint;
    }
    
    /// <summary>
    /// Holds global variables like Local player steamId and Player list
    /// This class should not exist, but refactoring would take a lot of my time and I gain nothing from it
    /// </summary>
    public static class GlobalsManager
    {
        public static VGPlayer LocalPlayerObj => Players[LocalPlayerId].VGPlayerData?.PlayerObject;
        public static SteamId LocalPlayerId;
        public static int LocalPlayerObjectId;
        public static readonly Dictionary<ulong, PlayerData> Players = new();
        public static readonly Dictionary<int, SteamId> ConnIdToSteamId = new();
  
        public static readonly LevelQueue Queue = new();
        
        public static string LevelId;
        public static bool IsMultiplayer = false;
        public static bool IsHosting = false;
        public static bool IsChallenge = false;
        public static bool IsDownloading = false;
        
        public static readonly List<HitInfo> HitsQueue = new();
    }
}
