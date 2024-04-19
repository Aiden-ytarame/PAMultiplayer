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
        public static string ServerIp;
        public static string ServerPort;
        public static bool SpawnPending = false;
        public static int DamageQueue = -1;
        public static void InitClient(string ServerName)
        {
            if(Client != null)
                Client.SendDisconnect();

            Plugin.Instance.Log.LogWarning(ServerIp);
            Plugin.Instance.Log.LogWarning(ServerPort);

            LocalPlayer = " ";
            try
            {

                if (DataManager.inst.GetSettingBool("online_host")) 
                    ServerIp = "127.0.0.1"; //does LocalHost work here?

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
