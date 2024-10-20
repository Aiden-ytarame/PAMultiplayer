using System;
using System.Collections.Generic;
using System.Reflection;
using Cpp2IL.Core.Extensions;
using Il2CppSystems.SceneManagement;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Managers
{
    /// <summary>
    /// Manages the Lobby menu that pops up on level enter
    /// </summary>
    public class LobbyScreenManager : MonoBehaviour
    {
        public static LobbyScreenManager Instance { get; private set; }
        public PauseMenu pauseMenu;
        
        readonly Dictionary<ulong, Transform> _playerList = new();

        private readonly Dictionary<ulong, string> _specialColors = new()
        {
            { 76561199551343591, "3e2dba" }, //Vyrmax
            { 76561198895041739, "f582ff" }, //Maxine
            { 76561198040724652, "f582ff" }, //Pidge
            { 76561199141999343, "6ce6bb" }, //Aiden 00ffd0 
            { 76561199106356594, "34eb67" }, //yikxle
            { 76561199088465180, "7300ff" }  //Cube
        };
        Transform _playersListGo;
        GameObject _playerPrefab;

        //spawns the lobby GameObject from the assetBundle
        void Awake()
        {
            Instance = this;
            VGCursor.Inst.ShowCursor();
            GameObject playerGUI = GameObject.Find("Player GUI");
            GameObject lobbyGo;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PAMultiplayer.Assets.lobby menu"))
            {
                var lobbyBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
            
                lobbyGo =  Instantiate(lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]).Cast<GameObject>(),  playerGUI.transform);
                _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]).Cast<GameObject>();
         
                lobbyBundle.Unload(false);
            }
          
            lobbyGo.name = "PAM_Lobby";

            pauseMenu = lobbyGo.GetComponent<PauseMenu>();
            _playersListGo = lobbyGo.transform.Find("Content/PlayerList");
         
            //handles what should appear on the screen
            //like the buttons for the host
            //Waiting for host message for clients
            //or Lobby Failed message
            if (GlobalsManager.IsHosting)
            {
                var buttons = lobbyGo.transform.Find("Content/buttons");
                
                buttons.GetChild(0).GetComponent<MultiElementButton>().Select();
                
                buttons.GetChild(1).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
                {
                    
                    SceneLoader.Inst.LoadSceneGroup("Arcade");
                }));
                
                buttons.GetChild(2).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
                {
                    SceneLoader.Inst.LoadSceneGroup("Menu");
                }));
                
                if (!SteamLobbyManager.Inst.InLobby)
                {
                    lobbyGo.transform.Find("Content/buttons").gameObject.SetActive(false);
                    lobbyGo.transform.Find("Content/LobbyFailed").gameObject.SetActive(true);
                    lobbyGo.transform.Find("Content/LobbyFailed").GetComponentInChildren<MultiElementButton>().onClick.AddListener(new Action(
                        () =>
                        {
                            PAM.Logger.LogDebug("RETRY");
                            GlobalsManager.IsReloadingLobby = true;
                            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                        }));
                }
            }
            else //this is for the Waiting for host Message
            {
                lobbyGo.transform.Find("Content/buttons").gameObject.SetActive(false);
                lobbyGo.transform.Find("Content/WaitingForHost").gameObject.SetActive(true);

                var quitButton = lobbyGo.transform.Find("Content/WaitingForHost")
                    .GetComponentInChildren<MultiElementButton>();

                quitButton.Select();
                quitButton.onClick.AddListener(new Action(
                    () =>
                    {
                        SceneLoader.Inst.LoadSceneGroup("Menu");
                    }));
            }
            
            foreach (var friend in SteamLobbyManager.Inst.CurrentLobby.Members)
            {
                AddPlayerToLobby(friend.Id, friend.Name);
                if(SteamLobbyManager.Inst.CurrentLobby.GetMemberData(friend, "IsLoaded") == "1")
                {
                    SetPlayerLoaded(friend.Id); 
                }
            }
        }

        private void OnDestroy()
        {
            if (_playersListGo)
            {
                Destroy(_playersListGo.parent.parent.gameObject);
            }
        }

        public void AddPlayerToLobby(SteamId player, string playerName)
        {
            if (_playerList.ContainsKey(player))
            {
                return;
            }
            
            var playerEntry = Instantiate(_playerPrefab, _playersListGo.transform).Cast<GameObject>().transform;

            if (_specialColors.TryGetValue(player, out var hex))
            {
                playerName = $"<color=#{hex}>{playerName}";
            }
            
            playerEntry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            _playerList.Add(player, playerEntry);
        }

        public void RemovePlayerFromLobby(SteamId player)
        {
            if (_playerList.TryGetValue(player, out var value))
            {
                Destroy(value.gameObject);
                _playerList.Remove(player);
            }
        }

        public void SetPlayerLoaded(SteamId player)
        {
            if (_playerList.TryGetValue(player, out var value))
            {
                AudioManager.Inst?.PlaySound("Add", 1);
                value.GetChild(1).GetComponent<TextMeshProUGUI>().text = "▓";    
            }
        }

        public void StartLevel()
        {
            GlobalsManager.HasStarted = true;
            SteamLobbyManager.Inst.HideLobby();
            pauseMenu.UnPause();
        }
    }
}
