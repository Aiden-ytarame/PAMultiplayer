using System;
using System.Collections.Generic;
using System.Reflection;
using Cpp2IL.Core.Extensions;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
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
    static void PostSTart(ArcadeMenu __instance)
    {
        GameObject QueueIconPrefab;
        _queueButtons.Clear();
        GlobalsManager.Queue.Clear();
        
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PAMultiplayer.Assets.queue assets"))
        {
            var lobbyBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
            
            QueueIconPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]).Cast<GameObject>();
         
            lobbyBundle.Unload(false);
        }
        
        foreach (var LevelButton in __instance.LevelButtons)
        {
            Transform button = LevelButton.Button.transform;
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
        queueButton = gameObject.GetComponent<UI_Button>();
        queueText = gameObject.GetComponentInChildren<TextMeshProUGUI>();
        queueButton.GetComponent<MultiElementButton>().onClick.AddListener(new Action(OnClick));

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
        queueButton.Show();
        
        currentLevel = level;

        queueIndex = GlobalsManager.Queue.IndexOf(level) + 1;
        queueText.text = queueIndex > 0 ? queueIndex.ToString() : "+";
    }
}

[HarmonyPatch(typeof(LevelEndScreen))]
public static class LevelEndScreenPatch
{
    [HarmonyPatch(nameof(LevelEndScreen.CreateUI))]
    [HarmonyPostfix]
    static void PostStart(LevelEndScreen __instance)
    {
        if (!GameManager.Inst.IsArcade) return;
        
        Transform buttonsParent = __instance.transform.Find("Content/EndScreen/Buttons");
        
        buttonsParent.Find("Flair").gameObject.SetActive(false);

        MultiElementButton nextLevel = buttonsParent.Find("Continue").GetComponent<MultiElementButton>();
        if (GlobalsManager.Queue.Count == 0 || (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting))
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
        nextLevel.onClick.AddListener(new Action(() =>
        {
            ulong id = ulong.Parse(GlobalsManager.Queue[0]);
            ArcadeManager.Inst.CurrentArcadeLevel = ArcadeLevelDataManager.Inst.GetSteamLevel(id);
            GlobalsManager.LevelId = id;
            
            
            if (GlobalsManager.IsMultiplayer)
            {
                GlobalsManager.IsReloadingLobby = true;
                SteamLobbyManager.Inst.UnloadAll();
            }
            
            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            PAM.Logger.LogInfo("Starting next level in queue!");
        }));
    }
}