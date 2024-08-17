using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using Steamworks;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Managers
{
    
    //the reason there's both unpause functions here, its cuz the UI unpause calls PauseMenu.UnPause() and pressing ESC calls GameManager.UnPause().
    [HarmonyPatch]
    public class PauseLobbyPatch
    {
        [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.UnPause))]
        [HarmonyPrefix]
        static bool PreMenuUnpause()
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            if (GlobalsManager.HasStarted || (GlobalsManager.IsHosting && SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return true;
            }

            return false;
        }

        public static IEnumerator ShowNames()
        {
            //stupid hack lmao
            yield return new WaitForUpdate();
            
            foreach (var currentLobbyMember in SteamLobbyManager.Inst.CurrentLobby.Members)
            {
                if (GlobalsManager.Players.TryGetValue(currentLobbyMember.Id, out var player))
                {
                    string text = "YOU";
                    if (currentLobbyMember.Id != GlobalsManager.LocalPlayer)
                    {
                        text = currentLobbyMember.Name;
                    }
                    
                    //band-aid fix for an error here
                    try
                    {
                        player.PlayerObject?.SpeechBubble?.DisplayText(text, 3);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.UnPause))]
        [HarmonyPrefix]
        static bool PreGameUnpause()
        {
            if (!GlobalsManager.IsMultiplayer) return true;
            
            if (!GlobalsManager.HasStarted && (!GlobalsManager.IsHosting || !SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return false;
            }
            if (GlobalsManager.IsHosting)
            {
                SteamManager.Inst.Server.StartLevel();
            }

            if (LobbyScreenManager.Instance)
            {
                Plugin.Logger.LogError(GlobalsManager.Players.Count);
                Plugin.Logger.LogError(SteamLobbyManager.Inst.CurrentLobby.MemberCount);
                Plugin.Logger.LogError(VGPlayerManager.Inst.players.Count);
                
                VGPlayerManager.inst.RespawnPlayers();
                GameManager.Inst.StartCoroutine(ShowNames().WrapToIl2Cpp());
                Object.Destroy(LobbyScreenManager.Instance);
            }
            
            return true;
        }
    }

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
            { 76561199141999343, "00ffd0" }, //Aiden 00ffd0
            { 76561199106356594, "34eb67" }, //yikxle 34eb67 
            { 76561199088465180, "7300ff" }  //Cube
        };
        Transform _playersListGo;
        Object _playerPrefab;

        //spawns the lobby GameObject from the assetBundle
        void Awake()
        {
            Instance = this;
            VGCursor.Inst.ShowCursor();
            GameObject playerGUI = GameObject.Find("Player GUI");
            
            //this is for when I bundle the assets into the dll.
            
            GameObject lobbyGo;
            
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PAMultiplayer.Assets.lobby menu"))
            {
                var lobbyBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
                var lobbyPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]);
                _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]);
                var lobbyObj = Instantiate(lobbyPrefab, playerGUI.transform);
                lobbyObj.name = "PAM_Lobby";

                lobbyGo = lobbyObj.Cast<GameObject>();
                lobbyBundle.Unload(false);
            }
            
            pauseMenu = lobbyGo.GetComponent<PauseMenu>();
            _playersListGo = lobbyGo.transform.Find("Content/PlayerList");
         
            //handles what should appear on the screen
            //like the buttons for the host
            //Waiting for host message for clients
            //or Lobby Failed message
            if (GlobalsManager.IsHosting)
            {
                var buttons = lobbyGo.transform.Find("Content/buttons");
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
            
            var enu = SteamLobbyManager.Inst.CurrentLobby.Members.GetEnumerator();
            while(enu.MoveNext())
            {
                AddPlayerToLobby(enu.Current.Id, enu.Current.Name);
                
                //this means that when you join a lobby
                //every player that joined before you will get shown as Loaded, even if they're not. 
                //it's easier than send if the player loaded or not to every new client.
                SetPlayerLoaded(enu.Current.Id);
               
            }
            enu.Dispose();
            
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

            if (_specialColors.TryGetValue(player, out var hex))
            {
                playerName = $"<color=#{hex}>{playerName}";
            }
            
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
            GlobalsManager.HasStarted = true;
            SteamLobbyManager.Inst.HideLobby();
            pauseMenu.UnPause();
        }
    }
}
