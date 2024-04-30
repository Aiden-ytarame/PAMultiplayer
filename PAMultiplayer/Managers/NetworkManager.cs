using Lidgren.Network;
using UnityEngine;

namespace PAMultiplayer
{
    public class NetworkManager : MonoBehaviour
    {
        void OnEnable()
        {
            StaticManager.IsHosting = DataManager.inst.GetSettingBool("online_host");
            if (StaticManager.IsHosting)
            {

                if (StaticManager.Server == null || StaticManager.Server.NetServer.Status == NetPeerStatus.NotRunning)
                {
                    Plugin.Instance.Log.LogError("Init Server");
                    StaticManager.Server = new Server.Server();
                }
            }
            else
            {
                if (StaticManager.ServerIp == "" || StaticManager.ServerIp == "localhost")
                {
                    StaticManager.IsMultiplayer = false;
                    StaticManager.IsLobby = false;
                    Destroy(this);
                    return;
                }
            }
            Plugin.Instance.Log.LogError("Init Client");
            StaticManager.IsLobby = true;
            StaticManager.IsMultiplayer = true;
            StaticManager.InitClient("PAServer");
        }

        void Update() //Not sure if FixedUpdate is better.
        {
            var PosEnu = StaticManager.PlayerPositions.GetEnumerator();

            while (PosEnu.MoveNext())
            {

                if (PosEnu.Current.Key == StaticManager.LocalPlayer)
                    continue;

                if (!StaticManager.Players[PosEnu.Current.Key].PlayerObject)
                    continue;

                Rigidbody2D rb = StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Rigidbody;

                Vector2 DeltaPos = rb.position - PosEnu.Current.Value;
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

            PosEnu.Dispose();
        }

        void OnDisable()
        {
            if (StaticManager.Server != null)
                StaticManager.Server.NetServer.Shutdown("Ended");
            
            if (StaticManager.Client != null && StaticManager.Client.NetClient != null && StaticManager.Client.NetClient.ConnectionStatus == NetConnectionStatus.Connected)
                StaticManager.Client.SendDisconnect();
            
            StaticManager.LobbyInfo = new LobbyInfo();
        }

    }
}
