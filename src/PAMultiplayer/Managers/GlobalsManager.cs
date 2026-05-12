using System;
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
        
        public static List<string> Queue = new();

        public static List<string> GetQueueLevelNames()
        {
            List<string> levelNames = new();
            for (var i = 0; i < Queue.Count; i++)
            {
                if (i > 9 && Queue.Count != 11)
                {
                    levelNames.Add($"+{Queue.Count - i} Levels");
                    break;
                }
                VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(Queue[i]);
                levelNames.Add(level.TrackName);
            }
            
            return levelNames;
        }
        
        public static string LevelId;
        public static bool IsMultiplayer = false;
        public static bool IsHosting = false;
        public static SteamLobbyManager.LobbyState LobbyState;
        
        public static bool HasLoadedAllInfo => HasLoadedExternalInfo && HasLoadedBasePlayerIds;

        public static bool HasLoadedExternalInfo;
        public static bool HasLoadedBasePlayerIds;

        public static bool HasLoadedAllLobbyInfo => HasLoadedMainLobbyInfo && HasLoadedMidLobbyInfo;
        
        public static bool HasLoadedMidLobbyInfo = true;
        public static bool HasLoadedMainLobbyInfo = true;

        public static bool IsReloadingLobby = false;
        public static bool HasStarted = false;
        public static bool IsDownloading = false;
        public static bool IsChallenge = false;
        
        public static bool JoinedMidLevel = false;

        public static readonly List<HitInfo> HitsQueue = new();
    }
}
