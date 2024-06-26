using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    public class StaticManager
    {
        public static SteamId LocalPlayer;

        public static Dictionary<SteamId, VGPlayerManager.VGPlayerData> Players = new();
        public static Dictionary<SteamId, Vector2> PlayerPositions = new();
        public static LobbyInfo LobbyInfo = new LobbyInfo();

        public static string ServerIp = "";
        public static string ServerPort = "";
        
        public static List<int> DamageQueue = new();

        public static bool IsLobby = true;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false; //checking if client is null always returns false for some reason.
    }

    //I wrote so much untested code while im tired, this likely sucks
    public struct LobbyInfo
    {
        public LobbyInfo() { }

        Dictionary<SteamId, bool> PlayerLoaded { get; } = new();

        public void AddPlayerToLoadList(SteamId playerSteamId)
        {
            PlayerLoaded.TryAdd(playerSteamId, false);
        }

        public void RemovePlayerFromLoadList(SteamId player)
        {
            PlayerLoaded.Remove(player);
        }

        public void SetLoaded(SteamId playerSteamId)
        {
            PlayerLoaded[playerSteamId] = true;
        }
        public bool IsEveryoneLoaded => !PlayerLoaded.ContainsValue(false);
    }
}
