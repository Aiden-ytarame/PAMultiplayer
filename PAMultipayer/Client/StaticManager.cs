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
        public static bool SpawnPending = false;
        public static int DamageQueue = -1;
        public static void InitClient(int port, string ServerIp, string ServerName)
        {
            if(Client != null)
                Client.SendDisconnect();

            LocalPlayer = " ";
            Client = new Client(port, ServerIp, ServerName);
            Players = new Dictionary<string, VGPlayerManager.VGPlayerData>();
        }
    }
}
