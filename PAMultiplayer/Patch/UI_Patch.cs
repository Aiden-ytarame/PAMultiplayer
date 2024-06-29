using HarmonyLib;
using UnityEngine;
using TMPro;
using PAMultiplayer.Managers;
using UnityEngine.UI;


namespace PAMultiplayer.Patch
{
  
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
            toggle.isOn = false;
            toggle.onValueChanged = new Toggle.ToggleEvent();
                toggle.onValueChanged.AddListener(new System.Action<bool>(_ => {UIConnector.Inst.PlaySound("UI_Select");}));
                toggle.onValueChanged.AddListener(new System.Action<bool>(x =>
                {
                    StaticManager.IsHosting = x;
                    StaticManager.IsMultiplayer = x;
                }));

                multiplayer.GetComponent<GhostUIElement>().subGraphics = null;
                multiplayer.GetComponentInChildren<TextMeshProUGUI>().text =
                    "<size=85%><voffset=3><sprite=0><voffset=0><size=100%> Multiplayer ";
        }
    }

}
