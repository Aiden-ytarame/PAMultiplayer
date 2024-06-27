using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PAMultiplayer.Managers
{
    [HarmonyPatch(typeof(GameManager))]
    public class GmLobbyPatch
    {
        [HarmonyPatch(nameof(GameManager.PlayGame))]
        [HarmonyPostfix]
        public static void Postfix(ref GameManager __instance)
        {
            if (StaticManager.IsMultiplayer && StaticManager.IsHosting)
            {
                __instance.Pause(false);
                __instance.gameObject.AddComponent<LobbyManager>();
                SteamManager.Inst.Client?.SendLoaded();
            }         
        }

        [HarmonyPatch(nameof(GameManager.UnPause))]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return true;
            if (StaticManager.IsHosting && SteamLobbyManager.Inst.IsEveryoneLoaded)
            {
                return true;
            }
            return false;
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
                Object.Destroy(__instance.gameObject); //why did I do this?
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
        Dictionary<SteamId, bool> _loadedPlayers = new();
        Transform _playersListGo;
        PauseMenu _pauseMenu;
        Object _playerPrefab;

        void Awake()
        {
            Instance = this;
            VGCursor.ShowCursor();
            GameObject playerGUI = GameObject.Find("Player GUI");
            var lobbyBundle = AssetBundle.LoadFromFile(Directory.GetFiles(Paths.PluginPath, "lobby", SearchOption.AllDirectories)[0]);

            var lobbyPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]);
            _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]);
            var lobbyObj = GameObject.Instantiate(lobbyPrefab, playerGUI.transform);
            lobbyObj.name = "PAM_Lobby";

            //again, if I can cast UnityEngine.Object to GameObject please tell me :)

            var lobbyGo = playerGUI.transform.Find("PAM_Lobby").gameObject;
            _pauseMenu = lobbyGo.GetComponent<PauseMenu>();
            _playersListGo = lobbyGo.transform.GetChild(1).GetChild(5); //eh I could do the Find() directly to the correct object.
         
            if (!StaticManager.IsHosting)
            {
                lobbyGo.transform.GetChild(1).GetChild(3).gameObject.SetActive(false);
                lobbyGo.transform.GetChild(1).GetChild(2).gameObject.SetActive(true);
            }

            var Enu = SteamLobbyManager.Inst.CurrentLobby.Members.GetEnumerator();
            while(Enu.MoveNext())
            {
                AddPlayerToLobby(Enu.Current.Id, Enu.Current.Name);
                //this is weird, this means that when you join a lobby
                //every player that joined before you will get shown as Loaded, even if theyre not. 
                //its easier than send if the player loaded or not to every new client.
                SetPlayerLoaded(Enu.Current.Id); 
            }
            Enu.Dispose();

            lobbyBundle.Unload(false);
        }

        public void AddPlayerToLobby(SteamId player, string playerName)
        {
            var playerEntry = GameObject.Instantiate(_playerPrefab, _playersListGo.transform);
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
            _pauseMenu.UnPause();
        }
    }
}
