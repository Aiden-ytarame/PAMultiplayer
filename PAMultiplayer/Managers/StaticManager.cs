using PAMultiplayer.Patch;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace PAMultiplayer
{
    public class StaticManager
    {
        public static string LocalPlayer;
        public static Client.Client Client;
        public static Server.Server Server;

        public static Dictionary<string, VGPlayerManager.VGPlayerData> Players;
        public static Dictionary<string, Vector2> PlayerPositions = new Dictionary<string, Vector2>();
        public static LobbyInfo LobbyInfo = new LobbyInfo();

        public static string ServerIp = "";
        public static string ServerPort = "";

        public static bool SpawnPending = false;
        public static List<int> DamageQueue = new List<int>();

        public static bool IsLobby = false;
        public static bool IsHosting = false;
        public static bool IsMultiplayer = false; //checking if client is null always returns false for some reason.

        public static void InitClient(string ServerName)
        {
            if (Client != null)
                Client.SendDisconnect();

            Plugin.Instance.Log.LogWarning(ServerIp);
            Plugin.Instance.Log.LogWarning(ServerPort);

            LocalPlayer = " ";
            try
            {
                Client = new Client.Client(int.Parse(ServerPort), ServerIp, ServerName);
                Players = new Dictionary<string, VGPlayerManager.VGPlayerData>();
            }
            catch (Exception ex)
            {
                Plugin.Instance.Log.LogFatal("Error while trying to InitClient");
                Plugin.Instance.Log.LogFatal(ex);
            }
        }
    }

    //I wrote so much untested code while im tired, this likely sucks
    public struct LobbyInfo
    {
        public LobbyInfo() { }

        public Dictionary<string, string> PlayerDisplayName { readonly get; private set; }  = new Dictionary<string, string>();
        public Dictionary<string, bool> PlayerLoaded { readonly get; private set; } = new Dictionary<string, bool>();

        public void AddPlayerInfo(string player, string displayName)
        {
            PlayerDisplayName.TryAdd(player, displayName);
            PlayerLoaded.TryAdd(player, false);
        }

        public void RemovePlayerInfo(string player)
        {
            PlayerDisplayName.Remove(player);
            PlayerLoaded.Remove(player);

            if (LobbyManager.instance)
            {
                LobbyManager.instance.RemovePlayerFromLobby(player);
            }
        }

        public void SetLoaded(string player)
        {
            PlayerLoaded[player] = true;
        }
        public bool isEveryoneLoaded
        {
            get 
            {
                return !PlayerLoaded.ContainsValue(false);
            }
        }
    }
}
