using System.IO;
using HarmonyLib;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.PropertyVariants;
using BepInEx;


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
        TMP_InputField IP;
        TMP_InputField PORT;

        void OnEnable()
        {
            GameObject serverToggle = transform.Find("Toggle Camera Jiggle").gameObject; //get one of the settings Gameobject to duplicate.
            GameObject MPTitle = GameObject.Find("General Title (1)"); //get a category title Gameobject to duplicate.


            //Add new  category title.
            GameObject newSetting = GameObject.Instantiate(MPTitle, transform);
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "<b>MULTIPLAYER</b> - Change multiplayer mod settings";
            newSetting.GetComponent<UI_Text>().graphics = null;

            //make the Host Server Toggle.
            newSetting = GameObject.Instantiate(serverToggle, transform);
            var toggle = newSetting.GetComponent<UI_Toggle>();
            toggle.DataID = "online_host";
            toggle.subGraphics = null;
            toggle.Value = DataManager.inst.GetSettingBool("online_host");


            GameObject.Destroy(newSetting.GetComponentInChildren<GameObjectLocalizer>()); //is this still necessary?
            newSetting.GetComponentInChildren<TextMeshProUGUI>().text = "Host Server";


            //setup IP Input Text
            var inputTextPrefabObj = AssetBundle.LoadFromFile(Directory.GetFiles(Paths.PluginPath, "inputtext", SearchOption.AllDirectories)[0]);

            var Prefab = inputTextPrefabObj.LoadAsset(inputTextPrefabObj.AllAssetNames()[0]);
            var NewInputText = GameObject.Instantiate(Prefab, transform);
            NewInputText.name = "PAM_IPText";
            
            //yes, this sucks. I couldnt find a way to cast this to GameObject without doing this.
            //the asset bundle returns UnityEngine.Object
            //if you find a fix, please tell me.

            GameObject inputField = transform.Find("PAM_IPText").gameObject;
            inputField.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Server IP";
            inputField.transform.GetChild(1).GetComponent<TMP_InputField>().text = "";

            IP = inputField.transform.GetChild(1).GetComponent<TMP_InputField>();
            IP.text = StaticManager.ServerIp;


            //Server Port
            NewInputText = GameObject.Instantiate(Prefab, transform);
            NewInputText.name = "PAM_PortText";


            inputField = transform.Find("PAM_PortText").gameObject;
            inputField.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Port";
            inputField.transform.GetChild(1).GetComponent<TMP_InputField>().text = "";

            PORT = inputField.transform.GetChild(1).GetComponent<TMP_InputField>();
            PORT.text = StaticManager.ServerPort;

            inputTextPrefabObj.Unload(false);
        }
        void OnDisable()
        {
            //OnValueChange event from text mesh pro didnt work for some reason, so this is what I did.
            StaticManager.ServerIp = IP.text;
            StaticManager.ServerPort = PORT.text;

            if (DataManager.inst.GetSettingBool("online_host"))
                StaticManager.ServerIp = "localhost"; //does LocalHost work here?
        }
    }

}
