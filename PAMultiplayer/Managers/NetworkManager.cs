using System;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    //this class has to be remade or removed
    public class NetworkManager : MonoBehaviour
    {
        void OnEnable()
        {
            if (GlobalsManager.IsHosting && !GameManager.Inst.IsEditor)
            {
                Plugin.Inst.Log.LogError("Init Server");
                SteamLobbyManager.Inst.CreateLobby();
            }
        }

        void Update()
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }

        void OnDisable()
        {
            try
            {
                SteamManager.Inst.EndServer();
                SteamManager.Inst.EndClient();
                GlobalsManager.Players.Clear();
            }
            catch(Exception e)
            {
                Plugin.Inst.Log.LogError(e);
            }
        }
    }
}
