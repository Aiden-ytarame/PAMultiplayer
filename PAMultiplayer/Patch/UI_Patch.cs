using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using UnityEngine;
using PAMultiplayer.Managers;
using SimpleJSON;
using Steamworks.Data;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Networking;
using UnityEngine.UI;
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
            var hiddenButtons = __instance.transform.parent.Find("Buttons/Buttons Hidden").gameObject;
            hiddenButtons.SetActive(true);

            var mpToggle = hiddenButtons.transform.GetChild(0).GetComponent<MultiElementToggle>();
            mpToggle.isOn = false;
            mpToggle.interactable = true;
            mpToggle.onValueChanged = new Toggle.ToggleEvent();
            mpToggle.onValueChanged.AddListener(new Action<bool>(_ =>
            {
                GlobalsManager.IsHosting = true;
                GlobalsManager.IsMultiplayer = true;

                PublishedFileId id = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID;
                if (!GlobalsManager.Queue.Contains(id.ToString()))
                    GlobalsManager.Queue.Add(id.ToString());

                ArcadeManager.Inst.CurrentArcadeLevel =
                    ArcadeLevelDataManager.Inst.GetSteamLevel(ulong.Parse(GlobalsManager.Queue[0]));

                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            }));

            MultiElementButton playgame = __instance.transform.parent.Find("Buttons/Primary/Play").GetComponent<MultiElementButton>();
            
            //playgame.onClick = new Button.ButtonClickedEvent();
            playgame.onClick.AddListener(new Action(() =>
            {
                if (GlobalsManager.Queue.Count > 0)
                {
                    PublishedFileId id = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID;
                    if (!GlobalsManager.Queue.Contains(id.ToString()))
                        GlobalsManager.Queue.Add(id.ToString());

                    ulong queueLevel = ulong.Parse(GlobalsManager.Queue[0]);
                    GlobalsManager.LevelId = queueLevel;
                    ArcadeManager.Inst.CurrentArcadeLevel =
                        ArcadeLevelDataManager.Inst.GetSteamLevel(queueLevel);
                    
                    //SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                }   
            }));
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
    }
    
      //the reason there's both unpause functions here, its cuz the UI unpause calls PauseMenu.UnPause() and pressing ESC calls GameManager.UnPause().
    [HarmonyPatch]
    public class PauseLobbyPatch
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
                    string text = currentLobbyMember.Name;;
                    if (currentLobbyMember.Id == GlobalsManager.LocalPlayer)
                    {
                        text = "YOU";
                        if (player.PlayerObject)
                        {
                            GameManager.Inst.StartCoroutine(ShowDecay(player.PlayerObject).WrapToIl2Cpp());
                        }
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
                SteamManager.Inst.Server.StartLevel();
            }

            if (LobbyScreenManager.Instance)
            {
                SteamLobbyManager.Inst.HideLobby();
                SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
                
                foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                {
                    VGPlayer player = vgPlayerData.PlayerObject;
                    if(!player) continue;
                
                    player.StopCoroutine(player.SpawnLoop());
                    player.Spawn();
                }
                
                VGPlayerManager.inst.RespawnPlayers();
                
                GameManager.Inst.StartCoroutine(ShowNames().WrapToIl2Cpp());
                
                LobbyScreenManager.Instance.LobbyMenu.HideAll();
                Object.Destroy(LobbyScreenManager.Instance, 1);
            }
            CameraDB.Inst.SetUIVolumeWeightOut(0.2f);
            return true;
        }
    }

    /// <summary>
    /// just having fun with loading screen Tips
    /// </summary>
    [HarmonyPatch(typeof(SceneLoader))]
    public static class LoadingTips
    {
        [HarmonyPatch(nameof(SceneLoader.Start))]
        [HarmonyPostfix]
        static void GetterTips(ref SceneLoader __instance)
        {
            var customTips = new List<string>(__instance.Tips)
            {
                "You should try the log Fallen Kingdom!",
                "You can always call other Nanos for help!",
                "Git Gud",
                "I'm in your walls.",
                "Good Nano.",
                "No tips for you >:)",
                "Boykisser sent kisses!",
                "The developer wants me to say something here.",
                "I'm a furry. So what?",
                "You might be an Nano but you should hydrate anyways.",
                "Before time began there was The Cube...",
                "Ready to be carried by another Nano again?",
                "Squeezing your Nano through the internet wire...",
            };
            //thanks Pidge for making this public after I complained lol
            __instance.Tips = customTips.ToArray();
        }
    }

    /// <summary>
    /// adds an "Update Mod" button in case a new version is available
    /// </summary>

    [HarmonyPatch(typeof(SkipIntroMenu))]
    public static class UpdateModButton
    {
        [HarmonyPatch(nameof(SkipIntroMenu.Start))]
        [HarmonyPostfix]
        static void PostStart(SkipIntroMenu __instance)
        {
            UI_Book book = __instance.transform.Find("Window/Content/Settings/Right Panel").GetComponent<UI_Book>();
            
            void instantiateSlider(GameObject prefab, Transform parent, string label, string dataId, Action<float> setter)
            {
                GameObject WarpSliderObj = Object.Instantiate(prefab, parent);
            
                UI_Slider slider = WarpSliderObj.GetComponent<UI_Slider>();
                slider.DataID = dataId;
                slider.DataIDType = UI_Slider.DataType.Runtime;
                slider.Range = new Vector2(0, 2);
                slider.Values = new[] { "All Players", "Local player Only", "None" };
                slider.Value = DataManager.inst.GetSettingInt(dataId, 0);
                slider.Label.text = label;

                slider.OnValueChanged.AddListener(setter);
               
                UIStateManager.inst.RefreshTextCache(slider.Label, label);
                book.Pages[1].SubElements.Add(slider);
            }
            
            __instance.StartCoroutine(FetchGithubReleases().WrapToIl2Cpp());
            
            GameObject sliderPrefab = book.transform.Find("Content/Audio/Content/Menu Music").gameObject;
            Transform audioParent = sliderPrefab.transform.parent;
            
            //destroy SFX toggles
            Object.Destroy(audioParent.GetChild(audioParent.childCount-1).gameObject);
            Object.Destroy(audioParent.GetChild(audioParent.childCount-2).gameObject);
            
            instantiateSlider(sliderPrefab, audioParent, "Player Hit SFX", "MpPlayerSFX", x =>
            {
                DataManager.inst.UpdateSettingInt("MpPlayerSFX", (int)x);
                DataManager.inst.UpdateSettingBool("PlayerSFX", x != 2);
            });
            instantiateSlider(sliderPrefab, audioParent, "Player Hit Warp SFX", "MpPlayerWarpSFX", x =>
            {
                DataManager.inst.UpdateSettingInt("MpPlayerWarpSFX", (int)x);
                DataManager.inst.UpdateSettingBool("PlayerWarpSFX", x != 2);
            });
            
            GameObject togglePrefab = __instance.transform.Find("Window/Content/Settings/Right Panel/Content/Accessibility/Content/High Contrast").gameObject;
            var toggle = Object.Instantiate(togglePrefab, togglePrefab.transform.parent).GetComponent<UI_Toggle>();
            toggle.transform.SetSiblingIndex(3);
            
            toggle.Value = DataManager.inst.GetSettingBool("MpTransparentPlayer", false);
            toggle.DataID = "MpTransparentPlayer";
            toggle.ToggleLabel.text = "Transparent Nanos";
            
            UIStateManager.inst.RefreshTextCache(toggle.ToggleLabel,"Transparent Nanos");
            book.Pages[3].SubElements.Add(toggle);
            
        }

        static IEnumerator FetchGithubReleases()
        {
            UnityWebRequest request =
                UnityWebRequest.Get("https://api.github.com/repos/Aiden-ytarame/PAMultiplayer/releases/latest");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                PAM.Logger.LogError("Failed to fetch Github Release, oof");
                yield break;
            }

            JSONNode latestRelease = JSON.Parse(request.downloadHandler.text);

            bool isLatest = Version.Parse(latestRelease["tag_name"].Value.Substring(1)).CompareTo(Version.Parse(PAM.Version)) <= 0;

            if (isLatest)
            {
                PAM.Logger.LogInfo("Got Latest Version");
                yield break;
            }

            PAM.Logger.LogWarning("New Mp Version Available!");
            
            Transform buttons = GameObject.Find("Canvas/Window/Content/Main Menu/Buttons 3").transform;
            
            //default has these buttons broken, fixed in next update 
            //todo: remove this after update
            if (DataManager.versionNumber == "24.8.5")
            {
                buttons.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
                buttons.Find("Quit").gameObject.AddComponent<LayoutElement>().minWidth = 547.333f;
            }
            GameObject updateMod = Object.Instantiate(buttons.Find("Changelog"), buttons).gameObject;
            updateMod.name = "Update MP";
            updateMod.SetActive(true);
            
            var button = updateMod.GetComponent<MultiElementButton>();
        
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new Action(() =>
            {
                Application.OpenURL("https://github.com/Aiden-ytarame/PAMultiplayer/releases/latest");
            }));

            updateMod.GetComponentInChildren<GameObjectLocalizer>().TrackedObjects._items[0]
                .GetTrackedProperty<LocalizedStringProperty>("m_text").LocalizedString
                .SetReference("Localization", "ui.multiplayer.update");
            
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
