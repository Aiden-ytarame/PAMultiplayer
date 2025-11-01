using System;
using Il2CppSystems.SceneManagement;
using Steamworks.Data;
using TMPro;
using UnityEngine;

namespace PAMultiplayer.Managers;

public class MenuSelectionManager : MonoBehaviour
{
    public static MenuSelectionManager Instance { get; private set; }
    public UI_Menu Menu;
    private TextMeshProUGUI LobbyCount;
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
        
        buttons.GetChild(1).GetComponent<MultiElementButton>().onClick.AddListener(new Action(async void () =>
        {
            Menu.HideAllInstant();
            GeneralUILoader.Inst?.LeaveSaveUI?.Invoke();
            LobbyQuery query = new LobbyQuery();
            var lobbies = await query.WithMaxResults(1).RequestAsync();

            if (lobbies != null && lobbies.Length > 0)
            {
                GlobalsManager.IsHosting = false;
                GlobalsManager.IsMultiplayer = true; 
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                lobbies[0].Join();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

        }));

        LobbyCount = buttons.GetChild(1).FindChild("QueueIcon/Text").GetComponent<TextMeshProUGUI>();
        
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
    
    public async void OpenMenu()
    {
        try
        {
            GeneralUILoader.Inst?.EnterSaveUI?.Invoke();
            Menu.ShowBase();
            Menu.SwapView("main");
            Menu.AllViews["main"].PossibleFirstButtons[0].Select();
            CameraDB.Inst.SetUIVolumeWeightIn(0.2f);
            
            LobbyCount.text = "...";
            UIStateManager.Inst.RefreshTextCache(LobbyCount, "...");
            
            LobbyQuery query = new LobbyQuery();
            var lobbies = await query.WithMaxResults(10).RequestAsync();

            if (!LobbyCount)
            {
                return;
            }
            
            if (lobbies != null)
            {
                LobbyCount.text = lobbies.Length.ToString();
                UIStateManager.Inst.RefreshTextCache(LobbyCount, lobbies.Length.ToString());
            }
            else
            {
                LobbyCount.text = "0";
                UIStateManager.Inst.RefreshTextCache(LobbyCount, "0");
            }
        }
        catch (Exception e)
        {
            PAM.Logger.LogError(e);
        }
    }
}