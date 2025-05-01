using System;
using Il2CppSystems.SceneManagement;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PAMultiplayer.Managers.MenuManagers;

public class LobbyCreationManager : MonoBehaviour
{
    public static LobbyCreationManager Instance { get; private set; }
    public UI_Menu LobbyCreationMenu;
    
    public bool IsPrivate { get; private set; }
    public int PlayerCount { get; set; } = 16;
    
    public Action FallbackAction { get; set; }
    public Selectable FallbackUIElement { get; set; }

    private bool _isChallenge;
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LobbyCreationMenu = gameObject.GetComponent<UI_Menu>();

        MultiElementToggle toggle = transform.Find("Pause Menu/Private").GetComponent<MultiElementToggle>();
        toggle.onValueChanged.AddListener(new Action<bool>(x =>
        {
            IsPrivate = x;
        }));
        
        UI_Slider slider = transform.Find("Pause Menu/PlayerCount").GetComponent<UI_Slider>();
        slider.OnValueChanged.AddListener(new Action<float>(x =>
        {
            PlayerCount = 16 - (int)x * 4;
        }));
        
        transform.Find("Pause Menu/StartLobby").GetComponent<MultiElementButton>().onClick
            .AddListener(new Action(() =>
            {
                GlobalsManager.IsHosting = true;
                GlobalsManager.IsMultiplayer = true;

                if (_isChallenge)
                {
                    GlobalsManager.IsChallenge = true;
                    LobbyCreationMenu.HideAllInstant();
                    SceneLoader.Inst.LoadSceneGroup("Challenge");
                    return;
                }
                PublishedFileId id = ArcadeManager.Inst.CurrentArcadeLevel.SteamInfo.ItemID;
                if (!GlobalsManager.Queue.Contains(id.ToString()))
                    GlobalsManager.Queue.Add(id.ToString());

                ArcadeManager.Inst.CurrentArcadeLevel =
                    ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.Queue[0]);

                
                LobbyCreationMenu.HideAllInstant();
                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            }));

        MultiElementButton returnButton = transform.Find("Pause Menu/Return to Customs").GetComponent<MultiElementButton>();
        returnButton.onClick = new();//todo: remove this in unity
        returnButton.onClick.AddListener(new Action(CloseMenu));
    }

    public void OpenMenu(bool bIsChallange)
    {
        _isChallenge = bIsChallange;
        
        LobbyCreationMenu.ShowBase();
        LobbyCreationMenu.SwapView("main");
        LobbyCreationMenu.AllViews["main"].PossibleFirstButtons[0].Select();
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