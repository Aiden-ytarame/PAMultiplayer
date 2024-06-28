using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Managers
{
    [HarmonyPatch(typeof(GameManager))]
    public class GmLobbyPatch
    {
        [HarmonyPatch(nameof(GameManager.PlayGame))]
        [HarmonyPostfix]
        public static void Postfix(ref GameManager __instance)
        {
            if (StaticManager.IsMultiplayer)
            {
                __instance.Pause(false);
                __instance.gameObject.AddComponent<LobbyManager>();
                
                if(StaticManager.IsHosting)
                    SteamManager.Inst.Server?.SendHostLoaded();
                else 
                    SteamManager.Inst.Client?.SendLoaded();
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenu))]
    public class PauseLobbyPatch
    {
        [HarmonyPatch(nameof(PauseMenu.UnPause))]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (LobbyManager.Instance.shouldStart || (StaticManager.IsHosting && SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return true;
            }
            return false;
        }


        [HarmonyPatch(nameof(PauseMenu.UnPause))]
        [HarmonyPostfix]
        public static void Postfix(ref PauseMenu __instance)
        {
            if (LobbyManager.Instance && SteamLobbyManager.Inst.IsEveryoneLoaded)
            {  
                Object.Destroy(LobbyManager.Instance);
                VGPlayerManager.inst.RespawnPlayers();

                if (StaticManager.IsHosting)
                    SteamManager.Inst.Server.StartLevel();
            }
        }
    }
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }
        public bool shouldStart = false;
        readonly Dictionary<SteamId, Transform> _playerList = new();
        Transform _playersListGo;
        public PauseMenu pauseMenu;
        Object _playerPrefab;

        void Awake()
        {
            Instance = this;
            VGCursor.ShowCursor();
            GameObject playerGUI = GameObject.Find("Player GUI");
            var lobbyBundle = AssetBundle.LoadFromFile(Directory.GetFiles(Paths.PluginPath, "lobby", SearchOption.AllDirectories)[0]);

            var lobbyPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]);
            _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]);
            var lobbyObj = Instantiate(lobbyPrefab, playerGUI.transform);
            lobbyObj.name = "PAM_Lobby";

            //again, if I can cast UnityEngine.Object to GameObject please tell me :)

            var lobbyGo = playerGUI.transform.Find("PAM_Lobby").gameObject;
            pauseMenu = lobbyGo.GetComponent<PauseMenu>();

            _playersListGo = lobbyGo.transform.Find("Content/PlayerList");
         
            if (StaticManager.IsHosting)
            {
                if (!SteamLobbyManager.Inst.InLobby)
                {
                    lobbyGo.transform.Find("Content/buttons").gameObject.SetActive(false);
                    lobbyGo.transform.Find("Content/LobbyFailed").gameObject.SetActive(true);
                    lobbyGo.transform.Find("Content/LobbyFailed").GetComponentInChildren<MultiElementButton>().onClick.AddListener(new System.Action(
                        () =>
                        {
                            Plugin.Logger.LogDebug("RETRY");
                            StaticManager.IsReloadingLobby = true;
                            SceneManager.Inst.LoadScene("Arcade Level");
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

            lobbyBundle.Unload(false);
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
            var playerEntry = Instantiate(_playerPrefab, _playersListGo.transform);
            playerEntry.name = $"PAM_Player {player}";

            Transform entry = _playersListGo.Find($"PAM_Player {player}");
            entry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            _playerList.Add(player, entry);
        }

        public void RemovePlayerFromLobby(SteamId player)
        {
            Destroy(_playerList[player]);
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
            pauseMenu.UnPause();
        }
    }
}
