using System;
using System.Collections.Generic;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Client
{
    public class StaticManager
    {
        public static string LocalPlayer;
        public static Client Client;
        public static Server.Server Server;
        public static Dictionary<string, VGPlayerManager.VGPlayerData> Players;
        public static string ServerIp = "";
        public static string ServerPort = "";
        public static bool SpawnPending = false;
        public static int DamageQueue = -1;
        static public bool IsMultiplayer = false; //checking if client is null always returns false for some reason.
        public static void InitClient(string ServerName)
        {
            if(Client != null)
                Client.SendDisconnect();

            Plugin.Instance.Log.LogWarning(ServerIp);
            Plugin.Instance.Log.LogWarning(ServerPort);

            LocalPlayer = " ";
            try
            {
                Client = new Client(int.Parse(ServerPort), ServerIp, ServerName);
                Players = new Dictionary<string, VGPlayerManager.VGPlayerData>();
            }
            catch(Exception ex)
            {
                Plugin.Instance.Log.LogFatal("Error while trying to InitClient");
                Plugin.Instance.Log.LogFatal(ex);
            }
        }
    }
}
