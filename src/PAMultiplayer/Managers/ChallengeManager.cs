using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AttributeNetworkWrapperV2;
using CielaSpike;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Patch;
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
    public static ChallengeManager Inst { get; private set; }
    public static readonly List<string> RecentLevels = new();
    
    public Transform playersParent;
    public int ColorID { get; private set; }
    public DataManager.BeatmapTheme ChallengeTheme { get; private set; }
    
    private readonly List<VoterCell> _levelButtons = new(6);
    private readonly List<VGLevel> _levelsToVote = new(6);
    private readonly Dictionary<VGLevel, int> _loadedLevels = new(6);
    private readonly Dictionary<VGPlayer, VGLevel> _votes = new(16);
    private readonly ConcurrentDictionary<ulong, Tuple<short[], int, int>> _songData = new(); //struct here crashes bepinex lmao
    
    private bool _votingStarted = false;
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

        for (int i = 0; i < cells.childCount; i++)
        {
            _levelButtons.Add(cells.GetChild(i).gameObject.AddComponent<VoterCell>());
        }
        VGPlayerManager.Inst.LockPlayerAdding(false);
        
        if (GlobalsManager.IsMultiplayer)
        {
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
        
        VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_) => {}, _ => {},_ => {}, 3);
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            vgPlayerData.PlayerObject.SetColor(ChallengeTheme.GetPlayerColor(vgPlayerData.PlayerID), ChallengeTheme.guiAccent);
        }
        
        StartCoroutine(ShowLevels());
        
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
            SceneLoader.Inst.LoadSceneGroup("Menu");
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

        if (RecentLevels.Count + 6 >= ArcadeLevelDataManager.Inst.ArcadeLevels.Count)
        {
            PAM.Logger.LogInfo("Clearing out recent level list due to being full");
            RecentLevels.Clear();
        }

        string blacklistStr = Settings.ChallengeBlacklist.Value;

        List<VGLevel> nonRepeatLevels = new(ArcadeLevelDataManager.Inst.ArcadeLevels.Count - RecentLevels.Count);

        HashSet<string> comparer = new(RecentLevels);
        comparer.UnionWith(blacklistStr.Split('/', StringSplitOptions.RemoveEmptyEntries));

        foreach (var arcadeLevel in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (!comparer.Contains(arcadeLevel.name))
            {
                nonRepeatLevels.Add(arcadeLevel);
            }
        }

        if (nonRepeatLevels.Count < 6)
        {
            PAM.Logger.LogError(
                $"Not enough non blacklisted levels found, minimum [6], loaded [{ArcadeLevelDataManager.Inst.ArcadeLevels.Count}]");
            SceneLoader.Inst.manager.ClearLoadingTasks();
            SceneLoader.Inst.LoadSceneGroup("Menu");
            yield break;
        }

        bool allowNonPublicLevels = Settings.AllowNonPublicLevels.Value;
        List<Task<Sprite>> loadTasks = new();

        for (int i = 0; i < 24; i++)
        {
            var level = nonRepeatLevels[Random.Range(0, nonRepeatLevels.Count)];

            if (_levelsToVote.Contains(level))
            {
                nonRepeatLevels.Remove(level);
                continue;
            }

            if (GlobalsManager.IsMultiplayer)
            {
                if (level.SteamInfo == null)
                {
                    continue;
                }

                var task = SteamUGC.QueryFileAsync(level.SteamInfo.ItemID);
                while (!task.IsCompleted)
                {
                    yield return new WaitForUpdate();
                }

                var result = task.Result;
                if (!result.HasValue || result.Value.Result != Result.OK)
                {
                    continue;
                }

                //not public, friends only or private means unlisted which is allowed.
                if (!result.Value.IsPublic && !allowNonPublicLevels && !result.Value.IsFriendsOnly &&
                    !result.Value.IsPrivate)
                {
                    continue;
                }
            }

            if (!level.LevelMusic) //this can mean the user is using the mod LessRam
            {
                level = ArcadeLevelDataManager.Inst
                    .GetLocalCustomLevel(level.name); //this triggers song load if thats the case

                for (int j = 0; j < 316; j++)
                {
                    if (!level.LevelMusic)
                    {
                        yield return new WaitForUpdate();
                    }
                }

                if (!level.LevelMusic) //too long has passed, no song yet. this is bad;
                {
                    continue;
                }
            }

            if (!level.AlbumArt)
            {
                loadTasks.Add(AlbumArtManager.LoadAlbumArtAsync(level.name, level.BaseLevelData.LocalFolder));
            }
            else
            {
                loadTasks.Add(null);
            }

            RecentLevels.Add(level.name);
            _levelsToVote.Add(level);
            nonRepeatLevels.Remove(level);

            if (_levelsToVote.Count < 6)
            {
                continue;
            }
            
            for (var levelIndex = 0; levelIndex < loadTasks.Count; levelIndex++)
            {
                var albumTask = loadTasks[levelIndex];
                if (albumTask == null)
                {
                    continue;
                }
                    
                if (!albumTask.IsCompleted)
                {
                    yield return new WaitUntil(() => albumTask.IsCompleted);
                }

                _levelsToVote[levelIndex].AlbumArt = albumTask.Result;
            }

            yield break;
        }

        PAM.Logger.LogError(
            "Not enough levels found in too many attempts");
        SceneLoader.Inst.manager.ClearLoadingTasks();
        SceneLoader.Inst.LoadSceneGroup("Menu");
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
            if (levelButton?.Level?.SteamInfo?.ItemID != level)
            {
                levelButton?.Hide();
            }
        }
    }
    
    void SetVoteWinner(VGLevel level)
    {
        if (GlobalsManager.IsMultiplayer)
        {
            CallRpc_Multi_VoteWinner(level.SteamInfo.ItemID);
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
    private static void Multi_VoteWinner(ulong level)
    {
        Inst?.SetVoteWinner(level);
    }
    
    #endregion

    #region Level entry download
    
    //clients setting up server sent levels
    public async void CreateLevelEntry(ulong id, int index)
    {
        try
        {
            VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id.ToString());
            if (level) 
            {
                if (!_levelsToVote.Contains(level))
                {
                    _levelsToVote.Add(level);
                    level.AlbumArt = await AlbumArtManager.LoadAlbumArtAsync(level.name, level.BaseLevelData.LocalFolder);
                    _loadedLevels[level] = 2;
                }
                
                CheckAllLevelsReady();
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
                PAM.Logger.LogError($"Failed to get workshop data for level id [{id}]");
                GlobalsManager.IsReloadingLobby = false;
             
                if (!GlobalsManager.IsHosting)
                {
                    SteamManager.Inst.EndClient();
                }
           
                SceneLoader.Inst.manager.ClearLoadingTasks();
                SceneLoader.Inst.LoadSceneGroup("Menu");
                return;
            }

            level.TrackName = result.Value.Title;
            level.ArtistName = "Artist";
            
            StartCoroutine(GetImageFromWorkshop(level, result.Value.PreviewImageUrl));
        }
        catch (Exception e)
        {
            PAM.Logger.LogError($"Error Creating Level Entry\n{e}");
        }
    }

    private bool GetVGLevel(ulong levelId, out Tuple<short[], int, int> songData)
    {
        return _songData.TryGetValue(levelId, out songData);
    }
    
    IEnumerator GetImageFromWorkshop(VGLevel level, string url)
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
        
        yield return www.SendWebRequest();
        
        if (www.result != UnityWebRequest.Result.Success)
        {
            PAM.Logger.LogError(www.error);
            level.AlbumArt = null;
            _loadedLevels[level]++;
            CheckAllLevelsReady();
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
            webr.Dispose();
            PAM.Logger.LogError(e);
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
        
        float[] songData = new float[Mathf.FloorToInt(4/*seconds*/ * clip.frequency * clip.channels)];
        short[] songDataShort = new short[Mathf.FloorToInt(4/*seconds*/ * frequency * clip.channels)];
        
        clip.GetData(songData, clip.samples / 2);

        //reduces frequency to 22-24k hz~
        int index = 0;
        for (var i = 0; i < songData.Length; i += (divider - 1) * clip.channels)
        {
            for (int j = 0; j < clip.channels; j++)
            {
                songDataShort[index] = (short)(songData[i] * short.MaxValue);
                index++;
                i++;
            }
            
        }
        
        //  clip.UnloadAudioData();
        //  Destroy(clip); leaked?
        
        _songData.AddOrUpdate(level.SteamInfo.ItemID, 
            new Tuple<short[], int, int>(songDataShort, frequency, clip.channels), 
            (_, tuple) => tuple);

        SceneLoader.Inst?.manager?.UpdateTaskStatus("Setting up chosen levels", $"<color=#FFD000>[ Prepping ]</color> {_songData.Count}/6");
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
        double timeSinceLastButton = Time.realtimeSinceStartupAsDouble;
        
        for (int i = 0; i < 6; i++)
        {
            var level = _levelsToVote[i];
            var button = _levelButtons[i];
            
            button.SetLevelData(level);  
            button.Show();
            
            if (level.LevelMusic)
            {
                AudioManager.Inst.PlayMusic(level.LevelMusic);
            }
            
            if (level.LevelMusic.length > 5)
            {
                AudioManager.Inst.musicSources[AudioManager.Inst.activeSource].time = level.LevelMusic.length / 2;
            }
           
            timeSinceLastButton += 2.5;
            yield return new WaitUntil(() => timeSinceLastButton <= Time.realtimeSinceStartupAsDouble);
           //Used instead of waitForSeconds to account for lag
        }

        foreach (var levelButton in _levelButtons)
        {
            levelButton.EnableVoting();
        }
        
        if (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting)
        {
            yield break;
        }
        
        yield return new WaitForSecondsRealtime(5f);
        
        VGLevel nextLevel = PickLevel();
        
        if (!nextLevel)
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
                yield return new WaitUntil(() => SteamLobbyManager.Inst.InLobby);
            }
            else
            {
                SteamManager.Inst.StartClient(SteamLobbyManager.Inst.CurrentLobby.Owner.Id);
                yield return new WaitUntil(() => AttributeNetworkWrapperV2.NetworkManager.Instance!.TransportActive);
                yield return new WaitUntil(() => GlobalsManager.HasLoadedAllInfo);
            }
        }

        if (MultiplayerDiscordManager.Instance)
        {
            MultiplayerDiscordManager.Instance.SetChallengePresence();
        }

        GlobalsManager.IsReloadingLobby = false;

        if (!GlobalsManager.IsHosting)
        {
            yield break;
        }

        SteamLobbyManager.Inst.CurrentLobby.SetData("LobbyState",
            ((ushort)SteamLobbyManager.LobbyState.Challenge).ToString());

        yield return PickLevelForVoting();

        // yield return new WaitUntil(new Func<bool>(() => _levelsToVote.Count >= 6));

        Transform skip = PauseUIManager.Inst.transform.Find("Pause Menu")?.Find("Skip Queue Level");
        if (skip)
        {
            skip.gameObject.SetActive(false);
        }

        var timer = Stopwatch.StartNew();
        
        List<ulong> ids = new();
        foreach (var vgLevel in _levelsToVote)
        {
            ids.Add(vgLevel.SteamInfo.ItemID);
            this.StartCoroutineAsync(GetSongData(vgLevel));
        }

        yield return new WaitUntil(() => _songData.Count >= 6);

        timer.Stop();
        PAM.Logger.LogDebug($"took {timer.ElapsedMilliseconds}ms to get level data");

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
    private static void Multi_CheckLevelIds(List<ulong> levelIds)
    {
        if (!GlobalsManager.IsHosting)
        {
            Inst?.StartCoroutine(CheckLevelIds(levelIds));
        }
    }

    static IEnumerator CheckLevelIds(List<ulong> levelIds)
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
    private static void Server_UnknownLevelIds(ClientNetworkConnection conn, List<ulong> levelIds)
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
                    CallRpc_Client_AudioData(conn, levelId, songData.Item2, songData.Item3, segment, false);
                    offset += separator + 1;
                }
                else
                {
                    ArraySegment<short> segment = new(songData.Item1, offset,
                        Mathf.FloorToInt(songData.Item1.Length - offset));
                    CallRpc_Client_AudioData(conn, levelId, songData.Item2, songData.Item3, segment, true);
                    break;
                }
            }
        }
    }

    private static readonly List<float> AudioDataBuffer = new(400000);
    private static ulong _lastId;
    
    [ClientRpc]
    private static void Client_AudioData(ulong audioID, int frequency, int channels, Span<short> songData, bool last)
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
          
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_) => {}, _ => {},_ => {}, 3);
        }
        else
        {
            foreach (var vgPlayerData in GlobalsManager.Players)
            {
                VGPlayerManager.Inst.players.Add(vgPlayerData.Value.VGPlayerData);
            }
            
            VGPlayerManager.Inst.SpawnPlayers(Vector2.zero, (_,_) => {}, _ => {},_ => {}, 3);
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
                while (_songData.Count < 6)
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
        int counter = _loadedLevels.Values.Count(x => x == 2);
        
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

        if (_playerCounter == 0)
        {
            SetSelected(true);
            if (AudioManager.Inst.CurrentAudioClip != Level.LevelMusic)
            {
                AudioManager.Inst.PlayMusic(Level.LevelMusic);

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
        
        CallRpc_Server_LevelVoted(Level.SteamInfo.ItemID);
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
    private static void Server_LevelVoted(ClientNetworkConnection conn, ulong level)
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