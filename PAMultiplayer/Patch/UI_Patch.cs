using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using AttributeNetworkWrapperV2;
using Crosstales;
using PaApi;
using UnityEngine;
using PAMultiplayer.Managers;
using SimpleJSON;
using TMPro;
using UnityEngine.Events;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Networking;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch
{
  
    /// <summary>
    /// adds the Multiplayer button to the UI
    /// </summary>
    [HarmonyPatch(typeof(ModifiersManager))]
    public static class UI_Patch
    {
        [HarmonyPatch(nameof(ModifiersManager.Start))]
        [HarmonyPostfix]
        static void AddUIToSettings(ref ModifiersManager __instance)
        {
            var hiddenButtons = __instance.transform.parent.Find("Buttons/Primary/Multiplayer").gameObject;
            hiddenButtons.SetActive(true);
            var mpToggle = hiddenButtons.GetComponent<MultiElementToggle>();
            mpToggle.isOn = false;
            mpToggle.interactable = true;
            mpToggle.onValueChanged = new Toggle.ToggleEvent();
            mpToggle.onValueChanged.AddListener(_ =>
            {
                mpToggle.isOn = false;
                if(LobbyCreationManager.Instance) 
                    LobbyCreationManager.Instance.OpenMenu(false);
            });
            
            mpToggle.GetComponentInChildren<TextMeshProUGUI>().richText = true;
            
            if (LobbyCreationManager.Instance)
            {
                LobbyCreationManager.Instance.FallbackUIElement = mpToggle;
            }
            MultiElementButton playgame = __instance.transform.parent.Find("Buttons/Primary/Play").GetComponent<MultiElementButton>();
 
            //playgame.onClick = new Button.ButtonClickedEvent();
            playgame.onClick.AddListener(() =>
            {
                if (GlobalsManager.Queue.Count > 0)
                {
                    string id = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo != null ? ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID.ToString() : ArcadeManager.Inst.CurrentArcadeLevel.name;
                    if (!GlobalsManager.Queue.Contains(id))
                        GlobalsManager.Queue.Add(id);

                    id = GlobalsManager.Queue[0];
                    GlobalsManager.LevelId = id;
                    ArcadeManager.Inst.CurrentArcadeLevel =
                        ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
                    
                    //SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                }   
            });
           
            var row1 = __instance.transform.Find("r-2");
            var mpRow = Object.Instantiate(row1, __instance.transform);
            for (int i = 0; i <   mpRow.childCount; i++)
            {
                Object.Destroy(mpRow.GetChild(i).gameObject);
            }
          
            var linkedHealthToggle = Object.Instantiate(row1.GetChild(0), mpRow).GetComponent<MultiElementToggle>();
            var modText = linkedHealthToggle.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            
            UIStateManager.Inst.RefreshTextCache(modText, "<size=75%><sprite name=\"heart\"><size=100%>Linked Health");
            modText.text = "<size=75%><sprite name=\"heart\"><size=100%>Linked Health";
            modText.richText = true;
            linkedHealthToggle.name = "mp_linkedHealth";
            
            linkedHealthToggle.onValueChanged = new();
            linkedHealthToggle.onValueChanged.AddListener(on =>
            {
                DataManager.inst.UpdateSettingBool("mp_linkedHealth", on);
            });
            
            DataManager.inst.UpdateSettingBool("mp_linkedHealth", false);
            linkedHealthToggle.isOn = false;
            linkedHealthToggle.wasOn = false;
            
            //adds to the 'Song Menu' page so it plays the glitch effect on this toggle 
            Object.FindFirstObjectByType<UI_Book>().Pages[1].SubElements.Add(linkedHealthToggle.uiElement);
        }
    }

    [HarmonyPatch(typeof(PauseUIManager))]
    public static class PauseMenuPatch
    {
        [HarmonyPatch(nameof(PauseUIManager.RestartLevel))]
        [HarmonyPrefix]
        static bool PreRestartLevel()
        {
            return !GlobalsManager.IsMultiplayer;
        }

        
        //instantiates the mp screens in the General_UI scene
        //this, or other any class on this scene has no startup function to patch
        [HarmonyPatch(nameof(PauseUIManager.Update))]
        [HarmonyPostfix]
        static void PostUpdate()
        {
            if (LobbyCreationManager.Instance != null)
                return;

            Transform generalUI = PauseUIManager.Inst.transform.parent;
            
            GameObject lobbySettingsGo;
            GameObject selectionMenuGo;
            using (var stream = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("PAMultiplayer.Assets.lobbysettings"))
            {
                var lobbyBundle = AssetBundle.LoadFromMemory(stream!.CTReadFully());
                lobbySettingsGo = Object.Instantiate(lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[0]) as GameObject,
                    generalUI);
          
                selectionMenuGo = Object.Instantiate(lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[1]) as GameObject,
                    generalUI);
                
                lobbyBundle.Unload(false);
            }
        
            lobbySettingsGo.name = "LobbySettings";
            lobbySettingsGo.AddComponent<LobbyCreationManager>();
            
            selectionMenuGo.name = "SelectionMenu";
            selectionMenuGo.AddComponent<MenuSelectionManager>();
        }
    }
    
      //the reason there's both unpause functions here, its cuz the UI unpause calls PauseMenu.UnPause() and pressing ESC calls GameManager.UnPause().
    [HarmonyPatch]
    public partial class PauseLobbyPatch
    {
        [HarmonyPatch(typeof(PauseUIManager), nameof(PauseUIManager.CloseWithEffects))]
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
                    string text = currentLobbyMember.Name;

                    if (LobbyScreenManager.SpecialColors.TryGetValue(currentLobbyMember.Id, out var hex))
                    {
                        text = $"<color=#{hex}>{currentLobbyMember.Name}";
                    }

                    if (currentLobbyMember.Id == GlobalsManager.LocalPlayerId)
                    {
                        text = "YOU";
                        if (!ChallengeManager.Inst && player.VGPlayerData.PlayerObject)
                        {
                            GameManager.Inst.StartCoroutine(ShowDecay(player.VGPlayerData.PlayerObject));
                        }
                    }

                    //band-aid fix for an error here

                    if (player.VGPlayerData.PlayerObject && player.VGPlayerData.PlayerObject.Player_Text)
                    {
                        player.VGPlayerData.PlayerObject.Player_Text.DisplayText(text, 3);
                    }
                }
            }
        }

        public static IEnumerator ShowDecay(VGPlayer player)
        {
            player.ChangeAnimationState(VGPlayer.ANIM_HURT);
            
            yield return new WaitForSeconds(3);
            
            if(player && player.isHurting == 0)
                player.ChangeAnimationState(VGPlayer.ANIM_IDLE);
        }
        
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.UnPause))]
        [HarmonyPrefix]
        static bool PreGameUnpause(ref GameManager __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;
            
            if (!GlobalsManager.HasStarted && (!GlobalsManager.IsHosting || !SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return false;
            }

            __instance.Paused = false;
            
            if (GlobalsManager.IsHosting)
            {
                CallRpc_Multi_StartLevel();
            }

            if (LobbyScreenManager.Instance)
            {
                SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
                
                GameManager.Inst.StartCoroutine(ShowNames());
                
                LobbyScreenManager.Instance.StartLevel();
                Object.Destroy(LobbyScreenManager.Instance, 1f);
            }
            CameraDB.Inst.SetUIVolumeWeightOut(0.2f);
            return true;
        }
        
        [MultiRpc]
        public static void Multi_StartLevel()
        {
            if (GlobalsManager.IsHosting)
            {
                return;
            }
            
            if (LobbyScreenManager.Instance)
            {
                LobbyScreenManager.Instance.StartLevel();
            }

            if (ChallengeManager.Inst)
            {
                ChallengeManager.Inst.StartVoting_Client();
            }
        }
    }
    

    /// <summary>
    /// adds an "Update Mod" button in case a new version is available
    /// and replaces the Player hit and Player warp settings with a multiplayer version of those settings
    /// </summary>

    [HarmonyPatch(typeof(ShowChangeLog))]
    public static class UpdateModButton
    {
        [HarmonyPatch(nameof(ShowChangeLog.Start))]
        [HarmonyPrefix]
        static bool PostStart(ShowChangeLog __instance)
        {
            UI_Book book = __instance.transform.parent.parent.parent.Find("Settings").GetComponent<UI_Book>();
            
            void InstantiateSlider(GameObject prefab, Transform parent, string label, float value, UnityAction<float> setter)
            {
                GameObject WarpSliderObj = Object.Instantiate(prefab, parent);
            
                UI_Slider slider = WarpSliderObj.GetComponent<UI_Slider>();
                slider.DataID = null;
                slider.DataIDType = UI_Slider.DataType.Enum;
                slider.Type = UI_Slider.VisualType.dot;
                slider.Range = new Vector2(0, 2);
                slider.Values = new[] { "All Players", "Local player Only", "None" };
                slider.Value = value;
                slider.Label.text = label;  
                slider.Label.GetComponentInChildren<GameObjectLocalizer>().enabled = false;
                slider.OnValueChanged.AddListener(setter);
               
                UIStateManager.inst.RefreshTextCache(slider.Label, label);
                slider.SetLocalization(slider.Label, PAM.Guid, label, label);
                
                book.Pages[1].SubElements.Add(slider);
            }
            
            SystemManager.inst.StartCoroutine(FetchGithubReleases(__instance.gameObject));
          
            GameObject sliderPrefab = book.transform.Find("Audio/Right/Music").gameObject;
            Transform audioParent = sliderPrefab.transform.parent;
           
            //destroy SFX toggles
            Object.Destroy(audioParent.GetChild(audioParent.childCount-1).gameObject);
            Object.Destroy(audioParent.GetChild(audioParent.childCount-2).gameObject);
        
            InstantiateSlider(sliderPrefab, audioParent, "Player Hit SFX", Settings.HitSfx.Value, x =>
            {
                Settings.HitSfx.Value = (int)x;
                DataManager.inst.UpdateSettingBool("PlayerSFX", x != 2);
            });
            InstantiateSlider(sliderPrefab, audioParent, "Player Hit Warp SFX", Settings.WarpSfx.Value, x =>
            {
                Settings.WarpSfx.Value = (int)x;
                DataManager.inst.UpdateSettingBool("PlayerWarpSFX", x != 2);
            });
           
            MultiElementButton button = __instance.transform.parent.parent.Find("pc_top-buttons/Custom Mode")
                .GetComponent<MultiElementButton>();

            button.onClick = new();
            button.onClick.AddListener(() =>
            {
                MenuSelectionManager.Instance.OpenMenu();
            });
            
            if (!SingletonBase<SettingsManager>.Inst.ShowChangeLog())
            {
                __instance.gameObject.SetActive(false);
            }
          
            return false;
        }

        const string UpdateStr = "<sprite name=info> Update Multiplayer";
        static IEnumerator FetchGithubReleases(GameObject changeLog)
        {
            UnityWebRequest request =
                UnityWebRequest.Get("https://api.github.com/repos/Aiden-ytarame/PAMultiplayer/releases/latest");
            
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                PAM.Logger.LogError("Failed to fetch Github Release, oof");
                request.Dispose();
                yield break;
            }

            JSONNode latestRelease = JSON.Parse(request.downloadHandler.text);

            request.Dispose();
            
            bool isLatest = Version.Parse(latestRelease["tag_name"].Value.Substring(1)).CompareTo(Version.Parse(PAM.Version)) <= 0;

            if (isLatest)
            {
                PAM.Logger.LogInfo("Got Latest Version");
                yield break;
            }

            PAM.Logger.LogWarning("New Mp Version Available!");
            
            GameObject updateMod = Object.Instantiate(changeLog, changeLog.transform.parent).gameObject;
            updateMod.name = "Update MP";
            updateMod.SetActive(true);
            updateMod.GetComponent<ShowChangeLog>().enabled = false;
            TextMeshProUGUI updateText = updateMod.GetComponentInChildren<TextMeshProUGUI>();
            
            updateText.text = UpdateStr;
            UIStateManager.Inst.RefreshTextCache(updateText, UpdateStr);
            
            var button = updateMod.GetComponent<MultiElementButton>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
               Application.OpenURL("https://thunderstore.io/c/project-arrhythmia/p/aiden_ytarame/Project_Arrhythmia_Multiplayer/");
            });
            
            
            //stupid workaround to getting the wrong canvas
            GameObject.Find("Canvas/Window").transform.parent.GetComponent<UI_Book>().Pages[0].SubElements
                .Add(updateMod.GetComponent<UI_Button>());
        }

        
    }
    /// <summary>
    /// prevents the changelog button from destroying itself, so we can copy it for the Update Button
    /// </summary>
    [HarmonyPatch(typeof(ShowChangeLog))]
    public static class ChangelogsPatch
    {
        [HarmonyPatch(nameof(ShowChangeLog.Start))]
        [HarmonyPrefix]
        static bool PreStart(ShowChangeLog __instance)
        {
            if (!SettingsManager.Inst.ShowChangeLog() && __instance.name == "Changelog")
            {
                __instance.gameObject.SetActive(false);
            }
            return false;
        }
    }
}
