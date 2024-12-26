using BepInEx.Unity.IL2CPP.UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using PAMultiplayer.Patch;
using Rewired;
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
            if (GlobalsManager.Players.TryGetValue(GlobalsManager.LocalPlayerId, out var playerData))
            {
                if (playerData.VGPlayerData.PlayerObject)
                {
                    var v2 = playerData.VGPlayerData.PlayerObject.Player_Wrapper.position;
                    if (GlobalsManager.IsHosting)
                        SteamManager.Inst.Server?.SendHostPosition(v2);
                    else
                        SteamManager.Inst.Client?.SendPosition(v2);
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

            TryToGetController();
            
            SteamManager.Inst.Server?.Receive();
            SteamManager.Inst.Client?.Receive();
        }

        void TryToGetController()
        {
            for (int i = 0; i < ReInput.controllers.controllerCount; i++)
            {
                var controller = ReInput.controllers.Controllers[i];
                if (controller.isConnected && controller.enabled && 
                    (controller.type == ControllerType.Keyboard || controller.type == ControllerType.Joystick))
                {
                    if (!ReInput.players.GetPlayer(0).controllers.ContainsController(controller))
                    {
                        ReInput.players.GetPlayer(0).controllers.AddController(controller, true);
                    }
                }
            }
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
            VGPlayerManager.Inst.players.Add(new VGPlayerManager.VGPlayerData(){ControllerID = 0, PlayerID = 0});
        }
        
    }
}
