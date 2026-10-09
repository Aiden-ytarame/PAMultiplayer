using HarmonyLib;
using PAMultiplayer.Managers;
using Systems.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VGFunctions;
using Action = System.Action;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(LevelEndScreen))]
public static class LevelEndScreenPatch
{
    public static LevelEndScreen Instance;
    
    [HarmonyPatch(nameof(LevelEndScreen.Start))]
    [HarmonyPostfix]
    static void PostStart(LevelEndScreen __instance)
    {
        Instance = __instance;
    }
    
    [HarmonyPatch(nameof(LevelEndScreen.CreateUI))]
    [HarmonyPrefix]
    static void PreCreateUI(LevelEndScreen __instance)
    {
        if (!GameManager.Inst.IsArcade) return;
        
        Transform buttonsParent = __instance.transform.Find("Content/EndScreen/Buttons");
        
        MultiElementButton blacklist = Object.Instantiate(buttonsParent.Find("Continue").gameObject, buttonsParent)
            .GetComponent<MultiElementButton>();

        blacklist.gameObject.SetActive(true);
        var ui = blacklist.GetComponent<UI_Button>();

        if (Settings.ChallengeBlacklist.Value.Contains(ArcadeManager.Inst.CurrentArcadeLevel.BaseLevelData.LevelID))
        {
            UIStateManager.Inst.RefreshTextCache(ui!.Text, "Whitelist Level");
        }
        else
        {
            UIStateManager.Inst.RefreshTextCache(ui!.Text, "Blacklist Level");
        }
        
        __instance.Buttons = __instance.Buttons.AddToArray(ui);
      
        blacklist.onClick = new();
        blacklist.onClick.AddListener(() =>
        {
            string blacklistStr = Settings.ChallengeBlacklist.Value;

            if (!blacklistStr.Contains(ArcadeManager.Inst.CurrentArcadeLevel.BaseLevelData.LevelID))
            {
                Settings.ChallengeBlacklist.Value += $"/{ArcadeManager.Inst.CurrentArcadeLevel.BaseLevelData.LevelID}";
                UIStateManager.Inst.RefreshTextCache(ui.Text, "Whitelist Level");
                ui.Text.text = "Whitelist Level";

            }
            else
            {
                Settings.ChallengeBlacklist.Value = Settings.ChallengeBlacklist.Value.Replace($"/{ArcadeManager.Inst.CurrentArcadeLevel.BaseLevelData.LevelID}", "");
                UIStateManager.Inst.RefreshTextCache(ui.Text, "Blacklist Level");
                ui.Text.text = "Blacklist Level";
            }
        });
    }
    
    [HarmonyPatch(nameof(LevelEndScreen.CreateUI))]
    [HarmonyPostfix]
    static void PostCreateUI(LevelEndScreen __instance)
    {
        if (!GameManager.Inst.IsArcade) return;

        Transform buttonsParent = __instance.transform.Find("Content/EndScreen/Buttons");

        buttonsParent.Find("Filer")?.transform.SetAsLastSibling();

        MultiElementButton nextLevel = __instance.ContinueButton;
        if ((GlobalsManager.Queue.Count == 0 && !GlobalsManager.IsChallenge) ||
            (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting))
        {
            __instance.DisableButton(nextLevel);
        }
        else
        {
            var element = nextLevel.GetComponent<UIElement>();
            nextLevel.gameObject.SetActive(true);

            LSHelpers.Delay(.1f, () =>
            {
                element.Show();
                nextLevel.LockButtonState(false);
                nextLevel.interactable = true;
            });
        }
   
        //remove all listeners seems broken :c
        nextLevel.onClick = new Button.ButtonClickedEvent();
        nextLevel.onClick.AddListener(() =>
        {
            if (GlobalsManager.IsMultiplayer)
            {
                SteamLobbyManager.Inst.UnloadAll();
            }

            if (GlobalsManager.IsChallenge)
            {
                if (GlobalsManager.IsMultiplayer && GlobalsManager.IsHosting)
                {
                    GameManagerPatch.CallRpc_Multi_OpenChallenge();
                }

                SceneLoader.Inst.LoadSceneGroup("Challenge");
                return;
            }

            GlobalsManager.PlayQueue(null);
        });

        if (GlobalsManager.IsMultiplayer)
        {
            __instance.DisableButton(buttonsParent.Find("Restart Level").GetComponent<MultiElementButton>());
        }
    }
}

[HarmonyPatch(typeof(PauseUIManager))]
public static class PauseUIManagerPatch
{
    [HarmonyPatch(nameof(PauseUIManager.OpenUI), typeof(Action), typeof(bool))]
    [HarmonyPrefix]
    static void PreOpen(PauseUIManager __instance)
    {
        __instance.transform.Find("sizer/Pause Menu/Skip Queue Level")?.gameObject.SetActive(GlobalsManager.Queue.Count > 0 || GlobalsManager.IsChallenge);
    }
}