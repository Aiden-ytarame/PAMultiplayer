using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.PropertyVariants;
using BepInEx;
using PAMultiplayer.Managers;


namespace PAMultiplayer.Patch
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
                    __instance.ObjectCamera = VGPlayerManager.Inst.players[0].PlayerObject.ObjectCamera;
                    return true;
                }
                return false;
            }
            return true;
        }
    }

    //This adds this Mod settings into the settings tab on the Main Menu, It looks absolutely garbage. its humongous
  
    [HarmonyPatch(typeof(SystemManager))]
    public class UI_Patch
    {
        [HarmonyPatch(nameof(SystemManager.Awake))]
        [HarmonyPostfix]
        static void AddUIToSettings()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Menu")
                return;

            //wtf is this dude
            GameObject.Find("Toggle Camera Jiggle").transform.parent.gameObject.AddComponent<UpdateIpAndPort>();
        }
    }

    //Events dont seem to work very well on Il2Cpp, so this is the workaround
    public class UpdateIpAndPort : MonoBehaviour
    {
        void OnEnable()
        {
            GameObject serverToggle = transform.Find("Toggle Camera Jiggle").gameObject; //get one of the settings Gameobject to duplicate.
            GameObject MPTitle = GameObject.Find("General Title (1)"); //get a category title Gameobject to duplicate.


            //Add new  category title.
            GameObject newSetting = Instantiate(MPTitle, transform);
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "<b>MULTIPLAYER</b> - Change multiplayer mod settings";
            newSetting.GetComponent<UI_Text>().graphics = null;

            //make the Multiplayer Toggle.
            newSetting = Instantiate(serverToggle, transform);
            var toggle = newSetting.GetComponent<UI_Toggle>();
            toggle.DataID = "online_isMultiplayer";
            toggle.subGraphics = null;
            toggle.Value = DataManager.inst.GetSettingBool("online_isMultiplayer");
            
            //make the Host Server Toggle.
            newSetting = Instantiate(serverToggle, transform);
            toggle = newSetting.GetComponent<UI_Toggle>();
            toggle.DataID = "online_host";
            toggle.subGraphics = null;
            toggle.Value = DataManager.inst.GetSettingBool("online_host");
            
            Destroy(newSetting.GetComponentInChildren<GameObjectLocalizer>()); //is this still necessary?
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "Host Server";
            
        }
        void OnDisable()
        {
             StaticManager.IsHosting = DataManager.inst.GetSettingBool("online_host");
             StaticManager.IsMultiplayer = DataManager.inst.GetSettingBool("online_isMultiplayer");
        }
    }

}
