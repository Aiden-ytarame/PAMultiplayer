using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.PropertyVariants;
using BepInEx;
using PAMultiplayer.Managers;
using UnityEngine.UI;


namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class ClampPatch
    {
        [HarmonyPatch(nameof(VGPlayer.ClampPlayerPosition))]
        [HarmonyPrefix]
        static bool ClampPlayerPos_Replace(ref VGPlayer __instance)
        {
            return true;
            if (__instance.ObjectCamera == null)
            {
                if (VGPlayerManager.Inst.players[0].PlayerObject.ObjectCamera)
                {
                    __instance.ObjectCamera = VGPlayerManager.Inst.players[0].PlayerObject.ObjectCamera;
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    //This adds this Mod settings into the settings tab on the Main Menu, It looks absolutely garbage. its humongous
  
    [HarmonyPatch(typeof(ModifiersManager))]
    public class UI_Patch
    {
        [HarmonyPatch(nameof(ModifiersManager.Start))]
        [HarmonyPostfix]
        static void AddUIToSettings(ref ModifiersManager __instance)
        {
            Transform modifier = __instance.transform.GetChild(0).GetChild(0);

            Transform multiplayer = Object.Instantiate(modifier, __instance.transform);
       
            var toggle = multiplayer.GetComponent<MultiElementToggle>();
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(new System.Action<bool>(_ => {UIConnector.Inst.PlaySound("UI_Select");}));
                toggle.onValueChanged.AddListener(new System.Action<bool>(x =>
                {
                    StaticManager.IsHosting = x;
                    StaticManager.IsMultiplayer = x;
                }));

                multiplayer.GetComponentInChildren<GhostUIElement>().subGraphics = null;
                multiplayer.GetComponentInChildren<TextMeshProUGUI>().text =
                    "<size=85%><voffset=3><sprite=0><voffset=0><size=100%> Multiplayer ";
        }
    }

}
