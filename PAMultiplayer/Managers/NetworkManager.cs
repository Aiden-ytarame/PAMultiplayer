using BepInEx.Unity.IL2CPP.UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Network_Test.Core;
using Network_Test.Core.Rpc;
using PAMultiplayer.AttributeNetworkWrapper;
using PAMultiplayer.Patch;
using Rewired;
using Steamworks;
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
        PaMNetworkManager _paMNetworkManager;
        
        void Update()
        {
            if (GlobalsManager.Players.TryGetValue(GlobalsManager.LocalPlayerId, out var playerData))
            {
                if (playerData.VGPlayerData.PlayerObject)
                {
                    var v2 = playerData.VGPlayerData.PlayerObject.Player_Wrapper.position;
                    if (GlobalsManager.IsHosting)
                        Multi_PlayerPos(GlobalsManager.LocalPlayerId, v2);
                    else
                        Server_PlayerPos(null, v2);
                }
            }
            
            if (Input.GetKeyInt(KeyCode.Tab) || Input.GetKeyInt(KeyCode.JoystickButton4)) //left shoulder (?)
            {
                //to make this an on button down action
                if (!_pressedNameKey)
                {
                    _pressedNameKey = true;
                    StartCoroutine(PauseLobbyPatch.ShowNames().WrapToIl2Cpp());
                }
            }
            else
            {
                _pressedNameKey = false;
            }

            TryToGetController();

            if (_paMNetworkManager == null)
            {
                _paMNetworkManager = (PaMNetworkManager)Network_Test.NetworkManager.Instance;
            }
            
            _paMNetworkManager?.Receive();
        }

        [ServerRpc(SendType.Unreliable)]
        public static void Server_PlayerPos(ClientNetworkConnection conn,Vector2 pos)
        {
            if(!conn.TryGetSteamId(out SteamId steamID))
            {
                return;
            }
            SetPlayerPos(steamID, pos);
            Multi_PlayerPos(steamID, pos);
        }
        
        [MultiRpc(SendType.Unreliable)]
        public static void Multi_PlayerPos(SteamId id,Vector2 pos)
        {
            SetPlayerPos(id, pos);
        }

        static void SetPlayerPos(SteamId steamID, Vector2 pos)
        {
            if (steamID.IsLocalPlayer()) return;
        
            if (GlobalsManager.Players.TryGetValue(steamID, out var playerData))
            {
                if (playerData.VGPlayerData.PlayerObject)
                {
                    VGPlayer player = GlobalsManager.Players[steamID].VGPlayerData.PlayerObject;
                
                    if(!player) return;
                
                    Transform rb = player.Player_Wrapper;
            
                    var rot = pos - (Vector2)rb.position;
                    rb.position = pos;
                    if (rot.sqrMagnitude > 0.0001f)
                    {
                        rot.Normalize();
                        player.p_lastMoveX = rot.x;
                        player.p_lastMoveY = rot.y;
                    }
                }
            }
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
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
            
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
