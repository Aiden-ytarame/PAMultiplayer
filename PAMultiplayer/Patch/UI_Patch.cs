using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppSystems.SceneManagement;
using UnityEngine;
using TMPro;
using PAMultiplayer.Managers;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch
{
  
    /// <summary>
    /// adds the Multiplayer modifier to the UI
    /// </summary>
    [HarmonyPatch(typeof(ModifiersManager))]
    public class UI_Patch
    {
        [HarmonyPatch(nameof(ModifiersManager.Start))]
        [HarmonyPostfix]
        static void AddUIToSettings(ref ModifiersManager __instance)
        {
            //modifier prefab
            Transform modifier = __instance.transform.GetChild(0).GetChild(0);

            Transform multiplayer = Object.Instantiate(modifier, __instance.transform);

            Object.Destroy(multiplayer.GetComponent<ToggleGroup>()); //don't remember why this here, might remove it

            var toggle = multiplayer.GetComponent<MultiElementToggle>();
            toggle.isOn = false;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            //Pidge removed these UI sounds in a recent update so ill removed them for now too
            //toggle.onValueChanged.AddListener(new System.Action<bool>(_ => {AudioManager.Inst.PlaySound("UI_Select", 1);})); 
            toggle.onValueChanged.AddListener(new Action<bool>(x =>
            {
                GlobalsManager.IsHosting = x;
                GlobalsManager.IsMultiplayer = x;
            }));
         
            //this is so the localization doesn't override the text
            multiplayer.GetComponent<GhostUIElement>().subGraphics = null;
            //sprite0 is the controller sprite
            multiplayer.GetComponentInChildren<TextMeshProUGUI>().text =
                "<size=85%><voffset=3><sprite=0><voffset=0><size=100%> Multiplayer ";
        }
    }

    /// <summary>
    /// just having fun with loading screen Tips
    /// </summary>
    [HarmonyPatch(typeof(SceneLoader))]
    public static class Test
    {
        [HarmonyPatch(nameof(SceneLoader.Start))]
        [HarmonyPostfix]
        static void GetterTips(ref SceneLoader __instance)
        {
            List<string> newVals = new List<string>(__instance.Tips)
            {
                "You should try the log Fallen Kingdom!",
                "You can always call other Nanos for help!",
                "Git Gud",
                "I'm in your walls.",
                "Good Nano.",
                "No tips for you >:)",
                "Boykisser sent kisses!"
            };
            //thanks for making this public after I complained lol
            __instance.Tips = newVals.ToArray();
        }
    }
}
