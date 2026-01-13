using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Helper;

public static class SettingsHelper
{
    private static UI_Book.Page _mpPage;
    
    private static Transform _settingsPanel;
    private static GameObject _sliderPrefab;
    private static GameObject _togglePrefab;
    private static GameObject _labelPrefab;
    private static GameObject _spacerPrefab;
    
 
    /// <summary>
    /// instantiates the new settings tab, but be called everytime the MENU scene is loaded.
    /// </summary>
    public static void SetupMenu()
    {
        Transform settingTabPrefab = GameObject.Find("Canvas/Window/Content/Settings/Blank").transform.GetChild(0).GetChild(0);
        
        if (!settingTabPrefab)
        {
            PAM.Logger.LogError("No settings tab found, SetupMenu was called at a incorrect time or scene changed");
            return;
        }
        UI_Book book = settingTabPrefab.parent.parent.parent.GetComponent<UI_Book>();
        
        _settingsPanel = Object.Instantiate(book.transform.Find("Audio"), book.transform).Find("Right");
        for (int i = 0; i < _settingsPanel.childCount; i++)
        {
            Object.Destroy(_settingsPanel.GetChild(i).gameObject);
        }
       
        var titleCard =_settingsPanel.parent.GetChild(0).GetChild(1).GetComponent<TextMeshProUGUI>();

        titleCard.text = VGFunctions.LSText.ASCII.Get2HighASCII("Multiplayer Settings", 80);
        //UIStateManager.Inst.RefreshTextCache(titleCard, VGFunctions.LSText.ASCII.Get2HighASCII("Multiplayer Settings", 80));

       LocalizationSettings.StringDatabase.GetTable("General UI").AddEntry("mp", $"Multiplayer {PAM.Version}");
        _mpPage = new()
        {
            _ID = "Multiplayer",
            PageContainer = _settingsPanel.parent.gameObject,
            TitleLocalized = new LocalizedString("General UI", "mp"),
            BottomTitleLocalized = new LocalizedString("Empty", "Empty"),
            SubElements = [_settingsPanel.parent.GetChild(0).GetChild(0).GetComponent<UI_Button>()]
        };
        book.Pages.Add(_mpPage);
   
        //the button
        GameObject mpSettingsTab = Object.Instantiate(settingTabPrefab.GetChild(1).gameObject, settingTabPrefab);
        var text = mpSettingsTab.GetComponentInChildren<TextMeshProUGUI>();
        text.text = "Multiplayer";
      
        UIStateManager.Inst.RefreshTextCache(text, "Multiplayer");
     
        var button = mpSettingsTab.GetComponent<MultiElementButton>();
        
        button.onClick = new();
        
        var uiButton = mpSettingsTab.GetComponent<UI_Button>();
        button.onClick.AddListener(() =>
        {
            book.ForceSwapPage("Multiplayer");
            uiButton.OnClick();
        });
        
        //get 'prefabs'
        Transform prefabsParent =  book.transform.Find("Audio/Right");
        _sliderPrefab = prefabsParent.Find("Music").gameObject;
        _togglePrefab = prefabsParent.Find("Checkpoint SFX").gameObject;
        _labelPrefab = prefabsParent.Find("General Title").gameObject;
        _spacerPrefab = prefabsParent.Find("spacer").gameObject;
      
        //add our button to the Settings page in the UI_Book in the Canvas
        book.transform.parent.parent.parent.GetComponent<UI_Book>().Pages[6].SubElements.Add(uiButton);
      
        //_mpPage.DefaultSelection = mpSettingsTab.GetComponentInChildren<UI_Button>().gameObject;
        SetupSettings();
    }


    static void SetupSettings()
    {
        InstantiateLabel("<b>TRANSPARENT NANOS</b> - and related settings");
        
        InstantiateToggle("Transparent Nanos", "MpTransparentPlayer");
        InstantiateSlider("Transparent Opacity", "MpTransparentPlayerAlpha", "35%", "50%", "85%");
        InstantiateSpacer();
        
        InstantiateLabel("<b>MISCELLANEOUS</b> - other settings");
        
        InstantiateSlider("No Repeats in Challenge", "MpNoRepeat", "0 Rounds", "1 Round", "2 Rounds", "3 Rounds", "Infinite");
        InstantiateToggle("Chat Enabled", "MpChatEnabled");
        InstantiateToggle("Linked Health Hit Popup", "MpLinkedHealthPopup");
        InstantiateToggle("Allow hidden workshop levels", "MpAllowNonPublicLevels");
        InstantiateSpacer();
    }

    static void InstantiateLabel(string label)
    {
        var text = Object.Instantiate(_labelPrefab, _settingsPanel).GetComponentInChildren<TextMeshProUGUI>();
        text.text = label;
        UIStateManager.Inst.RefreshTextCache(text, label);
        
        _mpPage.SubElements.Add(text.transform.parent.GetComponent<UI_Text>());
    }

    static void InstantiateSpacer()
    {
        Object.Instantiate(_spacerPrefab, _settingsPanel);
    }
    static void InstantiateToggle(string label, string dataID)
    {
        var toggle = Object.Instantiate(_togglePrefab, _settingsPanel).GetComponent<UI_Toggle>();
        toggle.Value = DataManager.inst.GetSettingBool(dataID, false);
        toggle.DataID = dataID;
        toggle.ToggleLabel.text = label;
            
        UIStateManager.inst.RefreshTextCache(toggle.ToggleLabel,label);
        _mpPage.SubElements.Add(toggle);
    }

    static void InstantiateSlider(string label, string dataId, params string[] values)
    {
        InstantiateSlider(label, dataId, f =>
        {
            DataManager.inst.UpdateSettingInt(dataId, (int)f);
        },values);
    }
    static void InstantiateSlider(string label, string dataId, UnityAction<float> setter, params string[] values)
    {
        UI_Slider slider = Object.Instantiate(_sliderPrefab, _settingsPanel).GetComponent<UI_Slider>();
        slider.DataID = dataId;
        slider.DataIDType = UI_Slider.DataType.Runtime;
        slider.Range = new Vector2(0, values.Length - 1);
        slider.Values = values;
        slider.Value = DataManager.inst.GetSettingInt(dataId, 0);
        slider.Label.text = label;

        slider.OnValueChanged.AddListener(setter);
               
        UIStateManager.inst.RefreshTextCache(slider.Label, label);
        _mpPage.SubElements.Add(slider);
    }
}