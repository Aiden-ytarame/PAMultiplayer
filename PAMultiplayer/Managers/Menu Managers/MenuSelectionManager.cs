using System;
using Il2CppSystems.SceneManagement;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Managers.MenuManagers;

public class MenuSelectionManager : MonoBehaviour
{
    public static MenuSelectionManager Instance { get; private set; }
    public UI_Menu Menu;
    private void Awake()
    {
        if(Instance != null)
        {
            Destroy(this);
            return;
        }
        
        Instance = this;
        Menu = gameObject.GetComponent<UI_Menu>();

        Transform buttons = transform.Find("New Game");
        
        buttons.GetChild(0).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
        {
            Menu.HideAllInstant();
            MultiplayerUIManager.Inst.SetContinueAction(new Action(() =>
            {
                SceneLoader.Inst.LoadSceneGroup("Arcade");
            }));
            MultiplayerUIManager.Inst.OpenUI();
        }));
        
        buttons.GetChild(1).GetComponent<MultiElementButton>().onClick.AddListener(new Action(async () =>
        {
            Menu.HideAllInstant();
            GeneralUILoader.Inst?.LeaveSaveUI?.Invoke();
            LobbyQuery query = new LobbyQuery();
            var lobbies = await query.WithMaxResults(1).RequestAsync();

            if (lobbies != null && lobbies.Length > 0)
            {
                GlobalsManager.IsHosting = false;
                GlobalsManager.IsMultiplayer = true;
                lobbies[0].Join();
            }

        }));
        
        buttons.GetChild(2).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
        {
            Menu.HideAllInstant();
            MultiplayerUIManager.Inst.SetContinueAction(new Action(() =>
            {
                GlobalsManager.IsChallenge = true;
                SceneLoader.Inst.LoadSceneGroup("Challenge");
            }));
            MultiplayerUIManager.Inst.OpenUI();
        }));
        buttons.GetChild(3).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
        {
            Menu.HideAllInstant();
            if (LobbyCreationManager.Instance)
            {
                LobbyCreationManager.Instance.FallbackAction = () =>
                {
                    GeneralUILoader.Inst?.LeaveSaveUI?.Invoke();
                };
                LobbyCreationManager.Instance.OpenMenu(true);
            }

            GlobalsManager.IsChallenge = true;
           // SceneLoader.Inst.LoadSceneGroup("Challenge");
            
        }));

        buttons.GetChild(4).GetComponent<MultiElementButton>().onClick.AddListener(new Action(() =>
        {
          //  GeneralUILoader.Inst?.AbortSaveUI?.Invoke();
          //EventSystem.current.SetSelectedGameObject(null);
            GeneralUILoader.Inst?.LeaveSaveUI?.Invoke();
        }));
    }
    
    public void OpenMenu()
    {
        GeneralUILoader.Inst?.EnterSaveUI?.Invoke();
        Menu.ShowBase();
        Menu.SwapView("main");
        Menu.AllViews["main"].PossibleFirstButtons[0].Select();
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);
    }
}