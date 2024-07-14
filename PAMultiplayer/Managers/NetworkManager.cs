using UnityEngine;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// this class should be removed honestly
    /// calls receive for the server/client callbacks
    /// and cleans some stuff on level unload
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        void Update()
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }
        
        private void OnDestroy()
        {
            GlobalsManager.HasStarted = false;
            SteamManager.Inst.EndServer();
            SteamManager.Inst.EndClient();
            GlobalsManager.Players.Clear();
        }
        
    }
}
