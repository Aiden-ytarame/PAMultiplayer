using BepInEx.Unity.IL2CPP.UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using KeyCode = BepInEx.Unity.IL2CPP.UnityEngine.KeyCode;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// this class should be removed honestly
    /// sends player position
    /// calls receive for the server/client callbacks
    /// and cleans some stuff on level unload
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        void Update()
        {
            if (!GlobalsManager.IsMultiplayer) return;

            if (Input.GetKeyInt(KeyCode.P))
            {
                GameManager.Inst.StartCoroutine(PauseLobbyPatch.ShowNames().WrapToIl2Cpp());
            }
            
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }
     
        private void FixedUpdate()
        {
            if (GlobalsManager.Players.TryGetValue(GlobalsManager.LocalPlayer, out var playerData))
            {
                if (playerData.PlayerObject)
                {
                    var V2 =playerData.PlayerObject.Player_Rigidbody.transform.position;
                    if (GlobalsManager.IsHosting)
                        SteamManager.Inst.Server?.SendHostPosition(V2);
                    else
                        SteamManager.Inst.Client?.SendPosition(V2);
                }
            }
        }

        private void OnDestroy()
        {
            GlobalsManager.HasStarted = false;
            GlobalsManager.LocalPlayerObjectId = 0;
            SteamManager.Inst.EndServer();
            SteamManager.Inst.EndClient();
            GlobalsManager.Players.Clear();
            VGPlayerManager.Inst.players.Clear();
        }
        
    }
}
