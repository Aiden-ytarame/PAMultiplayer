using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.PropertyVariants;
using BepInEx;
using PAMultiplayer;

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

            GameObject serverToggle = GameObject.Find("Toggle Camera Jiggle"); //get one of the settings Gameobject to duplicate.
            GameObject MPTitle = GameObject.Find("General Title (1)"); //get a category title Gameobject to duplicate.
            GameObject settingConent = serverToggle.transform.parent.gameObject; //get the settings parent to add new setting.

            //Add new  category title.
            GameObject newSetting = GameObject.Instantiate(MPTitle, settingConent.transform);
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "<b>MULTIPLAYER</b> - Change multiplayer mod settings";
            newSetting.GetComponent<UI_Text>().graphics = null;
            
            //make the Host Server Toggle.
            newSetting = GameObject.Instantiate(serverToggle, settingConent.transform);
            var toggle = newSetting.GetComponent<UI_Toggle>();
            toggle.DataID = "online_host";
            toggle.subGraphics = null;
            GameObject.Destroy(newSetting.GetComponentInChildren<GameObjectLocalizer>()); //is this still necessary?
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "Host Server";
            UpdateIpAndPort Updt = newSetting.AddComponent<UpdateIpAndPort>();

         
            //setup IP Input Text
            var inputTextPrefabObj = AssetBundle.LoadFromFile($"{Paths.PluginPath}\\PAMultiplayer\\Assets\\inputtext");

            var Prefab = inputTextPrefabObj.LoadAsset(inputTextPrefabObj.AllAssetNames()[0]);
            var NewInputText = GameObject.Instantiate(Prefab, settingConent.transform);
            NewInputText.name = "PAM_IPText";

            //yes, this sucks. I couldnt find a way to cast this to GameObject without doing this.
            //the asset bundle returns UnityEngine.Object
            //if you find a fix, please tell me.

            GameObject InputField = settingConent.transform.Find("PAM_IPText").gameObject;
            InputField.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Server IP";
            InputField.transform.GetChild(1).GetComponent<TMP_InputField>().text = "";

            Updt.IP = InputField.transform.GetChild(1).GetComponent<TMP_InputField>();
            Updt.IP.text = StaticManager.ServerIp;
           
    
            //Server Port
            NewInputText = GameObject.Instantiate(Prefab, settingConent.transform);
            NewInputText.name = "PAM_PortText";

            
            InputField = settingConent.transform.Find("PAM_PortText").gameObject;
            InputField.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Port";
            InputField.transform.GetChild(1).GetComponent<TMP_InputField>().text = "";

            Updt.PORT = InputField.transform.GetChild(1).GetComponent<TMP_InputField>();
            Updt.PORT.text = StaticManager.ServerPort;

            inputTextPrefabObj.Unload(false);
        }
    }

    //Events dont seem to work very well on Il2Cpp, so this is the workaround
    public class UpdateIpAndPort : MonoBehaviour
    {
        public TMP_InputField IP;
        public TMP_InputField PORT;
        void OnDisable()
        {
            //OnValueChange event from text mesh pro didnt work for some reason, so this is what I did.
            StaticManager.ServerIp = IP.text;
            StaticManager.ServerPort = PORT.text;

            if (DataManager.inst.GetSettingBool("online_host"))
                StaticManager.ServerIp = "127.0.0.1"; //does LocalHost work here?
        }
    }

}
