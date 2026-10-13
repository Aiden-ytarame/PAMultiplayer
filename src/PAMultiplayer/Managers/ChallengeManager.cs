using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AttributeNetworkWrapperV2;
using DG.Tweening;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Data;
using PAMultiplayer.Patch;
using PAMultiplayer.UI;
using Steamworks;
using Systems.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Task = System.Threading.Tasks.Task;

namespace PAMultiplayer.Managers;

public partial class ChallengeManager : MonoBehaviour
{
    class LoadState
    {
        public bool AudioLoaded;
        public bool ImageLoaded;

        public LoadState(bool audio = false, bool image = false)
        {
            AudioLoaded = audio;
            ImageLoaded = image;
        }

        public bool Loaded => AudioLoaded && ImageLoaded;
    }
    public static ChallengeManager Inst { get; private set; }
    public static readonly List<string> RecentLevels = new();
    
    public Transform playersParent;
    public int ColorID { get; private set; }
    public DataManager.BeatmapTheme ChallengeTheme { get; private set; }
    
    private readonly List<VoterCell> _levelButtons = new(6);
    private readonly List<VGLevel> _levelsToVote = new(6);
    private readonly Dictionary<VGLevel, LoadState> _loadedLevels = new(6);
    private readonly Dictionary<VGPlayer, VGLevel> _votes = new(16);
    
    private SongData _songData = new(); //struct here crashes bepinex lmao

    private Image _bgSlider;
    private Sequence _sequence = DOTween.Sequence();
    
    private bool _votingStarted;
    public AlbumArtManager AlbumArtManager = new();
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
        CameraDB.Inst.CamerasRoot.position = Vector3.zero;
        
        var cells = GameObject.Find("Voter/Canvas/Cells").transform;
        _bgSlider = GameObject.Find("Voter/Canvas/BGBar").GetComponent<Image>();
        _bgSlider.rectTransform.localScale = Vector3.one with { y = 0 };
        _bgSlider.color = _bgSlider.color with { a = 0.08f };
        
        _sequence.Append(DOTween.ToAlpha(() => _bgSlider.color, x => _bgSlider.color = x, 0.15f, 0.05f)
            .SetEase(Ease.OutExpo));
        _sequence.Append(DOTween.ToAlpha(() => _bgSlider.color, x => _bgSlider.color = x, 0f, 1.4f)
            .SetEase(Ease.InSine));
        _sequence.Pause();

        for (int i = 0; i < cells.childCount; i++)
        {
            _levelButtons.Add(cells.GetChild(i).gameObject.AddComponent<VoterCell>());
        }
        
        SceneLoader.Inst.manager.AddToLoadingTasks("Loading songs", Task.Run(async () =>
        {
            while (_levelsToVote.Count < 6)
            {
                await Task.Delay(100);
                SceneLoader.Inst?.manager?.UpdateTaskStatus("Loading songs", $"<color=#FFD000>[ Loading ]</color> {_levelsToVote.Count}/6");
            }

            SceneLoader.Inst?.manager?.ResetTaskStatus("Loading songs");
        }), "<color=#FFD000>[ Loading ]</color> 0/6"); 
        
        VGPlayerManager.Inst.InitPlayerStatus();
        
        if (GlobalsManager.IsMultiplayer)
        {
            VGPlayerManager.Inst.LockPlayerAdding(false);
            StartCoroutine(InitMultiplayer());
            return;
        }
        
        if (MultiplayerDiscordManager.Instance)
        { 
            MultiplayerDiscordManager.Instance.SetChallengePresence();  
        }

        StartCoroutine(PostAwake());
    }

    IEnumerator PostAwake()
    {
        yield return PickLevelForVoting();
      
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
        
        VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_,_,_) => {}, _ => {},(_, _, _) => {}, 3);
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            vgPlayerData.PlayerObject.SetColor(ChallengeTheme.GetPlayerColor(vgPlayerData.PlayerID), ChallengeTheme.guiAccent);
        }
        
        StartCoroutine(ShowLevels());
        
        Transform skip = PauseUIManager.Inst.transform.Find("sizer/Pause Menu")?.Find("Skip Queue Level");
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
        foreach (var vgLevel in _levelsToVote)
        {
            if (vgLevel && vgLevel.LevelMusic && !LevelMusicLoader.Manager.IsPlaybackClip(vgLevel.LevelMusic))
            {
                MPUtility.ReleaseClip(vgLevel.LevelMusic, vgLevel.IsBakedLevel);
            }
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
        
        AlbumArtManager.Dispose();
    }
    #endregion
    
    #region Voting

    IEnumerator PickLevelForVoting()
    {
        if (ArcadeLevelDataManager.Inst.ArcadeLevels.Count < 6)
        {
            PAM.Logger.LogError(
                $"Not enough levels loaded or downloaded, minimum [6], loaded [{ArcadeLevelDataManager.Inst.ArcadeLevels.Count}]");
            SceneLoader.Inst.manager.ClearLoadingTasks();
            MPUtility.DelayLoadScene("Menu");
            yield break;
        }

        int roundsToNotRepeat = Settings.NoRepeat.Value;
        if (roundsToNotRepeat != 4)
        {
            roundsToNotRepeat *= 6;
            if (RecentLevels.Count > roundsToNotRepeat)
            {
                RecentLevels.RemoveRange(0, RecentLevels.Count - roundsToNotRepeat);
            }
        }

        if (RecentLevels.Count + 6 >= ArcadeLevelDataManager.Inst.ArcadeLevels.Count + (MPUtility.IsStoryModeBeaten() ? MPUtility.StoryLevelCount : 0))
        {
            PAM.Logger.LogInfo("Clearing out recent level list due to being full");
            RecentLevels.Clear();
        }

        string blacklistStr = Settings.ChallengeBlacklist.Value;

        List<VGLevel> nonRepeatLevels = new(ArcadeLevelDataManager.Inst.ArcadeLevels.Count + (MPUtility.IsStoryModeBeaten() ? MPUtility.StoryLevelCount : 0) - RecentLevels.Count);

        HashSet<string> comparer = new(RecentLevels);
        comparer.UnionWith(blacklistStr.Split('/', StringSplitOptions.RemoveEmptyEntries));

        if (MPUtility.IsStoryModeBeaten())
        {
            foreach (var storyLevel in MPUtility.GetStoryLevels())
            {
                if (!comparer.Contains(storyLevel.BaseLevelData.LevelID))
                {
                    nonRepeatLevels.Add(storyLevel);
                }
            }
        }
        
        foreach (var arcadeLevel in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (!comparer.Contains(arcadeLevel.BaseLevelData.LevelID))
            {
                nonRepeatLevels.Add(arcadeLevel);
            }
        }

        if (nonRepeatLevels.Count < 6)
        {
            PAM.Logger.LogError(
                $"Not enough non blacklisted levels found, minimum [6], loaded [{ArcadeLevelDataManager.Inst.ArcadeLevels.Count}]");
            SceneLoader.Inst.manager.ClearLoadingTasks();
            MPUtility.DelayLoadScene("Menu");
            yield break;
        }

        bool allowNonPublicLevels = Settings.AllowNonPublicLevels.Value;
        List<CoroutineWrapper> levelCoroutines = new List<CoroutineWrapper>(6);
        for (int i = 0; i < 24; i++)
        {
            int countLeft = 6 - _levelsToVote.Count - levelCoroutines.Count;
            while (countLeft > 0)
            {
                var level = nonRepeatLevels[Random.Range(0, nonRepeatLevels.Count)];
                nonRepeatLevels.Remove(level);
                
                var wrapper = this.WrapCoroutine(CheckLevel(level, allowNonPublicLevels));
                levelCoroutines.Add(wrapper);
                countLeft--;
            }


            bool end = false;
            while (levelCoroutines.Count > 0 && !end)
            {
                for (var i1 = 0; i1 < levelCoroutines.Count;)
                {
                    CoroutineWrapper curr = levelCoroutines[i1];
                    if (curr.IsCompleted)
                    {
                        levelCoroutines.RemoveAt(i1);
                        break;
                    }

                    i1++;
                }

                if (_levelsToVote.Count + levelCoroutines.Count < 6)
                {
                    end = true;
                }

                yield return null;
            }

            if (_levelsToVote.Count >= 6)
            {
                yield break;
            }
        }

        PAM.Logger.LogError(
            "Not enough levels found in too many attempts");
        SceneLoader.Inst.manager.ClearLoadingTasks();
        MPUtility.DelayLoadScene("Menu");
    }

    IEnumerator CheckLevel(VGLevel level, bool allowNonPublic)
    {
        if (GlobalsManager.IsMultiplayer)
        {
            if (!level.IsBakedLevel && level.SteamInfo != null)
            {
                var task = SteamManager.GetSteamLevel(level.SteamInfo.ItemID);
                yield return task.WaitUntilComplete();

                var result = task.Result;
                if (!result.HasValue
                    || result.Value.Visibility == SteamWorkshopLevel.VisibilityType.Private
                    || (result.Value.Visibility is SteamWorkshopLevel.VisibilityType.Unlisted or SteamWorkshopLevel.VisibilityType.Friends && !allowNonPublic))
                {
                    yield break;
                }
            }
            else if (!level.IsBakedLevel)
            {
                yield break;
            }
        }
        
        var audio = LevelMusicLoader.LoadAsync(level).WaitUntilComplete();
      
        if (!level.AlbumArt)
        {
            var albumTask = AlbumArtManager.LoadAlbumArtAsync(level.BaseLevelData.LevelID, level.BaseLevelData.LocalFolder);
            yield return albumTask.WaitUntilComplete();
            level.AlbumArt = albumTask.Result;
        }

        yield return audio;
        
        RecentLevels.Add(level.BaseLevelData.LevelID);
        _levelsToVote.Add(level);
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
    
    void StartVoting()
    {
        SpawnPlayers_Multiplayer();
        StartCoroutine(ShowLevels());
    }
    
    private void StartVoting_Client()
    {
        if (_levelsToVote.Count >= 6 && CheckAllLevelsReady(false))
        {
            _votingStarted = true;
            SteamLobbyManager.Inst.UnloadAll();
            StartVoting();
        }
    }
    
    [MultiRpc]
    private static void Multi_StartVoting()
    {
        if (!GlobalsManager.IsHosting)
        {
            Inst?.StartVoting_Client();
        }
    }
    
    public void PlayerVote(VGPlayer player, VGLevel level)
    {
        _votes[player] = level;
        OnVoteChanged?.Invoke(level);
    }
    
    public void PlayerVote(ulong player, string level)
    {
        var playerObj = GlobalsManager.Players[player].VGPlayerData?.PlayerObject;
        if (!playerObj)
        {
            return;
        }

        var vgLevel = _levelsToVote.Find(x => x.BaseLevelData.LevelID == level);
        if (vgLevel != null)
        {
            _votes[playerObj] = vgLevel;
        }
    }

    public void SetVoteWinner(string level)
    {
        foreach (var levelButton in _levelButtons)
        {
            if (levelButton?.Level?.BaseLevelData?.LevelID != level)
            {
                levelButton?.Hide();
            }
        }
    }
    
    void SetVoteWinner(VGLevel level)
    {
        if (GlobalsManager.IsMultiplayer)
        {
            CallRpc_Multi_VoteWinner(level.BaseLevelData.LevelID);
            return;
        }
        
        foreach (var levelButton in _levelButtons)
        {
            if (levelButton.Level != level)
            {
                levelButton.Hide();
            }
        }
    }

    [MultiRpc]
    private static void Multi_VoteWinner(string level)
    {
        Inst?.SetVoteWinner(level);
    }
    
    #endregion

    #region Level entry download
    
    //clients setting up server sent levels
    public async void CreateLevelEntry(string id, int index)
    {
        try
        {
            VGLevel level = MPUtility.LookForLevel(id);
            if (level) 
            {
                if (!_levelsToVote.Contains(level))
                {
                    _levelsToVote.Add(level);
                    level.AlbumArt = await AlbumArtManager.LoadAlbumArtAsync(level.BaseLevelData.LevelID, level.BaseLevelData.LocalFolder);

                    if (!level.LevelMusic)
                    {
                        await LevelMusicLoader.LoadAsync(level);
                    }
                    _loadedLevels[level] = new(true, true);
                }
                
                CheckAllLevelsReady();
                return;
            }

            if (!ulong.TryParse(id, out var steamId))
            {
                if (!GlobalsManager.IsHosting)
                {
                    SteamManager.Inst.EndClient();
                }
           
                ErrorScreen.CreateErrorScreen($"Host tried to send level {id} which is local and not story, disconnecting...");

                SceneLoader.Inst.manager.ClearLoadingTasks();
                MPUtility.DelayLoadScene("Menu");
                return;
            }

            level = new()
            {
                SteamInfo = new()
                {
                    ItemID = steamId
                }
            };
            
            _levelsToVote.Add(level);
            _loadedLevels.TryAdd(level, new());
            
            var result = await SteamUGC.QueryFileAsync(steamId);
            if (!result.HasValue || result.Value.Result != Result.OK)
            {
                PAM.Logger.LogError($"Failed to get workshop data for level id [{id}]");
             
                if (!GlobalsManager.IsHosting)
                {
                    SteamManager.Inst.EndClient();
                }
           
                ErrorScreen.CreateErrorScreen($"Host tried to send level {id} which is not available on workshop, disconnecting...");

                SceneLoader.Inst.manager.ClearLoadingTasks();
                MPUtility.DelayLoadScene("Menu");
                return;
            }

            level.TrackName = result.Value.Title;
            level.ArtistName = "Artist";
            level.CharterName = result.Value.Owner.Nickname;
            
            StartCoroutine(GetImageFromWorkshop(level, result.Value.PreviewImageUrl));
        }
        catch (Exception e)
        {
            PAM.Logger.LogError($"Error Creating Level Entry\n{e}");
        }
    }
    
    IEnumerator GetImageFromWorkshop(VGLevel level, string url)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        
        yield return www.SendWebRequest();
        
        if (www.result != UnityWebRequest.Result.Success)
        {
            PAM.Logger.LogError(www.error);
            level.AlbumArt = null;
            _loadedLevels[level].ImageLoaded = true;
            CheckAllLevelsReady();
            yield break;
        }
        
        var texture = DownloadHandlerTexture.GetContent(www);

        level.AlbumArt = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        _loadedLevels[level].ImageLoaded = true;

        CheckAllLevelsReady();
    }
    
    public void SetLevelSong(string id, AudioClip clip)
    {
        var level = _levelsToVote.Find(x => x.BaseLevelData.LevelID == id);
        if (level)
        {
            level.LevelMusic = clip;
            _loadedLevels[level].AudioLoaded = true;
        }

        CheckAllLevelsReady();
    }
    
    #endregion
    
    IEnumerator ShowLevels()
    {
        double timeSinceLastButton = Time.realtimeSinceStartupAsDouble;
        
        for (int i = 0; i < 6; i++)
        {
            var level = _levelsToVote[i];
            var button = _levelButtons[i];
            
            button.SetLevelData(level);  
            button.Show();
            
            if (level.LevelMusic)
            {
                AudioManager.Inst.ForcePlayMusic(level.LevelMusic);
                
                if (level.LevelMusic.length > 5)
                {
                    AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = level.LevelMusic.length / 2;
                }
            }
            
            timeSinceLastButton += 2.5;
            // ReSharper disable once AccessToModifiedClosure
            yield return new WaitUntil(() => timeSinceLastButton <= Time.realtimeSinceStartupAsDouble);
           //Used instead of waitForSeconds to account for lag
        }

        timeSinceLastButton += 5;
        
        foreach (var levelButton in _levelButtons)
        {
            levelButton.EnableVoting();
        }

        Vector3 scale = Vector3.one;
        do
        {
            scale.y = (float)(1.0 - (timeSinceLastButton - Time.realtimeSinceStartupAsDouble) / 5.0); //bg acts as a slider 
            _bgSlider.rectTransform.localScale = scale;
            yield return new WaitForUpdate();
        } while (timeSinceLastButton > Time.realtimeSinceStartupAsDouble);

        _sequence.Play();
        scale.y = 1.1f; //makes sure slider covers whole screen
        _bgSlider.rectTransform.localScale = scale;
        
        if (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting)
        {
            yield break;
        }
        
        VGLevel nextLevel = PickLevel();
        
        if (!nextLevel)
        {
            MPUtility.DelayLoadScene("Menu");
            yield break;
        }
        
        SetVoteWinner(nextLevel);
        
        yield return new WaitForSecondsRealtime(1.5f);

        GlobalsManager.LevelId = nextLevel.BaseLevelData.LevelID;
        ArcadeManager.Inst.CurrentArcadeLevel = nextLevel;
      
        //only needed for singleplayer
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject)
            {
                Destroy(vgPlayerData.PlayerObject.gameObject);
                vgPlayerData.PlayerObject = null;
            }
        }
        
        DownloadAndPlayFlow.Play(nextLevel);
    }

    IEnumerator InitMultiplayer()
    {
        gameObject.AddComponent<NetworkManager>();

        AddLoadingScreenTasks();
        if (PaMNetworkManager.PamInstance == null)
        {
            if (GlobalsManager.IsHosting)
            {
                SteamLobbyManager.Inst.CreateLobby();
                yield return new WaitUntil(() => SteamLobbyManager.Inst.InLobby);
            }
            else
            {
                SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                yield return new WaitUntil(() => AttributeNetworkWrapperV2.NetworkManager.Instance!.TransportActive);
                yield return new WaitUntil(() => PaMNetworkManager.PamInstance?.LobbyInfo.HasLoadedAllInfo == true);
            }
        }

        if (MultiplayerDiscordManager.Instance)
        {
            MultiplayerDiscordManager.Instance.SetChallengePresence();
        }

        if (!GlobalsManager.IsHosting)
        {
            yield break;
        }


        PaMNetworkManager.CallRpc_Multi_UpdateLobbyState((byte)SteamLobbyManager.LobbyState.Challenge);
        SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState",
            ((ushort)SteamLobbyManager.LobbyState.Challenge).ToString());
        Stopwatch watch = Stopwatch.StartNew();
        yield return PickLevelForVoting();
        watch.Stop();
        PAM.Logger.LogFatal($"Time To pick and confirm level voting {watch.Elapsed}");
        // yield return new WaitUntil(new Func<bool>(() => _levelsToVote.Count >= 6));

        Transform skip = PauseUIManager.Inst.transform.Find("sizer/Pause Menu")?.Find("Skip Queue Level");
        if (skip)
        {
            skip.gameObject.SetActive(false);
        }
   
        _songData.SongDataUpdated += count =>
        {
            SceneLoader.Inst?.manager?.UpdateTaskStatus("Setting up chosen levels", $"<color=#FFD000>[ Prepping ]</color> {count}/6");
        };
        
        List<string> ids = new();
        foreach (var vgLevel in _levelsToVote)
        {
            ids.Add(vgLevel.BaseLevelData.LevelID);
        }
        var timer = Stopwatch.StartNew();
        yield return _songData.SetupSongData(this, _levelsToVote);

        timer.Stop();
        PAM.Logger.LogFatal($"took {timer.Elapsed} to get level data");

        SceneLoader.Inst?.manager?.ResetTaskStatus("Setting up chosen levels");
       
        CallRpc_Multi_CheckLevelIds(ids);
        SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");


        yield return new WaitUntil(() => SteamLobbyManager.Inst.IsEveryoneLoaded);
        
        SceneLoader.Inst?.manager?.ClearLoadingTasks();
        SteamLobbyManager.Inst.UnloadAll();
        CallRpc_Multi_StartVoting();
        StartVoting();
    }
    
    
    [MultiRpc]
    private static void Multi_CheckLevelIds(List<string> levelIds)
    {
        if (!GlobalsManager.IsHosting)
        {
            Inst?.StartCoroutine(CheckLevelIds(levelIds));
        }
    }

    static IEnumerator CheckLevelIds(List<string> levelIds)
    {
        List<string> unknownLevelIds = new();
        for (var i = 0; i < levelIds.Count; i++)
        {
            string levelId = levelIds[i];
            bool hasLevel = false;
            do
            {
                if (MPUtility.LookForLevel(levelId))
                {
                    hasLevel = true;
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            } while (SteamWorkshopFacepunch.inst.isLoadingLevels);

            if (!hasLevel)
            {
                unknownLevelIds.Add(levelId);
            }

            Inst.CreateLevelEntry(levelId, i);
        }

        PAM.Logger.LogInfo($"requesting audio of [{unknownLevelIds.Count}] levels");
        CallRpc_Server_UnknownLevelIds(unknownLevelIds);
    }

    [ServerRpc]
    private static void Server_UnknownLevelIds(ClientNetworkConnection conn, List<string> levelIds)
    {
        if (!Inst)
        {
            return;
        }
                                                                                     
        PAM.Logger.LogInfo($"Client [{conn.Address}] has asked for [{levelIds.Count}] levels");

        foreach (var levelId in levelIds)
        {

            SongData.SongInfo? info = Inst._songData.GetData(levelId.ToString());
            if (info == null)
            {
                PAM.Logger.LogFatal("client asked for level id not in the picked challenge levels");
                continue;
            }
    
            const int separator = 131000 * 2; //what is this magical number bruh
            int offset = 0;
            while (true)
            {
                if (offset + separator < info.Value.Data.Length)
                {
                    ReadOnlySpan<short> segment = new(info.Value.Data, offset, separator);
                    CallRpc_Client_AudioData(conn, levelId, info.Value.Frequency, segment, false);
                    offset += separator + 1;
                }
                else
                {
                    ReadOnlySpan<short> segment = new(info.Value.Data, offset,
                        Mathf.FloorToInt(info.Value.Data.Length - offset));
                    CallRpc_Client_AudioData(conn, levelId, info.Value.Frequency, segment, true);
                    break;
                }
            }
        }
    }

    private static readonly List<float> AudioDataBuffer = new();
    private static string _lastId;
    
    [ClientRpc]
    private static void Client_AudioData(string audioID, int frequency, ReadOnlySpan<short> songData, bool last)
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

        if (Inst)
        {
            var newClip = AudioClip.Create(audioID.ToString(), AudioDataBuffer.Count, 1, frequency, false);
            newClip.SetData(AudioDataBuffer.ToArray(),
                0); //this to array is specially bad cuz its making 2 copies, may fix later
            newClip.LoadAudioData();

            AudioDataBuffer.Clear();
            
            Inst.SetLevelSong(audioID, newClip);
        }
    }
    
    void SpawnPlayers_Multiplayer()
    {
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if(vgPlayerData.PlayerObject.IsValidPlayer())
                vgPlayerData.PlayerObject.PlayerDeath(0);
        }
        VGPlayerManager.Inst.players.Clear();

        if (GlobalsManager.IsHosting)
        {
            bool hasZero = false;
            foreach (var keyValuePair in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(keyValuePair.Value.VGPlayerData);
                if (keyValuePair.Value.VGPlayerData.PlayerID == 0)
                {
                    hasZero = true;
                }
            }
            
            if (!hasZero)
            {
                //player 0 is never added, so we add it here
                var newData = new VGPlayerManager.VGPlayerData() { PlayerID = 0, ControllerID = 0 };
                VGPlayerManager.Inst.players.Add(newData);
                GlobalsManager.Players.TryAdd(GlobalsManager.LocalPlayerId, new PlayerData(newData, SteamClient.Name));
            }
          
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_,_,_) => {}, _ => {},(_, _, _) => {}, 3);
        }
        else
        {
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
            }
            
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_,_,_) => {}, _ => {},(_, _, _) => {}, 3);
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
            }));
          
            SceneLoader.Inst.manager.AddToLoadingTasks("Setting up chosen levels", Task.Run(async () =>
            {
                while (!_songData.Ready())
                {
                    await Task.Delay(100);
                }
            }), "<color=#FFD000>[ Prepping ]</color> 0/6"); 
                
            SceneLoader.Inst.manager.AddToLoadingTasks("Waiting other players", Task.Run(async () =>
            {
                while (!SteamLobbyManager.Inst.InLobby || !SteamLobbyManager.Inst.IsEveryoneLoaded)
                {
                    await Task.Delay(100);
                }
            }), dynamicStatusProvider: () => $"<color=#FFD000>[ Waiting ]</color> {SteamLobbyManager.Inst.LoadedPlayerCount()}/{(GlobalsManager.Players.Count == 0 ? 1 : GlobalsManager.Players.Count)}");
        }
        else
        {
            SceneLoader.Inst.manager.AddToLoadingTasks("Setting up chosen levels", Task.Run(async () =>
            {
                while (!_votingStarted)
                {
                    await Task.Delay(100);
                }
            }), "<color=#FFD000>[ Prepping ]</color> 0/6");
            
            SceneLoader.Inst.manager.AddToLoadingTasks("Waiting other players", Task.Run(async () =>
            {
                while (!_votingStarted)
                {
                    await Task.Delay(100);
                }
            }), dynamicStatusProvider: () => $"<color=#FFD000>[ Waiting ]</color> {SteamLobbyManager.Inst.LoadedPlayerCount()}/{GlobalsManager.Players.Count}");
        }
    }
    
    private bool CheckAllLevelsReady(bool setLoaded = true)
    {
        int counter = _loadedLevels.Values.Count(x => x.Loaded);
        
        if (counter < 6)
        {
            SceneLoader.Inst?.manager?.UpdateTaskStatus("Setting up chosen levels", $"<color=#FFD000>[ Prepping ]</color> {counter}/6");
            return false;
        }
        
        SceneLoader.Inst?.manager?.ResetTaskStatus("Setting up chosen levels");
        
        if (setLoaded)
        {
           SteamLobbyManager.Inst.CurrentLobby.SetMemberData("IsLoaded", "1");
        }
        return true;
    }
}

public partial class VoterCell : MonoBehaviour
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
        _ghostUIElement?.Hide();
        gameObject.GetComponent<BoxCollider2D>()?.enabled = false;
    }
    public void SetLevelData(VGLevel level)
    {
        Level = level;
       
        if (level.AlbumArt)
        {
            transform.GetChild(1).GetChild(0).GetComponent<Image>().sprite = level.AlbumArt;
        }
            
        UIStateManager.Inst.RefreshTextCache(transform.GetChild(2).GetComponent<TextMeshProUGUI>(), level.TrackName ?? "MISSING NAME");
        UIStateManager.Inst.RefreshTextCache(transform.GetChild(3).GetComponent<TextMeshProUGUI>(), level.CharterName ?? "MISSING NAME");
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

        if (_playerCounter == 0)
        {
            SetSelected(true);
            if (AudioManager.Inst.CurrentAudioClip != Level.LevelMusic)
            {
                AudioManager.Inst.ForcePlayMusic(Level.LevelMusic);

                if (Level.PreviewStart <= 0)
                {
                    AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = Level.LevelMusic.length / 2;
                }
                else
                {
                    AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = Level.PreviewStart;
                }
            }
        }
        
        _playerCounter++;

        if (!GlobalsManager.IsMultiplayer || GlobalsManager.IsHosting)
        {
            ChallengeManager.Inst.PlayerVote(player, Level);
            return;
        }
        
        CallRpc_Server_LevelVoted(Level.BaseLevelData.LevelID);
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
    private static void Server_LevelVoted(ClientNetworkConnection conn, string level)
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