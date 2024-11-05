using System;
using System.Collections.Generic;
using System.Reflection;
using Cpp2IL.Core.Extensions;
using Il2CppSystems.SceneManagement;
using Newtonsoft.Json;
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
            { 76561199551343591, "3e2dba" }, //Vyrmax, frien
            { 76561198895041739, "f582ff" }, //Maxine, frien kitty
            { 76561198040724652, "f582ff" }, //Pidge, develepoer
            { 76561199141999343, "6ce6bb" }, //Aiden, me
            { 76561199106356594, "34eb67" }, //yikxle, frien
            { 76561199088465180, "7300ff" },  //Cube, frien
            { 76561198310357491, "66ccff" },  //cozm, made the mp logo 
        };
        Transform _playersList;
        GameObject _playerPrefab;

        Transform _queueList;
        GameObject _queueEntryPrefab;

        TextMeshProUGUI _PlayersLoaded;

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
                _queueEntryPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[2]).Cast<GameObject>();
                lobbyBundle.Unload(false);
            }
          
            lobbyGo.name = "PAM_Lobby";

            pauseMenu = lobbyGo.GetComponent<PauseMenu>();
            _playersList = lobbyGo.transform.Find("Content/PlayerList");
            _queueList = lobbyGo.transform.Find("Queue/Content/QueueList");
            _PlayersLoaded = lobbyGo.transform.Find("Content/Players/Text (1)").GetComponent<TextMeshProUGUI>();
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
                    
                if(SteamLobbyManager.Inst.GetIsPlayerLoaded(friend.Id))
                {
                    SetPlayerLoaded(friend.Id, false); 
                }
            }
            
            UpdateQueue();
        }

        private void OnDestroy()
        {
            if (_playersList)
            {
                Destroy(_playersList.parent.parent.gameObject);
            }
        }

        public void AddPlayerToLobby(SteamId player, string playerName)
        {
            if (_playerList.ContainsKey(player))
            {
                return;
            }
            
            var playerEntry = Instantiate(_playerPrefab, _playersList).Cast<GameObject>().transform;

            if (_specialColors.TryGetValue(player, out var hex))
            {
                playerName = $"<color=#{hex}>{playerName}";
            }
            
            playerEntry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            _playerList.Add(player, playerEntry);

            _PlayersLoaded.text = "\u2591";
        }

        public void RemovePlayerFromLobby(SteamId player)
        {
            if (_playerList.TryGetValue(player, out var value))
            {
                Destroy(value.gameObject);
                _playerList.Remove(player);
                _PlayersLoaded.text = SteamLobbyManager.Inst.IsEveryoneLoaded ? "▓" : "\u2591";
            }
        }

        public void SetPlayerLoaded(SteamId player, bool playSound = true)
        {
            if (_playerList.TryGetValue(player, out var value))
            {
                if (playSound)
                {
                    AudioManager.Inst?.PlaySound("Add", 1);
                }
                value.GetChild(1).GetComponent<TextMeshProUGUI>().text = "▓";
                
                _PlayersLoaded.text = SteamLobbyManager.Inst.IsEveryoneLoaded ? "▓" : "\u2591";
            }
        }

        public void StartLevel()
        {
            GlobalsManager.HasStarted = true;
            SteamLobbyManager.Inst.HideLobby();
            pauseMenu.UnPause();
        }

        public void UpdateQueue()
        {
            for (int i = 0; i < _queueList.childCount; i++)
            {
                Destroy(_queueList.GetChild(i).gameObject);
            }
            List<string> queue = JsonConvert.DeserializeObject<List<string>>(SteamLobbyManager.Inst.CurrentLobby.GetData("LevelQueue"));

            if (queue.Count == 0)
            {
                _queueList.parent.parent.gameObject.SetActive(false);
                return;
            }
            _queueList.parent.parent.gameObject.SetActive(true);
            foreach (var queueEntry in queue)
            {
                string text = queueEntry;
                if (queueEntry.Length > 21)
                {
                    text = queueEntry.Substring(0, 19) + "...";
                }
                var entry = Instantiate(_queueEntryPrefab, _queueList).Cast<GameObject>();
                entry.GetComponentInChildren<TextMeshProUGUI>().text = text;
            }
        }
    }
}
