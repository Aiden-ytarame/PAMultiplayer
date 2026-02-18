using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Crosstales;
using HarmonyLib;
using PAMultiplayer.Managers;
using Systems.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Action = System.Action;
using Object = UnityEngine.Object;


namespace PAMultiplayer.Patch;



/// <summary>
/// generate the queue UI
/// </summary>
[HarmonyPatch(typeof(ArcadeMenu))]
public static class ArcadeMenuPatch
{
    private static List<QueueButton> _queueButtons = new();
        
    [HarmonyPatch(nameof(ArcadeMenu.Start))]
    [HarmonyPostfix]
    static void PostStart(ArcadeMenu __instance)
    {
        GameObject QueueIconPrefab;
        _queueButtons.Clear();
        GlobalsManager.Queue.Clear();
        
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PAMultiplayer.Assets.queue assets"))
        {
            var lobbyBundle = AssetBundle.LoadFromMemory(stream!.CTReadFully());
            
            QueueIconPrefab = lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[0]) as GameObject;
         
            lobbyBundle.Unload(false);
        }
        
        foreach (var levelButton in __instance.LevelButtons)
        {
            Transform button = levelButton.Button.transform;
            GameObject icon = Object.Instantiate(QueueIconPrefab, button);
 
            _queueButtons.Add(icon.AddComponent<QueueButton>());
        }
    }
    
    [HarmonyPatch(nameof(ArcadeMenu.SelectPage), new[]{typeof(int), typeof(bool)})]
    [HarmonyPostfix]
    static void PostRenderLevelButtons(ArcadeMenu __instance)
    {
        int page = __instance.Page;
        for (int i = 0; i < 12; i++)
        {
            if (__instance.SearchedLevels.Count - 1 >= i + 12 * page)
            {
                _queueButtons[i].gameObject.SetActive(true);
                _queueButtons[i].SetLevel(__instance.SearchedLevels[i + 12 * page].BaseLevelData.LevelID);
            }
            else
            {
                _queueButtons[i].gameObject.SetActive(false);
            }
        }
    }
}


public class QueueButton : MonoBehaviour
{
    private UI_Button queueButton;
    private TextMeshProUGUI queueText;

    private string currentLevel = "";
    private int queueIndex = 0;
    private delegate void QueueUpdated();
    private static event QueueUpdated _queueUpdated;
    
    private void Start()
    {
        _queueUpdated += UpdateButton;
    }

    private void OnDestroy()
    {
        _queueUpdated -= UpdateButton;
    }

    void UpdateButton()
    {
        int newIndex = GlobalsManager.Queue.IndexOf(currentLevel) + 1;
        if (newIndex > 0 && newIndex != queueIndex)
        {
            queueIndex = newIndex;
            queueButton.Show();
            queueText.text =newIndex.ToString();
        }
    }
    public void OnClick()
    {
        if (GlobalsManager.Queue.Contains(currentLevel))
        {
            GlobalsManager.Queue.Remove(currentLevel);
            queueIndex = 0;
            queueText.text = "+";
            _queueUpdated.Invoke();
        }
        else
        {
            GlobalsManager.Queue.Add(currentLevel);
            queueIndex = GlobalsManager.Queue.Count;
            queueText.text = queueIndex.ToString();
        }
    }

    public void SetLevel(string level)
    {
        if (queueButton == null)
        {
            queueButton = gameObject.GetComponent<UI_Button>();
            queueText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
            queueButton.GetComponent<MultiElementButton>().onClick.AddListener(OnClick);
        }

        queueButton = gameObject.GetComponent<UI_Button>();
        queueButton.Show();
        
        currentLevel = level;

        queueIndex = GlobalsManager.Queue.IndexOf(level) + 1;
        queueText.text = queueIndex > 0 ? queueIndex.ToString() : "+";
    }
}

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

        blacklist.Start();
        var ui = blacklist.UIElement as UI_Button;

        if (Settings.ChallengeBlacklist.Value.Contains(ArcadeManager.Inst.CurrentArcadeLevel.name))
        {
            UIStateManager.Inst.RefreshTextCache(ui!.Text, "Whitelist Level");
        }
        else
        {
            UIStateManager.Inst.RefreshTextCache(ui!.Text, "Blacklist Level");
        }
        
        __instance.Buttons = __instance.Buttons.AddToArray(blacklist.UIElement);
      
        blacklist.onClick = new();
        blacklist.onClick.AddListener(() =>
        {
            string blacklistStr = Settings.ChallengeBlacklist.Value;

            if (!blacklistStr.Contains(ArcadeManager.Inst.CurrentArcadeLevel.name))
            {
                Settings.ChallengeBlacklist.Value += $"/{ArcadeManager.Inst.CurrentArcadeLevel.name}";
                UIStateManager.Inst.RefreshTextCache(ui.Text, "Whitelist Level");
                ui.Text.text = "Whitelist Level";

            }
            else
            {
                Settings.ChallengeBlacklist.Value = Settings.ChallengeBlacklist.Value.Replace($"/{ArcadeManager.Inst.CurrentArcadeLevel.name}", "");
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

        buttonsParent.Find("Flair").gameObject.SetActive(false);


        MultiElementButton nextLevel = buttonsParent.Find("Continue").GetComponent<MultiElementButton>();
        if ((GlobalsManager.Queue.Count == 0 && !GlobalsManager.IsChallenge) ||
            (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting))
        {
            __instance.DisableButton(nextLevel);
        }
        else
        {
            nextLevel.Lock = false;
            nextLevel.interactable = true;
        }

        nextLevel.uiElement.Show();
        nextLevel.gameObject.SetActive(true);

        //remove all listeners seems broken :c
        nextLevel.onClick = new Button.ButtonClickedEvent();
        nextLevel.onClick.AddListener(() =>
        {
            GlobalsManager.IsReloadingLobby = true;
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

            string id = GlobalsManager.Queue[0];
            ArcadeManager.Inst.CurrentArcadeLevel = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
            GlobalsManager.LevelId = id;


            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            PAM.Logger.LogInfo("Starting next level in queue!");
        });

        if (GlobalsManager.IsMultiplayer)
        {
            __instance.DisableButton(buttonsParent.Find("Restart Level").GetComponent<MultiElementButton>());
        }
    }
}