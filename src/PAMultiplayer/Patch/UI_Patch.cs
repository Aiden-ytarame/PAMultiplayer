using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using AttributeNetworkWrapperV2;
using Crosstales;
using PaApi;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using UnityEngine;
using PAMultiplayer.Managers;
using PAMultiplayer.UI;
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
            LevelDetailPanel panel = __instance.transform.parent.parent.parent.parent.GetComponent<LevelDetailPanel>();
            
            var hiddenButtons = __instance.transform.Find("Multiplayer").gameObject;
            hiddenButtons.SetActive(true);
            hiddenButtons.transform.SetSiblingIndex(2);
            
            var mpButton = hiddenButtons.GetComponent<MultiElementButton>();
            mpButton.onClick.AddListener(() =>
            {
                if(LobbyCreationManager.Instance) 
                    LobbyCreationManager.Instance.OpenMenu(false, panel._currentLevel);
            });

            var uiElement = (mpButton.UIElement as UI_Button);
            if (uiElement)
            {
                uiElement.Text.text = "▶ Multiplayer";
                UIStateManager.Inst.RefreshTextCache(uiElement.Text, "▶ Multiplayer");
            }
            
            if (LobbyCreationManager.Instance)
            {
                LobbyCreationManager.Instance.FallbackUIElement = mpButton;
            }
            
            MultiElementButton playgame = __instance.transform.Find("Play").GetComponent<MultiElementButton>();
            
            playgame.onClick = new Button.ButtonClickedEvent();
            playgame.onClick.AddListener(() =>
            {
                GlobalsManager.PlayQueue(panel._currentLevel);
            });

            var modsParent = __instance.transform.parent.Find("mods");
            var modifierTemplate = modsParent.GetChild(1);
            var linkedHealthSlider = Object.Instantiate(modifierTemplate, modsParent).GetComponent<UI_Slider>();
            var modText = linkedHealthSlider.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            
            UIStateManager.Inst.RefreshTextCache(modText, "Linked Health");
            modText.text = "Linked Health";

            linkedHealthSlider.name = "mp_linkedHealth";
            linkedHealthSlider.OnValueChanged = new();
            linkedHealthSlider.OnValueChanged.AddListener(x =>
            {
                DataManager.inst.UpdateSettingBool("mp_linkedHealth", x == 1);
            });

            linkedHealthSlider.Values = ["Off", "On"];
            linkedHealthSlider.Range = new Vector2(0, 1);
            linkedHealthSlider.VisualRange = linkedHealthSlider.Range;
            
            linkedHealthSlider.DataID = null;
            linkedHealthSlider.DataIDType = UI_Slider.DataType.Runtime;

            linkedHealthSlider.Value = 0;
            linkedHealthSlider.ChangeAmount = 1;
            linkedHealthSlider.Type = UI_Slider.VisualType.line;
            linkedHealthSlider.originalNonLocalizedText.Clear();
            linkedHealthSlider.SetLocalization(linkedHealthSlider.Label, PAM.Guid, "mpLinkedHealth", "Linked Health");
            
            DataManager.inst.UpdateSettingBool("mp_linkedHealth", false);
            
            //adds to the 'Song Menu' page so it plays the glitch effect on this toggle 
            Object.FindFirstObjectByType<UI_Book>()
                .Pages[1].SubElements.Add(linkedHealthSlider);
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

            if (PaMNetworkManager.PamInstance?.LobbyInfo.HasStarted == true || GlobalsManager.IsChallenge || (GlobalsManager.IsHosting && SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return true;
            }

            return false;
        }

        public static IEnumerator ShowNames()
        {
            //stupid hack lmao
            yield return new WaitForUpdate();
            yield return new WaitForUpdate();
            
            foreach (var currentLobbyMember in SteamLobbyManager.Inst.CurrentLobby.Members)
            {
                if (GlobalsManager.Players.TryGetValue(currentLobbyMember.Id, out var player))
                {
                    string text = currentLobbyMember.Name;

                    if (currentLobbyMember.Id.IsLocalPlayer())
                    {
                        text = "YOU";
                        if (!ChallengeManager.Inst && player.VGPlayerData.PlayerObject)
                        {
                            GameManager.Inst.StartCoroutine(ShowDecay(player.VGPlayerData.PlayerObject));
                        }
                    }

                    //band-aid fix for an error here

                    PlayerText pt = player.VGPlayerData?.PlayerObject?.Player_Text;
                    if (!pt)
                    {
                        continue;
                    }
                    
                    pt.text.richText = !Settings.DisableRichText.Value;
                    pt.StopCoroutine("HideTextAfterTime");
                    pt.DisplayText(text);
                    pt.StartCoroutine(pt.HideTextAfterTime(3));

                    if (currentLobbyMember.Id.IsLocalPlayer() ||
                        !LobbyScreenManager.SpecialColors.TryGetValue(currentLobbyMember.Id, out var colors))
                    {
                        continue;
                    }

                    switch (colors.Length)
                    {
                        case 1:
                            pt.text.color = colors[0];
                            break;
                        case 2:
                            void SetGradient(TMP_TextInfo info)
                            {
                                pt.text.OnPreRenderText -= SetGradient;
                                MPUtility.SetFullTextGradient(info, colors[0], colors[1]);
                            }

                            pt.text.OnPreRenderText += SetGradient;
                            break;
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
            
            if (PaMNetworkManager.PamInstance?.LobbyInfo.HasStarted != true && (!GlobalsManager.IsHosting || !SteamLobbyManager.Inst.IsEveryoneLoaded))
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
        }
    }
    

    /// <summary>
    /// adds an "Update Mod" button in case a new version is available
    /// and replaces the Player hit and Player warp settings with a multiplayer version of those settings
    /// </summary>

    [HarmonyPatch(typeof(ShowChangeLog))]
    public static class UpdateModButtonPatches
    {
        [HarmonyPatch(nameof(ShowChangeLog.Start))]
        [HarmonyPrefix]
        static bool PostStart(ShowChangeLog __instance)
        {
            if (!SettingsManager.Inst.ShowChangeLog())
            {
                __instance.gameObject.SetActive(false);
            }
            return false;
        }

        public static void HandleMenuCreation(ShowChangeLog changeLog)
        {
            UI_Book book = changeLog.transform.parent.parent.parent.Find("Settings").GetComponent<UI_Book>();
            
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
            
            SystemManager.inst.StartCoroutine(FetchGithubReleases(changeLog.gameObject));
          
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
           
            MultiElementButton button = changeLog.transform.parent.parent.Find("pc_top-buttons/Custom Mode")
                .GetComponent<MultiElementButton>();

            button.onClick = new();
            button.onClick.AddListener(() =>
            {
                MenuSelectionManager.Instance.OpenMenu();
            });
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

    [HarmonyPatch(typeof(UIToSystems))]
    public static class UiToSystemsPatch
    {
        [HarmonyPatch(nameof(UIToSystems.LoadScene))]
        [HarmonyPostfix]
        static void PostLoadScene()
        {
            if (GlobalsManager.IsMultiplayer)
            {
                SteamManager.Inst.DisconnectAll();
                PAM.Logger.LogInfo("Left game lobby");
            }
        }
    }
}
