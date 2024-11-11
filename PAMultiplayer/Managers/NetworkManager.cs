using BepInEx.Unity.IL2CPP.UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using PAMultiplayer.Patch;
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
        private bool _pressedNameKey;


        void Update()
        {
            if (GlobalsManager.Players.TryGetValue(GlobalsManager.LocalPlayer, out var playerData))
            {
                if (playerData.PlayerObject)
                {
                    var V2 = playerData.PlayerObject.Player_Rigidbody.transform.position;
                    if (GlobalsManager.IsHosting)
                        SteamManager.Inst.Server?.SendHostPosition(V2);
                    else
                        SteamManager.Inst.Client?.SendPosition(V2);
                }
            }

            if (Input.GetKeyInt(KeyCode.Tab) || Input.GetKeyInt(KeyCode.JoystickButton4)) //left shoulder (?)
            {
                //to make this an on button down action
                if (!_pressedNameKey)
                {
                    _pressedNameKey = true;
                    GameManager.Inst.StartCoroutine(PauseLobbyPatch.ShowNames().WrapToIl2Cpp());
                }
            }
            else
            {
                _pressedNameKey = false;
            }

            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }

        private void OnDestroy()
        {
            GlobalsManager.HasStarted = false;
            
            if (GlobalsManager.IsReloadingLobby) return;
            
            GlobalsManager.LocalPlayerObjectId = 0;
            SteamManager.Inst.EndServer();
            SteamManager.Inst.EndClient();
            GlobalsManager.Players.Clear();
            VGPlayerManager.Inst.players.Clear();
        }
        
    }
}
