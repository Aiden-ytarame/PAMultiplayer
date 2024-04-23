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

        public static string ServerIp = "";
        public static string ServerPort = "";

        public static bool SpawnPending = false;
        public static List<int> DamageQueue = new List<int>();

        static public bool IsLobby = false;
        static public bool IsMultiplayer = false; //checking if client is null always returns false for some reason.
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
}
