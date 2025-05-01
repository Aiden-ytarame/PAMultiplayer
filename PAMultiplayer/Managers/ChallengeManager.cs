using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystems.SceneManagement;
using AttributeNetworkWrapper.Core;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Patch;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using LobbyState = PAMultiplayer.Managers.SteamLobbyManager.LobbyState;
using Random = UnityEngine.Random;

namespace PAMultiplayer.Managers;

public class ChallengeManager : MonoBehaviour
{
    public static ChallengeManager Inst { get; private set; }
    public Transform playersParent;
    public int ColorID { get; private set; }
    public DataManager.BeatmapTheme ChallengeTheme { get; private set; }
    
    private readonly List<VoterCell> _levelButtons = new(6);
    private readonly List<VGLevel> _levelsToVote = new(6);
    private readonly Dictionary<VGLevel, int> _loadedLevels = new(6);
    private readonly Dictionary<VGPlayer, VGLevel> _votes = new(16);
    private readonly Dictionary<ulong, Tuple<short[], int, int>> _songData = new(); //struct here crashes bepinex lmao
    
    private bool _votingStarted = false;
    
    public delegate void OnVoteChangedSignature(VGLevel newLevel);
    public event OnVoteChangedSignature OnVoteChanged;

    #region MonoBehaviour Methods

       private void Awake()
    {
        if (Inst)
        {
            Destroy(this);
            return;
        }
        Inst = this;
        ColorID =  Shader.PropertyToID("_BaseColor");
        
        ChallengeTheme = new();
        ChallengeTheme.guiAccent = Color.white;
        ChallengeTheme.playerColors.Add(new(0.980f, 0.361f, 0.4f));
        ChallengeTheme.playerColors.Add(new(0.361f, 0.545f, 0.980f));
        ChallengeTheme.playerColors.Add(new(0.024f, 0.839f, 0.626f));
        ChallengeTheme.playerColors.Add(new(1f, 0.820f, 0.4f));
        
        CameraDB.Inst.foregroundCamera.orthographicSize = 20;
        CameraDB.Inst.backgroundCamera.orthographicSize = 20;
        CameraDB.Inst.CamerasParent.position = Vector3.zero;
        
        var cells = GameObject.Find("Voter/Canvas/Cells").transform;

        for (int i = 0; i < cells.childCount; i++)
        {
            _levelButtons.Add(cells.GetChild(i).gameObject.AddComponent<VoterCell>());
        }
        VGPlayerManager.Inst.LockPlayerAdding(false);
        
        
        if (GlobalsManager.IsMultiplayer)
        {
            StartCoroutine(InitMultiplayer().WrapToIl2Cpp());
            return;
        }
        
        if (MultiplayerDiscordManager.Instance)
        { 
            MultiplayerDiscordManager.Instance.SetChallengePresence();  
        }
        
        PickLevelForVoting();
      
        VGLevel level = _levelsToVote[0];
        if (!level)
        {
            PAM.Logger.LogFatal("client asked for level id not in the picked challenge levels");
        }
        
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject)
            {
                Destroy(vgPlayerData.PlayerObject.gameObject);
                vgPlayerData.PlayerObject = null;
            }
        }
        
        if (VGPlayerManager.Inst.players.Count == 0)
        {
            VGPlayerManager.Inst.players.Add(new VGPlayerManager.VGPlayerData(){PlayerID = 0, ControllerID = 0});
        }
        
        VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, new Action<int, Vector3>((_,_2) => {}), new Action<Vector3>((_) => {}),new Action<Vector3>((_) => {}), 3);
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            vgPlayerData.PlayerObject.SetColor(ChallengeTheme.GetPlayerColor(vgPlayerData.PlayerID), ChallengeTheme.guiAccent);
        }
        
        StartCoroutine(ShowLevels().WrapToIl2Cpp());
        
        Transform skip = PauseUIManager.Inst.transform.Find("Pause Menu")?.Find("Skip Queue Level");
        if (skip)
        {
            skip.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        LSEffectsManager.Inst.UpdateChroma(0.1f);
        LSEffectsManager.Inst.UpdateBloom(1, 0.1f);
        LSEffectsManager.Inst.UpdateLensDistort(0.25f, new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (!GlobalsManager.IsReloadingLobby)
        {
            MultiplayerDiscordManager.Instance.SetMenuPresence();
            GlobalsManager.IsChallenge = false;
        }
        
        //attempt on fixing dupe players
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject)
            {
                Destroy(vgPlayerData.PlayerObject.gameObject);
                vgPlayerData.PlayerObject = null;
            }
        }
    }
    #endregion
    
    #region Voting

    async void PickLevelForVoting()
    {
        try
        {
            if (ArcadeLevelDataManager.Inst.ArcadeLevels.Count < 6)
            {
                PAM.Logger.LogError(
                    $"Not enough levels loaded or downloaded, minimum [6], loaded [{ArcadeLevelDataManager.Inst.ArcadeLevels.Count}]");
                SceneLoader.Inst.manager.ClearLoadingTasks();
                SceneLoader.Inst.LoadSceneGroup("Menu");
                return;
            }

            while (true)
            {
                var level = ArcadeLevelDataManager.Inst.ArcadeLevels[Random.RandomRange(0, ArcadeLevelDataManager.Inst.ArcadeLevels.Count)];
                
                if (!_levelsToVote.Contains(level))
                {
                    if (!GlobalsManager.IsMultiplayer)
                    {
                        _levelsToVote.Add(level);
                    }
                    else if (level.SteamInfo != null)
                    {
                        var result = await SteamUGC.QueryFileAsync(level.SteamInfo.ItemID);
                        if (!result.HasValue || result.Value.Result != Result.OK)
                        {
                            continue;
                        }

                        _levelsToVote.Add(level);
                    }
                }

                if (_levelsToVote.Count >= 6)
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            PAM.Logger.LogError(e);
        }
    }

    VGLevel PickLevel()
    {
        VGLevel level = _levelsToVote[0];
        List<VGLevel> choices = new();
        
        int highestVote = 0;
        Dictionary<VGLevel, int> counts = new();
        
        foreach (var vote in _votes)
        {
            counts.TryAdd(vote.Value, 0);
            int voteCount = ++counts[vote.Value];
            
            if (voteCount > highestVote)
            {
                choices.Clear();
                choices.Add(vote.Value);
                highestVote = voteCount;
            }
            
            else if (voteCount == highestVote)
            {
                choices.Add(vote.Value);
            }
        }
        
        if(choices.Count > 0)
        {
            level = choices[Random.Range(0, choices.Count)];
        }
        return level;
    }

    public void StartVoting_Client()
    {
        if (_levelsToVote.Count >= 6 && CheckAllLevelsReady(false))
        {
            _votingStarted = true;
            SteamLobbyManager.Inst.UnloadAll();
            StartVoting();
        }
    }
    
    void StartVoting()
    {
        SpawnPlayers_Multiplayer();
        StartCoroutine(ShowLevels().WrapToIl2Cpp());
    }

    public void PlayerVote(VGPlayer player, VGLevel level)
    {
        _votes[player] = level;
        OnVoteChanged?.Invoke(level);
    }
    
    public void PlayerVote(ulong player, ulong level)
    {
        var playerObj = GlobalsManager.Players[player].VGPlayerData?.PlayerObject;
        if (!playerObj)
        {
            return;
        }

        var vgLevel = _levelsToVote.Find(x => x.SteamInfo.ItemID == level);
        if (vgLevel != null)
        {
            _votes[playerObj] = vgLevel;
        }
    }

    public void SetVoteWinner(ulong level)
    {
        foreach (var levelButton in _levelButtons)
        {
            if (levelButton.Level.SteamInfo.ItemID != level)
            {
                levelButton.Hide();
            }
        }
    }
    
    void SetVoteWinner(VGLevel level)
    {
        foreach (var levelButton in _levelButtons)
        {
            if (levelButton.Level != level)
            {
                levelButton.Hide();
            }
        }

        if (GlobalsManager.IsMultiplayer)
        {
           Multi_VoteWinner(level.SteamInfo.ItemID);
        }
      
    }

    [MultiRpc]
    public static void Multi_VoteWinner(ulong level)
    {
        Inst.SetVoteWinner(level);
    }
    
    #endregion

    #region Level entry download
    
    //clients setting up server sent levels
    public async void CreateLevelEntry(ulong id, int index)
    {
        try
        {
            VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id.ToString());
            if (level != null) 
            {
                if (!_levelsToVote.Contains(level))
                {
                    _levelsToVote.Add(level);
                    _loadedLevels[level] = 2;
                }

                if (_levelsToVote.Count >= 6)
                {
                    CheckAllLevelsReady();
                }
                return;
            }

            level = new()
            {
                SteamInfo = new()
                {
                    ItemID = id
                }
            };
            
            _levelsToVote.Add(level);
            _loadedLevels.TryAdd(level, 0);
            
            var result = await SteamUGC.QueryFileAsync(id);
            if (!result.HasValue || result.Value.Result != Result.OK)
            {
                return;
            }

            level.TrackName = result.Value.Title;
            level.ArtistName = "Artist";
            
            StartCoroutine(GetImageFromWorkshop(level, result.Value.PreviewImageUrl).WrapToIl2Cpp());
        }
        catch (Exception e)
        {
            PAM.Logger.LogError(e);
        }
    }
    
    public bool GetVGLevel(ulong levelId, out Tuple<short[], int, int> songData)
    {
        return _songData.TryGetValue(levelId, out songData);
    }
    
    IEnumerator GetImageFromWorkshop(VGLevel level, string url)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        
        yield return www.SendWebRequest();
        
        if (www.isNetworkError || www.isHttpError)
        {
            PAM.Logger.LogError(www.error);
            level.AlbumArt = null;
            yield break;
        }
        
        var texture = DownloadHandlerTexture.GetContent(www);

        level.AlbumArt = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        _loadedLevels[level]++;

        CheckAllLevelsReady();
    }
    
    IEnumerator GetSongData(VGLevel level)
    {
        string path = "";
        if (File.Exists(level.BaseLevelData.LocalFolder + "/audio.ogg"))
        {
            path = level.BaseLevelData.LocalFolder + "/audio.ogg";
        }
        else if (File.Exists(level.BaseLevelData.LocalFolder + "/level.ogg"))
        {
            path = level.BaseLevelData.LocalFolder + "/level.ogg";
        }

        AudioClip clip;
        UnityWebRequest webr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS);
        yield return webr.SendWebRequest(); //cant be inside Try block
        
        try
        {
            clip = DownloadHandlerAudioClip.GetContent(webr);
        }
        catch (Exception e)
        {
            PAM.Logger.LogError(e);
            webr.Dispose();
            yield break;
        }
      
        webr.Dispose();

        
        int frequency;
        int divider = 1;
        while (true)
        {
            frequency = clip.frequency / divider;
            if (frequency <= 24000)
            {
                break;
            }

            divider *= 2;
        }
        
        //float[] does not work with GetData
        var songData = new Il2CppStructArray<float>(Mathf.FloorToInt(4/*seconds*/ * clip.frequency * clip.channels));
        short[] songDataShort = new short[Mathf.FloorToInt(4/*seconds*/ * frequency * clip.channels)];
        
        clip.GetData(songData, clip.samples / 2);

        //reduces frequency to 22-24k hz~
        int index = 0;
        for (var i = 0; i < songData.Count; i += (divider - 1) * clip.channels)
        {
            for (int j = 0; j < clip.channels; j++)
            {
                songDataShort[index] = (short)(songData[i] * short.MaxValue);
                index++;
                i++;
            }
            
        }
      
        //  clip.UnloadAudioData();
        //  Destroy(clip);
        
        _songData.Add(level.SteamInfo.ItemID, new Tuple<short[], int, int>(songDataShort, frequency, clip.channels));
    }
    
    public void SetLevelSong(ulong id, AudioClip clip)
    {
        var level = _levelsToVote.Find(x => x.SteamInfo.ItemID == id);
        if (level)
        {
            level.LevelMusic = clip;
            _loadedLevels[level]++;
        }

        CheckAllLevelsReady();
    }
    
    #endregion
    
    IEnumerator ShowLevels()
    {
        yield return new WaitForSeconds(1f);

        for (int i = 0; i < 6; i++)
        {
            var level = _levelsToVote[i];
            var button = _levelButtons[i];
          
            button.SetLevelData(level);  
            button.Show();
            AudioManager.Inst.PlayMusic(level.LevelMusic);
            
            if (level.LevelMusic.length > 5)
            {
                AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = level.LevelMusic.length / 2;
            }
           
            yield return new WaitForSecondsRealtime(2.5f);
        }

        foreach (var levelButton in _levelButtons)
        {
            levelButton.EnableVoting();
        }
        
        yield return new WaitForSecondsRealtime(5f);

        if (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting)
        {
            yield break;
        }
        
        VGLevel nextLevel = PickLevel();
        
        if (nextLevel == null)
        {
            SceneLoader.Inst.LoadSceneGroup("Menu");
            yield break;
        }
        
        SetVoteWinner(nextLevel);
        
        yield return new WaitForSecondsRealtime(1.5f);

        GlobalsManager.LevelId = nextLevel.SteamInfo.ItemID.ToString();
        
        ArcadeManager.Inst.CurrentArcadeLevel = nextLevel;
        GlobalsManager.IsReloadingLobby = true;
      
        //only needed for singleplayer
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject)
            {
                Destroy(vgPlayerData.PlayerObject.gameObject);
                vgPlayerData.PlayerObject = null;
            }
        }
        
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }
    
    IEnumerator InitMultiplayer()
    {
        gameObject.AddComponent<NetworkManager>();
        
        AddLoadingScreenTasks();
        if (!GlobalsManager.IsReloadingLobby)
        {
            if (GlobalsManager.IsHosting)
            {
                SteamLobbyManager.Inst.CreateLobby();
                yield return new WaitUntil(new Func<bool>(() => SteamLobbyManager.Inst.InLobby));
            }
            else
            {
                SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                yield return new WaitUntil(new Func<bool>(() => AttributeNetworkWrapper.NetworkManager.Instance.TransportActive));
                yield return new WaitUntil(new Func<bool>(() => GlobalsManager.HasLoadedAllInfo ));
            }
        }
        
        if (MultiplayerDiscordManager.Instance)
        { 
            MultiplayerDiscordManager.Instance.SetChallengePresence();  
        }

        GlobalsManager.IsReloadingLobby = false;
        
        if (GlobalsManager.IsHosting)
        {
            SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState", ((ushort)LobbyState.Challenge).ToString());
            
            PickLevelForVoting();
            
            yield return new WaitUntil(new Func<bool>(() => _levelsToVote.Count >= 6));
            
            Transform skip = PauseUIManager.Inst.transform.Find("Pause Menu")?.Find("Skip Queue Level");
            if (skip)
            {
                skip.gameObject.SetActive(false);
            }
            
            var timer = new Stopwatch();
            timer.Start();

            List<ulong> ids = new();
            foreach (var vgLevel in _levelsToVote)
            {
                ids.Add(vgLevel.SteamInfo.ItemID);
                StartCoroutine(GetSongData(vgLevel).WrapToIl2Cpp());
            }
            
            yield return new WaitUntil(new Func<bool>(() => _songData.Count >= 6));
            
            timer.Stop();
            PAM.Logger.LogDebug($"took {timer.ElapsedMilliseconds}ms to get level data");
          
            Multi_CheckLevelIds(ids);
            SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
        }
        else
        {
            yield break;
        }
        
        yield return new WaitUntil(new Func<bool>(() => SteamLobbyManager.Inst.IsEveryoneLoaded));
        yield return null;
        SteamLobbyManager.Inst.UnloadAll();
        PauseLobbyPatch.Multi_StartLevel();
        StartVoting();
    }

    [MultiRpc]
    public static async void Multi_CheckLevelIds(List<ulong> levelIds)
    {
        try
        {
            List<ulong> unknownLevelIds = new();
            for (var i = 0; i < levelIds.Count; i++)
            {
                ulong levelId = levelIds[i];
                bool hasLevel = false;
                do
                {
                    if (ArcadeLevelDataManager.Inst.GetLocalCustomLevel(levelId.ToString()))
                    {
                        hasLevel = true;
                        break;
                    }

                    await Task.Delay(500);
                } while (SteamWorkshopFacepunch.inst.isLoadingLevels);

                if (!hasLevel)
                {
                    unknownLevelIds.Add(levelId);
                }
                Inst.CreateLevelEntry(levelId, i);
            }
            
            PAM.Logger.LogInfo($"requesting audio of [{unknownLevelIds.Count}] levels");
            Server_UnknownLevelIds(null, unknownLevelIds);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    [ServerRpc]
    public static void Server_UnknownLevelIds(ClientNetworkConnection conn, List<ulong> levelIds)
    {
        if (!Inst)
        {
            return;
        }
                                                                                     
        PAM.Logger.LogInfo($"Client [{conn.Address}] has asked for [{levelIds.Count}] levels");

        foreach (var levelId in levelIds)
        {

            if (!Inst.GetVGLevel(levelId, out var songData))
            {
                PAM.Logger.LogFatal("client asked for level id not in the picked challenge levels");
                continue;
            }

            const int separator = 131000;
            int offset = 0;
            while (true)
            {
                if (offset + separator < songData.Item1.Length)
                {
                    ArraySegment<short> segment = new(songData.Item1, offset, separator);
                    Client_AudioData(conn, levelId, songData.Item2, songData.Item3, segment, false);
                    offset += separator + 1;
                }
                else
                {
                    ArraySegment<short> segment = new ArraySegment<short>(songData.Item1, offset,
                        Mathf.FloorToInt(songData.Item1.Length - offset));
                    Client_AudioData(conn, levelId, songData.Item2, songData.Item3, segment, true);
                    break;
                }
            }
        }
    }

    private static readonly List<float> AudioDataBuffer = new(400000);
    private static ulong _lastId;
    
    [ClientRpc]
    public static void Client_AudioData(ClientNetworkConnection conn, ulong audioID, int frequency, int channels, Span<short> songData, bool last)
    {
        PAM.Logger.LogInfo("Received audio data");
    
        if (audioID != _lastId)
        {
            _lastId = audioID;
            AudioDataBuffer.Clear();
        }
        
        foreach (var f in songData)
        {
            AudioDataBuffer.Add((float)f / short.MaxValue); //add range doesnt work, and ToArray may allocate a lot of memory
        }

        if (!last)
        {
            return;
        }
      
        PAM.Logger.LogInfo($"Got all audio data for level [{audioID}]");
        
        var newClip = AudioClip.Create(audioID.ToString(), AudioDataBuffer.Count / channels, channels, frequency, false);
        newClip.SetData(AudioDataBuffer.ToArray(), 0); //this to array is specially bad cuz its making 2 copies, may fix later
        newClip.LoadAudioData();
        
        AudioDataBuffer.Clear();

        if (Inst)
        {
            Inst.SetLevelSong(audioID, newClip);
        }
    }
    
    
    void SpawnPlayers_Multiplayer()
    {
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if(vgPlayerData.PlayerObject)
                vgPlayerData.PlayerObject.PlayerDeath(0);
        }
        VGPlayerManager.Inst.players.Clear();

        if (GlobalsManager.IsHosting)
        {
            if (GlobalsManager.Players.Count == 0)
            {
                //player 0 is never added, so we add it here
                var newData = new VGPlayerManager.VGPlayerData() { PlayerID = 0, ControllerID = 0 };
                VGPlayerManager.Inst.players.Add(newData);
                GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayerId, new PlayerData(newData, SteamClient.Name));
            }
            else
            {
                foreach (var vgPlayerData in GlobalsManager.Players)
                {
                    VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
                }
            }
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, new Action<int, Vector3>((_,_2) => {}), new Action<Vector3>((_) => {}),new Action<Vector3>((_) => {}), 3);
        }
        else
        {
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
            }
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, new Action<int, Vector3>((_,_2) => {}), new Action<Vector3>((_) => {}),new Action<Vector3>((_) => {}), 3);
        }

        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            vgPlayerData.PlayerObject?.SetColor(ChallengeTheme.GetPlayerColor(vgPlayerData.PlayerID), ChallengeTheme.guiAccent);
        }
    }
    
    void AddLoadingScreenTasks()
    {
        if (GlobalsManager.IsHosting)
        {
            SceneLoader.Inst.manager.AddToLoadingTasks( "Creating Lobby", Task.Run(async () =>
            {
                while (!SteamLobbyManager.Inst.InLobby)
                {
                    await Task.Delay(100);
                }
            }).ToIl2Cpp());
          
            SceneLoader.Inst.manager.AddToLoadingTasks("Setting up chosen levels", Task.Run(async () =>
            {
                while (_songData.Count < 6)
                {
                    await Task.Delay(100);
                }
            }).ToIl2Cpp()); 
                
            SceneLoader.Inst.manager.AddToLoadingTasks("Waiting other players", Task.Run(async () =>
            {
                while (!SteamLobbyManager.Inst.InLobby || !SteamLobbyManager.Inst.IsEveryoneLoaded)
                {
                    await Task.Delay(0);
                }
            }).ToIl2Cpp());
        }
        else
        {
            SceneLoader.Inst.manager.AddToLoadingTasks("Waiting other players", Task.Run(async () =>
            {
                while (!_votingStarted)
                {
                    await Task.Delay(0);
                }
            }).ToIl2Cpp());
        }
    }
    
    private bool CheckAllLevelsReady(bool setLoaded = true)
    {
        foreach (var loadValue in _loadedLevels.Values)
        {
            if (loadValue != 2)
            {
                return false;
            }
        }

        if (setLoaded)
        {
           SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
        }
        return true;
    }
}

public class VoterCell : MonoBehaviour
{
    private GhostUIElement _ghostUIElement;
    private Image _border;
    private int _playerCounter;
    private bool _isSelected;
    
    public VGLevel Level { get; private set; }
    private void Awake()
    {
        _ghostUIElement = GetComponent<GhostUIElement>();
        _ghostUIElement.HideInstant();
        _border = GetComponent<Image>();
        ChallengeManager.Inst.OnVoteChanged += OnVoteChanged;
    }
    
    private void OnVoteChanged(VGLevel newlevel)
    {
        if (newlevel != Level)
        {
            SetSelected(false);
        }
    }

    public void Show()
    {
        _ghostUIElement.Show();
    }

    public void Hide()
    {
        _ghostUIElement.Hide();
        gameObject.GetComponent<BoxCollider2D>().enabled = false;
    }
    public void SetLevelData(VGLevel level)
    {
        Level = level;
      
        if (level.AlbumArt)
        {
            transform.GetChild(1).GetChild(0).GetComponent<Image>().sprite = level.AlbumArt;
        }
            
        UIStateManager.Inst.RefreshTextCache(transform.GetChild(2).GetComponent<TextMeshProUGUI>(), level.TrackName);
        UIStateManager.Inst.RefreshTextCache(transform.GetChild(3).GetComponent<TextMeshProUGUI>(), level.ArtistName);
    }

    public void EnableVoting()
    {
        gameObject.GetComponent<BoxCollider2D>().enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {

        var player = other.transform.parent.parent.gameObject.GetComponent<VGPlayer>();
        if (!player)
        {
            return;
        }

        if (GlobalsManager.IsMultiplayer && !player.IsLocalPlayer())
        {
            return;
        }

        _playerCounter++;
        SetSelected(true);
        AudioManager.Inst.PlayMusic(Level.LevelMusic);
        AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = Level.LevelMusic.length / 2;

        if (!GlobalsManager.IsMultiplayer || GlobalsManager.IsHosting)
        {
            ChallengeManager.Inst.PlayerVote(player, Level);
            return;
        }
        
        Server_LevelVoted(null, Level.SteamInfo.ItemID);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.transform.parent.parent.gameObject.GetComponent<VGPlayer>();
        if (!player)
        {
            return;
        }
        if (GlobalsManager.IsMultiplayer && !player.IsLocalPlayer())
        {
            return;
        }
        
        _playerCounter--;
        if (_playerCounter <= 0)
        {
            _playerCounter = 0;
            SetSelected(false);
        }
    }

    public void SetSelected(bool selected)
    {
        if (selected == _isSelected)
        {
            return;
        }
        _isSelected = selected;
        
        _ghostUIElement.Stutter(false);
        Color color = selected ? new Color(1f, 0.815f, 0f) : Color.white;
        _border.color = color;
    }

    [ServerRpc]
    public static void Server_LevelVoted(ClientNetworkConnection conn, ulong level)
    {
        if (!conn.TryGetSteamId(out SteamId id))
        {
            return;
        }
        
        if (ChallengeManager.Inst)
        {
            ChallengeManager.Inst.PlayerVote(id, level);  
        }
    }
}