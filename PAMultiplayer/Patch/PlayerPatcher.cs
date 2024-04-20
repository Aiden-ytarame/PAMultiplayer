
using HarmonyLib;
using UnityEngine;
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
            VGPlayerManager.Inst.pla

            if (__instance.PlayerID == 0)
            {
                if (StaticManager.IsMultiplayer && StaticManager.Client.NetClient != null)
                    StaticManager.Client.SendDamage();
                return true;
            }

            if (__instance.PlayerID == StaticManager.DamageQueue)
            {
                StaticManager.DamageQueue = -1;
                return true;
            }
            return false;

        }


        [HarmonyPatch(nameof(VGPlayer.Update))]
        [HarmonyPrefix]
        static void Update_Pre(ref VGPlayer __instance)
        {
            if (!StaticManager.IsMultiplayer || StaticManager.Client.NetClient == null || StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Disconnected)
            { 
                return;
            }


              if (StaticManager.SpawnPending)
              {
                StaticManager.SpawnPending = false;
                VGPlayerManager.Inst.RespawnPlayers();
                GameManager2.Inst.RewindToCheckpoint(0, true);
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

            if (DataManager.inst.GetSettingBool("online_host"))
            {

                if (StaticManager.Server == null || StaticManager.Server.NetServer.Status == NetPeerStatus.NotRunning)
                {
                    StaticManager.Server = new Server.Server();
                }
            }


            if (!DataManager.inst.GetSettingBool("online_host"))
            {
                if (StaticManager.ServerIp == "")
                    return;
            }
            Plugin.Instance.Log.LogError("Init Client");
            StaticManager.IsMultiplayer = true;
            StaticManager.InitClient("PAServer");


        }

        //interpolate player positions
        void Update()
        {
            var PosEnu = StaticManager.PlayerPositions.GetEnumerator();
            Vector2 newPos;
            while (PosEnu.MoveNext())
            {

                Rigidbody2D rb = StaticManager.Players[PosEnu.Current.Key].PlayerObject?.Player_Rigidbody;
                if (!rb)
                    continue;

                newPos = PosEnu.Current.Value;
                float Magnetude = (newPos - rb.position).sqrMagnitude;
                if (Magnetude > 10) // no idea if these numbers are correct
                {
                    Plugin.Instance.Log.LogWarning("Corrected Pos");
                    rb.position = newPos;
                    continue;
                }

                if (Magnetude < 2)
                {
                    if (rb.position != newPos)
                        rb.position = Vector2.Lerp(rb.position, newPos, 20 * Time.deltaTime);

                    return;
                }

                rb.position = Vector2.LerpUnclamped(rb.position, newPos, 20 * Time.deltaTime);

            }
        }
    
        void OnDisable()
        {
            StaticManager.IsMultiplayer = false;
            if (StaticManager.IsMultiplayer && StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Connected)
                StaticManager.Client.SendDisconnect();

            if (StaticManager.Server != null && StaticManager.Server.NetServer.Status == NetPeerStatus.Running)
                StaticManager.Server.NetServer.Shutdown("Ended");
        }

    }

}
