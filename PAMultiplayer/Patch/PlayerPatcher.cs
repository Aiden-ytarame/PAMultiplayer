
using HarmonyLib;
using UnityEngine;
using UnityEditor;
using YtaramMultiplayer.Client;
using Lidgren.Network;
using UnityEngine.Localization.PropertyVariants;
using Il2CppSystem.Runtime.InteropServices;
using YtaramMultiplayer.Server;



namespace YtaramMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_UpdatePatch
    {
        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static bool Hit_Pre(ref VGPlayer __instance)
        {
            Plugin.Instance.Log.LogWarning("DAMAGE ATTEMPT");
            Plugin.Instance.Log.LogWarning(__instance.PlayerID);
 
            if (__instance.PlayerID == 0)
            {
                if (StaticManager.IsMultiplayer && __instance.CanTakeDamage)
                {
                    StaticManager.Client.SendDamage();
                }
                return true;
            }
            if (StaticManager.DamageQueue.Contains(__instance.PlayerID))
            {
                StaticManager.DamageQueue.Remove(__instance.PlayerID);
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
                Plugin.Instance.Log.LogError(VGPlayerManager.Inst.players.Count);
                VGPlayerManager.Inst.RespawnPlayers();
                GameManager2.Inst.RewindToCheckpoint(0, true);
            }

            if (StaticManager.DamageQueue.Contains(__instance.PlayerID))
            {
                Plugin.Instance.Log.LogWarning("CONTAINS");
                __instance.PlayerHit();
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
                          StaticManager.Client.SendPosition(V2.x, V2.y);
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
                {
                    Destroy(this);
                    return;
                }
            }
            Plugin.Instance.Log.LogError("Init Client");
            StaticManager.IsMultiplayer = true;
            StaticManager.InitClient("PAServer");


        }

        void FixedUpdate()
        {
            var PosEnu = StaticManager.PlayerPositions.GetEnumerator();

            while (PosEnu.MoveNext())
            {
               
                if (PosEnu.Current.Key == StaticManager.LocalPlayer)
                    continue;

                if(StaticManager.Players[PosEnu.Current.Key].PlayerObject == null)               
                    continue;
                
                Rigidbody2D rb = StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Rigidbody;

                Vector2 DeltaPos = rb.position -  PosEnu.Current.Value;
                StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Wrapper.transform.Rotate(new Vector3(0, 0, Mathf.Atan2(DeltaPos.x, DeltaPos.y)), Space.World);

                rb.position = PosEnu.Current.Value;

                continue;

                //Ignore everything under this, its broken as shit
                /////////////////////////////////////////////////////////////
                Vector2 newPos = PosEnu.Current.Value;
                float Magnetude = (newPos - rb.position).sqrMagnitude;
                if (Magnetude > 15) // no idea if these numbers are correct
                {
                    Plugin.Instance.Log.LogWarning("Corrected Pos");
                    rb.position = newPos;
                    continue;
                }

                if (Magnetude < 0.3)
                {
                    if (rb.position != newPos)
                        rb.position = Vector2.Lerp(rb.position, newPos, 20 * Time.fixedDeltaTime);

                    return;
                }

                if (Magnetude < 5)
                {
                    rb.position = Vector2.LerpUnclamped(rb.position, newPos, 85 * Time.fixedDeltaTime);
                    return;
                }

                rb.position = Vector2.LerpUnclamped(rb.position, newPos, 20 * Time.fixedDeltaTime);

            }
        }

        private void SceneManager_activeSceneChanged(UnityEngine.SceneManagement.Scene arg0, UnityEngine.SceneManagement.Scene arg1)
        {
            throw new System.NotImplementedException();
        }

        void OnDisable()
        {
            StaticManager.IsMultiplayer = false;
            if (StaticManager.IsMultiplayer && StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Connected)
                StaticManager.Client.SendDisconnect();

            if (StaticManager.Server != null)
                StaticManager.Server.NetServer.Shutdown("Ended");
        }

    }

}
