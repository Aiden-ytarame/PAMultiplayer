using System;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    //this class has to be remade or removed
    public class NetworkManager : MonoBehaviour
    {
        void Update()
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }

        void OnDisable()
        {
            GlobalsManager.HasStarted = false;
            SteamManager.Inst.EndServer();
            SteamManager.Inst.EndClient();
            GlobalsManager.Players.Clear();

        }
    }
}
