using System.Collections;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using HarmonyLib;
using PAMultiplayer.Managers;
using PAMultiplayer.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(LevelListController))]
internal static class LevelListControllerPatch
{
    private static List<QueueButton> _queueButtons = new();
    
    [HarmonyPatch(nameof(LevelListController.Awake))]
    [HarmonyPostfix]
    static void PostAwake(LevelListController __instance)
    {
        GlobalsManager.Queue.Clear();
        _queueButtons.Clear();
        __instance.Scroller.Scroller.cellViewInstantiated += OnCellInstantiated;
        __instance.Scroller.Scroller.cellViewVisibilityChanged += OnCellVisibilityChanged;
        SystemManager.inst.StartCoroutine(CheckForInput(__instance));
    }

    [HarmonyPatch(nameof(LevelListController.OnDestroy))]
    [HarmonyPostfix]
    static void PostDestroy(LevelListController __instance)
    {
        __instance.Scroller.Scroller.cellViewInstantiated -= OnCellInstantiated;
        __instance.Scroller.Scroller.cellViewVisibilityChanged -= OnCellVisibilityChanged;
    }

    static void OnCellInstantiated(EnhancedScroller scroller, EnhancedScrollerCellView view)
    {
        GameObject icon = Object.Instantiate(PAM.QueueIconPrefab, view.transform);
            
        var buttonElement = icon.GetComponent<MultiElementButton>();
        buttonElement.navigation = buttonElement.navigation with { mode = Navigation.Mode.None };
        if ( buttonElement.transform is RectTransform rect)
        {
            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
        }
        
        _queueButtons.Add(icon.AddComponent<QueueButton>());
    }
    
    private static void OnCellVisibilityChanged(EnhancedScrollerCellView cellView)
    {
        if (cellView == null || !cellView.active)
        {
            return;
        }

        var levelButton = (cellView as LevelRowCellView)?.LevelButton;

        if (!levelButton)
        {
            return;
        }
        
        if (LevelButtonBinder.BoundLevels.TryGetValue(levelButton, out var level))
        {
            var button = cellView.GetComponentInChildren<QueueButton>();
            button?.SetLevel(level.BaseLevelData.LevelID, level.TrackName);
        }
    }

    private static IEnumerator CheckForInput(LevelListController instance)
    {
        while (true)
        {
            if (!instance)
            {
                yield break;
            }
            
            if ((Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.JoystickButton3)) &&
                EventSystem.current && EventSystem.current.currentSelectedGameObject)
            {
                var button = EventSystem.current.currentSelectedGameObject.GetComponentInChildren<QueueButton>();
                button?.OnClick();
            }

            yield return new WaitForUpdate();
        }
    }
}