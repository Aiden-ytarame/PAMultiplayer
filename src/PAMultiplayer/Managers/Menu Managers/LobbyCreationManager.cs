using System;
using PAMultiplayer.UI;
using Steamworks.Data;
using Systems.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace PAMultiplayer.Managers;

public class LobbyCreationManager : MonoBehaviour
{
    public static LobbyCreationManager Instance { get; private set; }
    public UI_Menu LobbyCreationMenu;
    
    public bool IsPrivate { get; private set; }
    public bool AllowClientLevels { get; private set; }
    public int PlayerCount { get; set; } = 16;
    
    public Action FallbackAction { get; set; }
    public Selectable FallbackUIElement { get; set; }

    private bool _isChallenge;
    private GameObject _allowClientLevels;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LobbyCreationMenu = gameObject.GetComponent<UI_Menu>();

        MultiElementToggle toggle = transform.Find("Pause Menu/Buttons/Private").GetComponent<MultiElementToggle>();
        toggle.onValueChanged.AddListener(x =>
        {
            IsPrivate = x;
        });
        
        MultiElementToggle toggle2 = transform.Find("Pause Menu/Buttons/AddLevels").GetComponent<MultiElementToggle>();
        toggle2.onValueChanged.AddListener(x =>
        {
            AllowClientLevels = x;
        });

        _allowClientLevels = toggle2.gameObject;
        
        UI_Slider slider = transform.Find("Pause Menu/Buttons/PlayerCount").GetComponent<UI_Slider>();
        slider.OnValueChanged.AddListener(x =>
        {
            PlayerCount = 16 - (int)x * 4;
        });
        
        transform.Find("Pause Menu/StartLobby").GetComponent<MultiElementButton>().onClick
            .AddListener(() =>
            {
                GlobalsManager.IsHosting = true;
                GlobalsManager.IsMultiplayer = true;

                if (_isChallenge)
                {
                    GlobalsManager.IsChallenge = true;
                    AllowClientLevels = false;
                    LobbyCreationMenu.HideAllInstant();
                    SceneLoader.Inst.LoadSceneGroup("Challenge");
                    return;
                }
                
                PublishedFileId id = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID;
                if (!GlobalsManager.Queue.ContainsLevel(id.ToString()))
                    GlobalsManager.Queue.AddLevel(ArcadeManager.Inst.CurrentArcadeLevel.TrackName, ArcadeManager.Inst.CurrentArcadeLevel.BaseLevelData.LevelID);

                ArcadeManager.Inst.CurrentArcadeLevel =
                    ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.Queue[0].Id);

                
                LobbyCreationMenu.HideAllInstant();
                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            });

        MultiElementButton returnButton = transform.Find("Pause Menu/Return to Customs").GetComponent<MultiElementButton>();
        returnButton.onClick = new();//todo: remove this in unity
        returnButton.onClick.AddListener(CloseMenu);
    }

    public void OpenMenu(bool bIsChallange)
    {
        _isChallenge = bIsChallange;
        _allowClientLevels.SetActive(!bIsChallange);
        
        LobbyCreationMenu.ShowBase();
        LobbyCreationMenu.SwapView("main");
        //LobbyCreationMenu.AllViews["main"].PossibleFirstButtons[0].Select();
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);
    }

    public void CloseMenu()
    {
        LobbyCreationMenu.HideAll();
        if (FallbackUIElement)
        {
            FallbackUIElement.Select();
        }

        if (FallbackAction != null)
        {
            FallbackAction.Invoke();
        }
    }
}