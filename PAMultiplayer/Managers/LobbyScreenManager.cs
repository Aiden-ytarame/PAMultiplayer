using System.Collections.Generic;
using System.Reflection;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Managers
{

    [HarmonyPatch(typeof(PauseMenu))]
    public class PauseLobbyPatch
    {
        [HarmonyPatch(nameof(PauseMenu.UnPause))]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!GlobalsManager.IsMultiplayer) return true;
            
            if(LobbyScreenManager.Instance.shouldStart ||
                (GlobalsManager.IsHosting))
            {
                if (GlobalsManager.IsHosting)
                    SteamManager.Inst.Server.StartLevel();
                LobbyScreenManager.Instance.shouldStart = false;
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(GameManager))]
    public class GMPauseLobbyPatch
    {
        [HarmonyPatch(nameof(GameManager.UnPause))]
        [HarmonyPrefix]
        public static void Postfix()
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            if (LobbyScreenManager.Instance)
            {
                if (GlobalsManager.IsHosting)
                    SteamManager.Inst.Server.StartLevel();
                
                VGPlayerManager.inst.RespawnPlayers();
                Object.Destroy(LobbyScreenManager.Instance);
            }
        }
    }

    public class LobbyScreenManager : MonoBehaviour
    {
        public static LobbyScreenManager Instance { get; private set; }
        public bool shouldStart = false;
        readonly Dictionary<SteamId, Transform> _playerList = new();
        Transform _playersListGo;
        public PauseMenu pauseMenu;
        Object _playerPrefab;

        void Awake()
        {
            Instance = this;
            VGCursor.Inst.ShowCursor();
            GameObject playerGUI = GameObject.Find("Player GUI");
            
            //this is for when I bundle the assets into the dll.
            
            GameObject lobbyGo;
            
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PAMultiplayer.Assets.lobby menu"))
            {
                var lobbyBundle = AssetBundle.LoadFromMemory(stream.ReadBytes());
                
                var lobbyPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]);
                _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]);
                var lobbyObj = Instantiate(lobbyPrefab, playerGUI.transform);
                lobbyObj.name = "PAM_Lobby";

                lobbyGo = lobbyObj.Cast<GameObject>();
                lobbyBundle.Unload(false);
            }
            
            pauseMenu = lobbyGo.GetComponent<PauseMenu>();
            _playersListGo = lobbyGo.transform.Find("Content/PlayerList");
         
            if (GlobalsManager.IsHosting)
            {
                var buttons = lobbyGo.transform.Find("Content/buttons");
                buttons.GetChild(1).GetComponent<MultiElementButton>().onClick.AddListener(new System.Action(() =>
                {
                    SceneLoader.Inst.LoadSceneGroup("Arcade");
                }));
                buttons.GetChild(2).GetComponent<MultiElementButton>().onClick.AddListener(new System.Action(() =>
                {
                    SceneLoader.Inst.LoadSceneGroup("Menu");
                }));
                
                if (!SteamLobbyManager.Inst.InLobby)
                {
                    lobbyGo.transform.Find("Content/buttons").gameObject.SetActive(false);
                    lobbyGo.transform.Find("Content/LobbyFailed").gameObject.SetActive(true);
                    lobbyGo.transform.Find("Content/LobbyFailed").GetComponentInChildren<MultiElementButton>().onClick.AddListener(new System.Action(
                        () =>
                        {
                            Plugin.Logger.LogDebug("RETRY");
                            GlobalsManager.IsReloadingLobby = true;
                            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                        }));
                }
            }
            else //this is for the Waiting for host Message
            {
                lobbyGo.transform.Find("Content/buttons").gameObject.SetActive(false);
                lobbyGo.transform.Find("Content/WaitingForHost").gameObject.SetActive(true);
            }
            
            var Enu = SteamLobbyManager.Inst.CurrentLobby.Members.GetEnumerator();
            while(Enu.MoveNext())
            {
                AddPlayerToLobby(Enu.Current.Id, Enu.Current.Name);
                
                //this means that when you join a lobby
                //every player that joined before you will get shown as Loaded, even if they're not. 
                //it's easier than send if the player loaded or not to every new client.
                SetPlayerLoaded(Enu.Current.Id);
               
            }
            Enu.Dispose();
            
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
            var playerEntry = Instantiate(_playerPrefab, _playersListGo.transform).Cast<GameObject>().transform;
            playerEntry.name = $"PAM_Player {player}";
            
            playerEntry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            _playerList.Add(player, playerEntry);

        }

        public void RemovePlayerFromLobby(SteamId player)
        {
            Destroy(_playerList[player].gameObject);
            _playerList.Remove(player);
        }

        public void SetPlayerLoaded(SteamId player)
        {
            Transform entry = _playerList[player];
            if(entry)
                entry.GetChild(1).GetComponent<TextMeshProUGUI>().text = "▓";           
        }

        public void StartLevel()
        {
            shouldStart = true;
            SteamLobbyManager.Inst.HideLobby();
            pauseMenu.UnPause();
        }
    }
}
