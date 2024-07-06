using System;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    //this class has to be remade or removed
    public class NetworkManager : MonoBehaviour
    {
        void OnEnable()
        {
            if (StaticManager.IsHosting && !GameManager.Inst.IsEditor)
            {
                Plugin.Inst.Log.LogError("Init Server");
                SteamLobbyManager.Inst.CreateLobby();
            }
        }

        void Update()
        {
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();

            if (!StaticManager.IsMultiplayer || VGPlayerManager.Inst.players.Count == 0) return;
            
            VGPlayer player = VGPlayerManager.Inst.players[0].PlayerObject;
            if (!player) return;
            
            if (player.Player_Rigidbody)
            {
                var V2 = player.Player_Rigidbody.transform.position;
                if (StaticManager.IsHosting)
                    SteamManager.Inst.Server?.SendHostPosition(V2);
                else
                    SteamManager.Inst.Client?.SendPosition(V2);
            }
        }

        void OnDisable()
        {
            try
            {
                SteamManager.Inst.EndServer();
                SteamManager.Inst.EndClient();
                StaticManager.Players.Clear();
            }
            catch(Exception e)
            {
                Plugin.Inst.Log.LogError(e);
            }
        }
    }
}
