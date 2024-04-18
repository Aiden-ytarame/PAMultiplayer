using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.PropertyVariants;
namespace YtaramMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class ClampPatch
    {
        [HarmonyPatch(nameof(VGPlayer.ClampPlayerPosition))]
        [HarmonyPrefix]
        static bool ClampPlayerPos_Replace(ref VGPlayer __instance)
        {
            if (__instance.ObjectCamera == null)
            {
                if (VGPlayerManager.Inst.players[0].PlayerObject.ObjectCamera)
                {
                    __instance.ObjectCamera = VGPlayerManager.Inst.players[0].PlayerObject.ObjectCamera; //this patch might be useless now, it prevented an issue early on but it shouldnt happen anymore
                    return true;
                }
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(SystemManager))]
    public class UI_Patch
    {
        [HarmonyPatch(nameof(SystemManager.Awake))]
        [HarmonyPostfix]
        static void AddUIToSettings(ref SystemManager __instance)
        {
            GameObject UI = GameObject.Find("Toggle Camera Jiggle");
            if (UI == null)
                return;
  
            var newSetting = GameObject.Instantiate(UI, UI.transform.parent.GetComponent<RectTransform>());
            var toggle = newSetting.GetComponent<UI_Toggle>();
                toggle.DataID = "online_host";
            //toggle.localizers = null;
            //toggle.localizersStr = null;
           // toggle.tmpTMP = null;
           // toggle.ToggleLabel = null;
            toggle.subGraphics = null;
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "Host Server";

            GameObject.Destroy(newSetting.GetComponentInChildren<GameObjectLocalizer>());

        }
    }

}
