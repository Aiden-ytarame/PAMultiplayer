using System.Collections.Generic;
using AttributeNetworkWrapperV2;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Patch;
using Rewired;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// this class should be removed honestly
    /// sends player position
    /// calls receive for the server/client callbacks
    /// and cleans some stuff on level unload
    /// </summary>
    public partial class NetworkManager : MonoBehaviour
    {
        private class PlayerPredictionData
        {
            public VGPlayerManager.VGPlayerData Player;
            public Vector2 Position;
            public Vector2 LastPosition;
            public Vector2 PredictedPosition;
            public Vector2 LastMovementDirection;
            public float Speed;
            public float InterpSpeed;
            public bool Extrapolating = false;
            
            private ushort _lastId;
            private float _timeReceived;

            public void Update(ushort id, Vector2 pos)
            {
                if (id < _lastId && id > _lastId - 100)
                {
                    return;
                }

                float timeDelta = Time.timeSinceLevelLoad - _timeReceived;
                Transform player = Player?.PlayerObject?.Player_Wrapper;

                Position = pos;
                PredictedPosition = player?.position ?? pos;
                Extrapolating = false;
                _timeReceived = Time.timeSinceLevelLoad;

                Vector2 rot = pos - LastPosition;
                if (rot.sqrMagnitude > 0.0001f)
                {
                    rot.Normalize();
                }
                else
                {
                    rot = Vector2.zero;
                }
                
                Speed = (Position - LastPosition).sqrMagnitude;
                InterpSpeed = (Position - PredictedPosition).sqrMagnitude;

                if (rot != Vector2.zero)
                {
                    LastMovementDirection = rot;
                }
                else
                {
                    Speed = 0;
                }
                
                if (InterpSpeed < Speed)
                {
                    Speed = Mathf.Sqrt(Speed) / timeDelta; //speed in units per second
                    InterpSpeed = Speed;
                }
                else
                {
                    if (timeDelta >= 2f && player) // too much time and distance since last position
                    {
                        player.position = pos;
                    }

                    Speed = Mathf.Sqrt(Speed) / timeDelta;
                    InterpSpeed = Mathf.Sqrt(InterpSpeed) / timeDelta;
                }

                if (!float.IsNormal(Speed))
                {
                    Speed = 0;
                }

                if (!float.IsNormal(InterpSpeed))
                {
                    InterpSpeed = 0;
                }
               
                _lastId = id;
                LastPosition = Position;
            }

            public PlayerPredictionData(VGPlayerManager.VGPlayerData player, Vector2 lastPosition, Vector2 lastMovementDirection)
            {
                Player = player;
                Position = lastPosition;
                LastMovementDirection = lastMovementDirection;
                _timeReceived = Time.timeSinceLevelLoad;
            }
        }
        
        private const float PositionUpdateDelay = 1f / 50f;
        
        PaMNetworkManager _paMNetworkManager;
        private VGPlayerManager.VGPlayerData _localData;
        private float _timeSinceUpdate = 0;
        private ushort _movementId;
        
        private static Dictionary<ulong, PlayerPredictionData> _playerPrediction = new();
        
        void Update()
        {
            if (_localData == null)
            {
                if (GlobalsManager.Players.TryGetValue(GlobalsManager.LocalPlayerId, out var data))
                {
                    _localData = data.VGPlayerData;
                }
            }

            if (_timeSinceUpdate > PositionUpdateDelay)
            {
                if (_localData?.PlayerObject?.Player_Wrapper && !_localData!.PlayerObject.isDead)                                 
                {        
                    _timeSinceUpdate = 0;
                    _movementId++;
                    var v2 = _localData.PlayerObject.Player_Wrapper.position;                  
                    if (GlobalsManager.IsHosting)                                              
                    {                                                                          
                        CallRpc_Multi_PlayerPos(GlobalsManager.LocalPlayerId, _movementId, v2);             
                    }                                                                          
                    else                                                                       
                    {                                                                          
                        CallRpc_Server_PlayerPos(_movementId, v2);                                          
                    }                                                                          
                }                                                                              
            }
            else
            {
                _timeSinceUpdate += Time.unscaledDeltaTime;   
            }

            TryToGetController();      
            
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton4)) //left shoulder (?)
            {
                StartCoroutine(PauseLobbyPatch.ShowNames());
            }
            
            if (_paMNetworkManager == null)
            {
                _paMNetworkManager = (PaMNetworkManager)AttributeNetworkWrapperV2.NetworkManager.Instance;
                
                if (_paMNetworkManager != null)
                {
                    _paMNetworkManager.OnPlayerLeave += id =>
                    {
                        _playerPrediction.Remove(id);
                    };
                    
                    _paMNetworkManager.Receive();    
                }
            }
            else
            {
                _paMNetworkManager.Receive();    
            }
            
            foreach (var playerPrediction in _playerPrediction)
            {
                HandlePlayersPrediction(playerPrediction.Value);
            }
        }

        [ServerRpc(SendType.Unreliable)]
        private static void Server_PlayerPos(ClientNetworkConnection conn, ushort moveId, Vector2 pos)
        {
            if(!conn.TryGetSteamId(out SteamId steamID))
            {
                PAM.Logger.LogError($"failed to get player position {conn.Address}");
                return;
            }
            
            CallRpc_Multi_PlayerPos(steamID, moveId, pos);
        }

        [MultiRpc(SendType.Unreliable)]
        private static void Multi_PlayerPos(SteamId id, ushort moveId, Vector2 pos)
        {
            if (id.IsLocalPlayer() || !GlobalsManager.Players.TryGetValue(id, out var playerData))
            {
                return;
            }

            VGPlayer player = playerData.VGPlayerData.PlayerObject;

            if (!player?.Player_Wrapper) 
            {               
                return;     
            }

            Vector2 currentPos = player.Player_Wrapper.position;
            if (!_playerPrediction.TryGetValue(id, out var predictionData))
            {
                var rot = pos - currentPos;
                if (rot.sqrMagnitude > 0.0001f)
                {
                    rot.Normalize();        
                }
                else
                {
                    rot = Vector2.zero;
                }

                _playerPrediction.Add(id, new PlayerPredictionData(playerData.VGPlayerData, pos, rot));
                return;
            }
            
            predictionData.Update(moveId, pos);     
        }

        void HandlePlayersPrediction(PlayerPredictionData data)
        {
            VGPlayer player = data.Player?.PlayerObject;

            if (!player?.Player_Wrapper || player.isDead)
            {
                return;
            }

            player.p_lastMoveX = data.LastMovementDirection.x;
            player.p_lastMoveY = data.LastMovementDirection.y;
            
            if (data.Extrapolating)
            {
                Vector2 current = player.Player_Wrapper.position;
                current += data.Speed * Time.deltaTime * data.LastMovementDirection;
                player.Player_Wrapper.position = current;
                return;
            }
            
            Vector2 interp = Vector2.MoveTowards(player.Player_Wrapper.position, data.Position, data.InterpSpeed * Time.deltaTime);
            if (float.IsNaN(interp.x) || float.IsNaN(interp.y))
            {
                interp = data.Position;
            }
            player.Player_Wrapper.position = interp;
        
            if (interp == data.Position)
            {
                data.Extrapolating = true;
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
            _playerPrediction.Clear();
            
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
