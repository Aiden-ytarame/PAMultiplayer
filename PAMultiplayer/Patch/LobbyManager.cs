using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace PAMultiplayer.Patch
{

    //this logic has to be ReWritten. if the client doesnt recive confirmation that we're on a lobby. it will start without checking.
    //could fix by being Lobby by default, and on LocalPlayer packet call this code again in case of lobby being false on server.
    [HarmonyPatch(typeof(GameManager2))]
    public class GmLobbyPatch
    {
        [HarmonyPatch(nameof(GameManager2.PlayGame))]
        [HarmonyPostfix]
        public static void Postfix(ref GameManager2 __instance)
        {
            if (StaticManager.IsMultiplayer && (StaticManager.IsLobby || StaticManager.IsHosting))
            {
                __instance.Pause(false);
                __instance.gameObject.AddComponent<LobbyManager>();
                StaticManager.Client.SendLoaded();
            }         
        }

        [HarmonyPatch(nameof(GameManager2.UnPause))]
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!StaticManager.IsLobby || (StaticManager.IsHosting && StaticManager.LobbyInfo.isEveryoneLoaded))
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
            if (!StaticManager.IsLobby || (StaticManager.IsHosting && StaticManager.LobbyInfo.isEveryoneLoaded))
            {
                return true;
            }
            return false;
        }


        [HarmonyPatch(nameof(PauseMenu.UnPause))]
        [HarmonyPostfix]
        public static void Postfix(ref PauseMenu __instance)
        {
            if (LobbyManager.Instance && StaticManager.LobbyInfo.isEveryoneLoaded)
            {  
                StaticManager.IsLobby = false;
                Object.Destroy(__instance.gameObject);
                Object.Destroy(LobbyManager.Instance);
                VGPlayerManager.inst.RespawnPlayers();

                if (StaticManager.IsHosting)
                    StaticManager.Server.SendStartLevel();
            }
        }
    }
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        readonly Dictionary<string, Transform> _playerList = new Dictionary<string, Transform>();
        Transform _playersListGo;
        PauseMenu _pauseMenu;
        UnityEngine.Object _playerPrefab;

        void Awake()
        {
            Instance = this;
            StaticManager.IsLobby = true;

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

            var Enu = StaticManager.LobbyInfo.PlayerDisplayName.GetEnumerator();
            while(Enu.MoveNext())
            {
                AddPlayerToLobby(Enu.Current.Key, Enu.Current.Value);
                //this is weird, this means that when you join a lobby
                //every player that joined before you will get shown as Loaded, even if theyre not. 
                //its easier than send if the player loaded or not to every new client.
                SetPlayerLoaded(Enu.Current.Key); 
            }
            Enu.Dispose();

            lobbyBundle.Unload(false);
        }

        public void AddPlayerToLobby(string player, string playerName)
        {
            var playerEntry = GameObject.Instantiate(_playerPrefab, _playersListGo.transform);
            playerEntry.name = $"PAM_Player {player}";

            Transform entry = _playersListGo.Find($"PAM_Player {player}");
            entry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            _playerList.Add(player, entry);
        }

        public void RemovePlayerFromLobby(string player)
        {
            Transform entry = _playersListGo.transform.Find($"PAM_Player {player}");
            Destroy(entry);

            _playerList.Remove(player);
        }

        public void SetPlayerLoaded(string player)
        {
            Transform entry = _playersListGo.Find($"PAM_Player {player}");
            if(entry)
                entry.GetChild(1).GetComponent<TextMeshProUGUI>().text = "▓";           
        }

        public void StartLevel()
        {
            StaticManager.IsLobby = false;
            _pauseMenu.UnPause();
        }
    }
}
