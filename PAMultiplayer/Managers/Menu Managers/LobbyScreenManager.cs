using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Cpp2IL.Core.Extensions;
using Il2CppSystems.SceneManagement;
using Newtonsoft.Json;
using Steamworks;
using TMPro;
using UnityEngine;
using LobbyState = PAMultiplayer.Managers.SteamLobbyManager.LobbyState;

namespace PAMultiplayer.Managers.MenuManagers;

/// <summary>
/// Manages the Lobby menu that pops up on level enter
/// </summary>
public class LobbyScreenManager : MonoBehaviour
{
    public static LobbyScreenManager Instance { get; private set; }
    public static Dictionary<ulong, string> SpecialColors = new();

    public UI_Menu LobbyMenu;
    
    readonly Dictionary<ulong, Transform> _playerList = new();
    
    Transform _playersList;
    GameObject _playerPrefab;

    Transform _queueList;
    GameObject _queueEntryPrefab;

    private MultiElementButton ResumeButton;
    private MultiElementButton QuitButton;

    //spawns the lobby GameObject from the assetBundle
    void Awake()
    {
        Instance = this;
        VGCursor.Inst.ShowCursor();

        Transform UiManager = PauseUIManager.Inst.transform.parent;
        GameObject lobbyGo;
            
        using (var stream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("PAMultiplayer.Assets.lobbymenuv2"))
        {
            var lobbyBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
            lobbyGo = Instantiate(lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[0]).Cast<GameObject>(),
                UiManager);
            _playerPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[1]).Cast<GameObject>();
            _queueEntryPrefab = lobbyBundle.LoadAsset(lobbyBundle.AllAssetNames()[2]).Cast<GameObject>();
          
            lobbyBundle.Unload(false);
        }
        
        lobbyGo.name = "PAM_Lobby";
        LobbyMenu = lobbyGo.GetComponent<UI_Menu>();
        
        _playersList = lobbyGo.transform.Find("PlayersList");
        _queueList = lobbyGo.transform.Find("QueueList");
        
        var buttons = lobbyGo.transform.Find("Pause Menu");
        ResumeButton = buttons.GetChild(0).GetComponent<MultiElementButton>();
        
        ResumeButton.onClick.AddListener(new Action(() =>
        {
            GameManager.Inst.UnPause();
        }));

        QuitButton = buttons.GetChild(1).GetComponent<MultiElementButton>();
        QuitButton.onClick.AddListener(new Action(() =>
        {
            SceneLoader.Inst.LoadSceneGroup("Arcade");
        }));
        
        if (!GlobalsManager.IsHosting)
        {
            SetButtonActive(false);
        }
        
        foreach (var friend in SteamLobbyManager.Inst.CurrentLobby.Members)
        {
            AddPlayerToLobby(friend.Id, friend.Name);
                
            if (SteamLobbyManager.Inst.GetIsPlayerLoaded(friend.Id))
            {
                SetPlayerLoaded(friend.Id, false);
            }
        }
            
        UpdateQueue();
    }

    private void Start()
    {
        StartCoroutine(ShowLobby().WrapToIl2Cpp());
    }

    IEnumerator ShowLobby()
    {
        yield return new WaitForUpdate();
        
        LobbyMenu.ShowBase();
        LobbyMenu.SwapView("main");
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);

        if (GlobalsManager.IsHosting)
        {
            ResumeButton.Select();
        }
        else
        {
            QuitButton.Select();
        }

    }
    private void OnDestroy()
    {
        if (LobbyMenu)
        {
            LobbyMenu.HideAll();
            Destroy(LobbyMenu.gameObject);
        }
    }

    public void AddPlayerToLobby(SteamId player, string playerName)
    {
        if (_playerList.ContainsKey(player))
        {
            return;
        }

        var playerEntry = Instantiate(_playerPrefab, _playersList).Cast<GameObject>().transform;

        if (SpecialColors.TryGetValue(player, out var hex))
        {
            playerName = $"<color=#{hex}>{playerName}";
        }

        playerEntry.GetComponentInChildren<TextMeshProUGUI>().text = playerName;
        playerEntry.GetComponent<UI_Text>().ShowCustom(0.2f);
        _playerList.Add(player, playerEntry);
        
        if (GlobalsManager.IsHosting)
        {
            SetButtonActive(SteamLobbyManager.Inst.IsEveryoneLoaded); 
        }
    }

    public void RemovePlayerFromLobby(SteamId player)
    {
        if (_playerList.TryGetValue(player, out var value))
        {
            value.GetComponent<UI_Text>().HideCustom(0.2f);
            Destroy(value.gameObject, 0.3f);
            _playerList.Remove(player);
            
           
            if (GlobalsManager.IsHosting)
            {
                SetButtonActive(SteamLobbyManager.Inst.IsEveryoneLoaded); 
            }
        }
    }

    public void SetPlayerLoaded(SteamId player, bool playSound = true)
    {
        if (_playerList.TryGetValue(player, out var value))
        {
            if (playSound)
            {
                AudioManager.Inst?.PlaySound("Add", 1);
            }

            var loadIcon = value.GetChild(1).GetComponent<TextMeshProUGUI>();
            loadIcon.text = "▓";

            UIStateManager.inst.RefreshTextCache(loadIcon, "▓");

            if (GlobalsManager.IsHosting)
            {
                SetButtonActive(SteamLobbyManager.Inst.IsEveryoneLoaded); 
            }
        }
    }

    public void StartLevel()
    {
        if (GlobalsManager.HasStarted)
        {
            return;
        }
        SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState", LobbyState.Playing.ToString());
        GlobalsManager.HasStarted = true;
        SteamLobbyManager.Inst.HideLobby();
        SteamLobbyManager.Inst.UnloadAll();
        
        LobbyMenu.HideAll();
        GameManager.Inst.UnPause();
    }

    public void UpdateQueue()
    {
        for (int i = 0; i < _queueList.childCount; i++)
        {
            Destroy(_queueList.GetChild(i).gameObject);
        }

        string queueData = SteamLobbyManager.Inst.CurrentLobby.GetData("LevelQueue");
        if (string.IsNullOrEmpty(queueData))
        {
            LobbyMenu.transform.Find("Queue Title").gameObject.SetActive(false);
            return;
        }
        
        List<string> queue =
            JsonConvert.DeserializeObject<List<string>>(queueData);

        if (queue.Count == 0)
        {
            LobbyMenu.transform.Find("Queue Title").gameObject.SetActive(false);
            return;
        }
        
        foreach (var queueEntry in queue)
        {
            var entry = Instantiate(_queueEntryPrefab, _queueList).Cast<GameObject>();
            entry.GetComponentInChildren<TextMeshProUGUI>().text = queueEntry;
        }
    }
    
    void SetButtonActive(bool active)
    {
        if (ResumeButton.interactable == active) return;
        
        ResumeButton.interactable = active;
        ResumeButton.enabled = active;

        Color newColor = active ? Color.white : new Color(1, 1, 1, 0.188f);
        ResumeButton.targetGraphics.subGraphics[0].color = newColor;
        
        ResumeButton.GetComponent<UI_Button>().PlayGlitch(0.6f, 0, 0.5f);
    }
}
