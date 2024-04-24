using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PAMultiplayer.Patch
{

    //this logic has to be ReWritten. if the client doesnt recive confirmation that we're on a lobby. it will start without checking.
    //could fix by being Lobby by default, and on LocalPlayer packet call this code again in case of lobby being false on server.
    [HarmonyPatch(typeof(GameManager2))]
    public class GM_Lobby_Patch
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
    public class Pause_Lobby_Patch
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
            if (LobbyManager.instance && StaticManager.LobbyInfo.isEveryoneLoaded)
            {  
                StaticManager.IsLobby = false;
                Object.Destroy(__instance.gameObject);
                Object.Destroy(LobbyManager.instance);
                VGPlayerManager.inst.RespawnPlayers();

                if (StaticManager.IsHosting)
                    StaticManager.Server.SendStartLevel();
            }
        }
    }
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager instance { get; private set; }

        Dictionary<string, Transform> PlayerList = new Dictionary<string, Transform>();
        Transform PlayersListGO;
        PauseMenu pauseMenu;
        UnityEngine.Object PlayerPrefab;

        void Awake()
        {
            instance = this;
            StaticManager.IsLobby = true;

            GameObject PlayerGUI = GameObject.Find("Player GUI");
            var lobbyBundle = AssetBundle.LoadFromFile($"{Paths.PluginPath}\\PAMultiplayer\\Assets\\lobby");

            var lobbyPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]);
            PlayerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]);
            var lobbyObj = GameObject.Instantiate(lobbyPrefab, PlayerGUI.transform);
            lobbyObj.name = "PAM_Lobby";

            //again, if I can cast UnityEngine.Object to GameObject please tell me :)

            var LobbyGO = PlayerGUI.transform.Find("PAM_Lobby").gameObject;
            pauseMenu = LobbyGO.GetComponent<PauseMenu>();
            PlayersListGO = LobbyGO.transform.GetChild(1).GetChild(5); //eh I could do the Find() directly to the correct object.
         
            if (!StaticManager.IsHosting)
            {
                LobbyGO.transform.GetChild(1).GetChild(3).gameObject.SetActive(false);
                LobbyGO.transform.GetChild(1).GetChild(2).gameObject.SetActive(true);
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
            var playerEntry = GameObject.Instantiate(PlayerPrefab, PlayersListGO.transform);
            playerEntry.name = $"PAM_Player {player}";

            Transform entry = PlayersListGO.Find($"PAM_Player {player}");
            entry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
            PlayerList.Add(player, entry);
        }

        public void RemovePlayerFromLobby(string player)
        {
            Transform entry = PlayersListGO.transform.Find($"PAM_Player {player}");
            Destroy(entry);

            PlayerList.Remove(player);
        }

        public void SetPlayerLoaded(string player)
        {
            Transform entry = PlayersListGO.Find($"PAM_Player {player}");
            if(entry)
                entry.GetChild(1).GetComponent<TextMeshProUGUI>().text = "▓";           
        }

        public void StartLevel()
        {
            StaticManager.IsLobby = false;
            pauseMenu.UnPause();
        }
    }
}
