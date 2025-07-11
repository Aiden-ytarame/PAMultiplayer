﻿using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using AttributeNetworkWrapper.Core;
using BepInEx;
using PAMultiplayer.Helper;
using UnityEngine;
using PAMultiplayer.Managers;
using PAMultiplayer.Managers.MenuManagers;
using SimpleJSON;
using TMPro;
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
                mpToggle.isOn = false;
                if(LobbyCreationManager.Instance) 
                    LobbyCreationManager.Instance.OpenMenu(false);
            }));

            if (LobbyCreationManager.Instance)
            {
                LobbyCreationManager.Instance.FallbackUIElement = mpToggle;
            }
            MultiElementButton playgame = __instance.transform.parent.Find("Buttons/Primary/Play").GetComponent<MultiElementButton>();
            
            //playgame.onClick = new Button.ButtonClickedEvent();
            playgame.onClick.AddListener(new Action(() =>
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
            }));
            
            var row1 = __instance.transform.Find("r-2");
            var linkedHealthToggle = Object.Instantiate(row1.GetChild(0), __instance.transform).GetComponent<MultiElementToggle>();
            var modText = linkedHealthToggle.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            
            UIStateManager.Inst.RefreshTextCache(modText, "<size=75%><sprite name=\"heart\"><size=100%>Linked Health");
            modText.text = "<size=75%><sprite name=\"heart\"><size=100%>Linked Health";
            linkedHealthToggle.name = "mp_linkedHealth";
            
            linkedHealthToggle.onValueChanged = new();
            linkedHealthToggle.onValueChanged.AddListener(new Action<bool>(on =>
            {
                DataManager.inst.UpdateSettingBool("mp_linkedHealth", on);
            }));
            
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
                var lobbyBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
                lobbySettingsGo = Object.Instantiate(lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]).Cast<GameObject>(),
                    generalUI);
          
                selectionMenuGo = Object.Instantiate(lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]).Cast<GameObject>(),
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
                            GameManager.Inst.StartCoroutine(ShowDecay(player.VGPlayerData.PlayerObject).WrapToIl2Cpp());
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
                Multi_StartLevel();
            }

            if (LobbyScreenManager.Instance)
            {
                SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "0");
                
                GameManager.Inst.StartCoroutine(ShowNames().WrapToIl2Cpp());
                
                LobbyScreenManager.Instance.StartLevel();
                Object.Destroy(LobbyScreenManager.Instance, 0.2f);
            }
            CameraDB.Inst.SetUIVolumeWeightOut(0.2f);
            return true;
        }
        
        [MultiRpc]
        public static void Multi_StartLevel()
        {
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
            PAM.Logger.LogError("SKIP INTRO");
            UI_Book book = __instance.transform.parent.parent.parent.Find("Settings/Right Panel").GetComponent<UI_Book>();
            
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
                slider.Label.GetComponentInChildren<GameObjectLocalizer>().enabled = false;
                slider.OnValueChanged.AddListener(setter);
               
                UIStateManager.inst.RefreshTextCache(slider.Label, label);
                book.Pages[1].SubElements.Add(slider);
            }
            
            SystemManager.inst.StartCoroutine(FetchGithubReleases(__instance.gameObject).WrapToIl2Cpp());
            
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
            
            //this creates the Multiplayer tab in the settings
            SettingsHelper.SetupMenu();

            MultiElementButton button = __instance.transform.parent.parent.Find("pc_top-buttons/Custom Mode")
                .GetComponent<MultiElementButton>();

            button.onClick = new();
            button.onClick.AddListener(new Action(() =>
            {
                MenuSelectionManager.Instance.OpenMenu();
            }));
            
            if (!SingletonBase<SettingsManager>.Inst.ShowChangeLog())
            {
                __instance.gameObject.SetActive(false);
            }

            return false;
        }

        const string UpdateStr = "<sprite name=info> Update Multiplayer";
        const string RestartStr = "<sprite name=info> Restart the game";
        private const string FailedStr = "<sprite name=info> Failed";
        static bool _isUpdating;
        static bool _updated;
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
            
            string updText = _updated ? RestartStr : UpdateStr;
            updateText.text = updText;
            UIStateManager.Inst.RefreshTextCache(updateText, updText);
            
            var button = updateMod.GetComponent<MultiElementButton>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(new Action(() =>
            {
                if (_isUpdating || _updated)
                {
                    return;
                }
                _isUpdating = true;
                
                const string downloadingStr = "<sprite name=info> Updating Multiplayer...";
                updateText.text = downloadingStr;
                UIStateManager.Inst.RefreshTextCache(updateText, downloadingStr);
                
                DataManager.inst.StartCoroutine(DownloadGithubRelease(updateText).WrapToIl2Cpp());
            }));
            
            
            //stupid workaround to getting the wrong canvas
            GameObject.Find("Canvas/Window").transform.parent.GetComponent<UI_Book>().Pages[0].SubElements
                .Add(updateMod.GetComponent<UI_Button>());
        }

        static IEnumerator DownloadGithubRelease(TextMeshProUGUI button)
        {
            string hash;
            
            UnityWebRequest hashRequest =
                UnityWebRequest.Get("https://github.com/Aiden-ytarame/PAMultiplayer/releases/latest/download/ReleaseHash.sha256");
            try
            {
                yield return hashRequest.SendWebRequest();
            
                if (hashRequest.result != UnityWebRequest.Result.Success)
                {
                    PAM.Logger.LogError("Failed to download Github Release, oof");
                    PAM.Logger.LogError(hashRequest.error); ;
                    Fail();
                    yield break;
                }

                hash = hashRequest.downloadHandler.text.Trim();
            }
            finally
            {
                hashRequest.Dispose();
            }
            
            UnityWebRequest downloadRequest =
                UnityWebRequest.Get("https://github.com/Aiden-ytarame/PAMultiplayer/releases/latest/download/PAMultiplayer.dll");
            try
            {
                yield return downloadRequest.SendWebRequest();
            
                if (downloadRequest.result != UnityWebRequest.Result.Success)
                {
                    PAM.Logger.LogError("Failed to download Github Release, oof");
                    PAM.Logger.LogError(downloadRequest.error);
                    Fail();
                    yield break;
                }

                string fileHash;
            
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(downloadRequest.downloadHandler.data);
                    fileHash = BitConverter.ToString(bytes).Replace("-","");
                }

                if (fileHash != hash)
                {
                    PAM.Logger.LogError("Release Hash Mismatch!");
                    PAM.Logger.LogError(hash);
                    PAM.Logger.LogError(fileHash);
                    Fail();
                    yield break;
                }
            
                string fullPath = Assembly.GetExecutingAssembly().Location;
            
                try
                {
                    File.Move(fullPath, Path.GetDirectoryName(fullPath) + "\\PAMultiplayerOld.dll", true);
                }
                catch (Exception e)
                {
                    PAM.Logger.LogError(e);
                    Fail();
                    yield break;
                }

                try
                {
                    File.WriteAllBytesAsync(Path.GetDirectoryName(fullPath) + "\\PAMultiplayer.dll", downloadRequest.downloadHandler.data); //async throws everytime due to bepinex
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            finally
            {
                downloadRequest.Dispose();  
            }
         
            _updated = true;
            if (button)
            {
                button.text = RestartStr;
                UIStateManager.Inst.RefreshTextCache(button, RestartStr);
            }
            yield break;

            void Fail()
            {
                _isUpdating = false;
                if (button)
                {
                    button.text = FailedStr;
                    UIStateManager.Inst.RefreshTextCache(button,  FailedStr);
                }
            }
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
