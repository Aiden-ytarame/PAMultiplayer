using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(GameManager2))]
    public class GM_Lobby_Patch
    {
        [HarmonyPatch(nameof(GameManager2.PlayGame))]
        [HarmonyPostfix]
        public static void Postfix(ref GameManager2 __instance)
        {
            if(StaticManager.IsMultiplayer)
            {
                __instance.Pause(false);
                 __instance.gameObject.AddComponent<LobbyManager>();
            }
            
        }
    }
    public class LobbyManager : MonoBehaviour
    {
        static LobbyManager instance;

        Dictionary<string, GameObject> PlayerList = new Dictionary<string, GameObject>();
        GameObject PlayersListGO;
        UnityEngine.Object PlayerPrefab;
        void Start()
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
            PlayersListGO = PlayerGUI.transform.Find("PAM_Lobby").GetChild(1).GetChild(4).gameObject; //eh I could do the Find() directly to the correct object.


            if (!DataManager.inst.GetSettingBool("online_host"))
            {
                if (StaticManager.ServerIp == "")
                {
                    
                    //Delete the Buttons for clients.
                }
            }

            lobbyBundle.Unload(false);
        }

        void OnDisable()
        {
            StaticManager.IsLobby = false;
            instance = null;
        }

        public static void AddPlayerToLobby(string player)
        {
            if (instance == null)
                return;

            var playerEntry = GameObject.Instantiate(instance.PlayerPrefab, instance.PlayersListGO.transform);
            playerEntry.name = $"PAM_Player {player}";

            GameObject entry = instance.PlayersListGO.transform.Find($"PAM_Player {player}").gameObject;
            entry.GetComponentInChildren<TextMeshProUGUI>().text = player;
            instance.PlayerList.Add(player, entry);
        }

        public static void RemovePlayerFromLobby(string player)
        {
            if (instance == null)
                return;

            GameObject entry = instance.PlayersListGO.transform.Find($"PAM_Player {player}").gameObject;
            Destroy(entry);

            instance.PlayerList.Remove(player);
        }

    }
}
