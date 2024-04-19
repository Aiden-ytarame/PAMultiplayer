
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using YtaramMultiplayer.Client;
using Lidgren.Network;



namespace YtaramMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_UpdatePatch
    {
        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static bool Hit_Pre(ref VGPlayer __instance)
        {
            if (__instance.PlayerID == StaticManager.DamageQueue)
            {
                StaticManager.DamageQueue = -1;
                return true;
            }
            if(__instance.PlayerID == 0)
            {
                StaticManager.Client.SendDamage();
                return true;
            }
            return false;

        }

        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPrefix]
        static void Update_Pre(ref VGPlayer __instance)
        {
            if (StaticManager.Client == null || StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Disconnected)
                return;

              if (StaticManager.SpawnPending)
              {

                var Enu = VGPlayerManager.Inst.players.GetEnumerator();

                while (Enu.MoveNext())
                {
                    Enu.Current.PlayerObject.ClearEvents();
                    Enu.Current.PlayerObject.PlayerDeath();

                }

                if (GameManager2.Inst)
                    GameManager2.Inst.RewindToCheckpoint(0, true);

                StaticManager.SpawnPending = false;
                VGPlayerManager.Inst.RespawnPlayers();
            }

            if (StaticManager.Players == null)
                return;

              if (StaticManager.Players.ContainsKey(StaticManager.LocalPlayer))
              {
                  if (__instance.PlayerID == 0)
                  {
                      if (__instance.Player_Rigidbody != null)
                      {
                          if (!StaticManager.Players[StaticManager.LocalPlayer].PlayerObject)
                              StaticManager.Players[StaticManager.LocalPlayer].PlayerObject = __instance;

                          var V2 = __instance.Player_Rigidbody.transform.position;
                          var rot = __instance.Player_Rigidbody.transform.eulerAngles;
                          StaticManager.Client.SendPosition(V2.x, V2.y);
                          StaticManager.Client.SendRotation(rot.z);
                      }

                  }
              }
              else
              {
                  if (__instance.PlayerID == 0)
                      StaticManager.Players.Add(StaticManager.LocalPlayer, VGPlayerManager.Inst.players[0]);
              }

        }

    }
    [HarmonyPatch(typeof(GameManager2))]
    public class AddNetManager
    {
        [HarmonyPatch(nameof(GameManager2.Start))]
        [HarmonyPostfix]
        static void GM2_PostStart(ref GameManager2 __instance)
        {       
            var netMan = new GameObject("Network");
            netMan.AddComponent<NetworkManager>();
        }

    }

    public class NetworkManager : MonoBehaviour
    {
        void OnEnable() 
        {
            if(DataManager.inst.GetSettingBool("online_host"))
            {
                if(StaticManager.Server == null || StaticManager.Server.NetServer.Status == NetPeerStatus.NotRunning)
                    StaticManager.Server = new Server.Server();
            }
            if (StaticManager.Client == null || StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Disconnected)
            {
                StaticManager.InitClient("PAServer");
                return;
            }
        
        }

        void OnDisable()
        {
            if (StaticManager.Client != null && StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Connected)
                StaticManager.Client.SendDisconnect();

            if (StaticManager.Server == null || StaticManager.Server.NetServer.Status == NetPeerStatus.Running)
                StaticManager.Server.NetServer.Shutdown("Ended");
        }
    }

}
