using System;
using System.Collections.Generic;
using System.Reflection;
using AttributeNetworkWrapperV2;
using Crosstales;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using Steamworks;
using TMPro;
using UnityEngine;
using VGFunctions;

namespace PAMultiplayer.Managers;

public partial class PointsManager : MonoBehaviour
{
    private class PointChallenge(string name, int points)
    {
        public string Name = name;
        public int Points = points;
    }

    private struct PlayerRank(int hits, int boosts, int cc, int score, bool disqualify)
    {
        public readonly int Hits = hits;
        public readonly int Boosts = boosts;
        public readonly int Cc = cc;
        public readonly int Score = score;
        public readonly bool Disqualify = disqualify;
    }

    private struct PlayerEntry
    {
        public PlayerEntry(Transform entry)
        {
            Transform wrapper = entry.GetChild(1);
            Icon = wrapper.GetChild(1).GetComponent<TextMeshProUGUI>();
            Name = wrapper.GetChild(2).GetComponent<TextMeshProUGUI>();

            Transform content = entry.GetChild(2);
            Rank = content.GetChild(0).GetComponent<TextMeshProUGUI>();

            Stats = content.GetChild(1).GetComponent<TextMeshProUGUI>();

            (content.GetChild(2).GetChild(0).transform as RectTransform)!.sizeDelta = new Vector2(50, 50);
            Score = content.GetChild(2).GetChild(1).GetComponent<TextMeshProUGUI>();
        }

        public readonly TextMeshProUGUI Icon;
        public readonly TextMeshProUGUI Name;
        public readonly TextMeshProUGUI Rank;
        public readonly TextMeshProUGUI Stats;
        public readonly TextMeshProUGUI Score;

    }

    public static PointsManager Inst;
    private float _levelDuration = 0;

    private int _localBoosts = 0;
    private int _localCc = 0;
    private int _localHits = 0;
    private int _localDeaths = 0;
    private float _timeOfDeath = 0;

    private float _averageBoostDuration = 0;
    private float _timeAlive = 0;
    private float _timeMoving = 0;

    private Vector3 _sumOfPos = Vector3.zero;
    private int _posSaved = 0;

    private int _checkpointLow = 0;

    private readonly List<ulong> _playersDead = new();

    private readonly Dictionary<ulong, PlayerRank> _playerRanks = new();
    private readonly Dictionary<ulong, PlayerEntry> _playerEntries = new();

    private AssetBundle _pointsBundle;
    private UI_Menu _menu;
    private Transform _playerList;
    private GameObject _playerEntryPrefab;

    private static readonly string[] PlayerShapes = ["■", "▲", "<size=60>●", "<size=50><voffset=-15>^"];

    private void Awake()
    {
        if (Inst)
        {
            Destroy(this);
        }

        Inst = this;
        GameManager.Inst.levelRestartEvent += () => Inst?.LevelRestarted();
        GameManager.Inst.levelFinishEvent += () => Inst?.LevelEnded();

        using (var stream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("PAMultiplayer.Assets.points"))
        {
            _pointsBundle = AssetBundle.LoadFromMemory(stream!.CTReadFully());
        }

        Transform uiManager = PauseUIManager.Inst.transform.parent;
        _menu = Instantiate(_pointsBundle.LoadAsset("assets/level result.prefab") as GameObject, uiManager)
            .GetComponent<UI_Menu>();
        
        _playerList = _menu.transform.Find("PlayersList");
        
        _playerEntryPrefab = _pointsBundle.LoadAsset("assets/playerentry.prefab") as GameObject;
    }

    private void OnDestroy()
    {
        Inst = null;
        _pointsBundle?.Unload(true);
        if (_menu)
        {
            Destroy(_menu.gameObject);
        }
    }

    private void Update()
    {
        if (!GameManager.Inst || GameManager.Inst.CurGameState != GameManager.GameState.Playing)
        {
            return;
        }

        _levelDuration += Time.deltaTime;
    }

    private void LevelRestarted()
    {
        _levelDuration = 0;
        _localBoosts = 0;
        _localCc = 0;
        _localHits = 0;
        _localDeaths = 0;
        _timeOfDeath = 0;
        _averageBoostDuration = 0;
        _timeAlive = 0;
        _timeMoving = 0;

        _sumOfPos = Vector3.zero;
        _posSaved = 0;

        _checkpointLow = 0;
        
        _playerEntries.Clear();
        _playerRanks.Clear();
        _playersDead.Clear();
    }

    private void LevelEnded()
    {
        int score = Settings.Score.Value;
        if (!GlobalsManager.JoinedMidLevel)
        {
            score += GetWonChallenges();
        }

        Settings.Score.Value = score;
        if (GlobalsManager.IsMultiplayer)
        {
            CallRpc_Client_SendResults(_localHits, _localBoosts, _localCc, score, GlobalsManager.JoinedMidLevel);
            ShowEndScreen();
        }
    }


    public void ShowEndScreen()
    {
        _menu.ShowBase();
        _menu.SwapView("main");
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);
        
        foreach (var keyValuePair in GlobalsManager.Players)
        {
            GenerateEntry(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private void GenerateEntry(ulong playerId, PlayerData playerData)
    {
        var entry = new PlayerEntry(Instantiate(_playerEntryPrefab, _playerList).transform);
        entry.Name.text = playerData.Name;

        var hex = LSColors.ColorToHex(GameManager.Inst.LiveTheme.GetPlayerColor(playerData.VGPlayerData.PlayerID % 4));
        entry.Icon.text = $"<color=#{hex}>{PlayerShapes[playerData.VGPlayerData.PlayerID / 4]}";
        
        entry.Stats.rectTransform.anchoredPosition = new Vector2(100, 7);
        
        _playerEntries.Add(playerId, entry);
        
        if (_playerRanks.TryGetValue(playerId, out var playerRank))
        {
           UpdateEntry(playerId, playerRank);
        }
        else
        {
            entry.Rank.text = "<color=#" + LSColors.ColorToHex(DataManager.inst.LevelRanks[DataManager.LevelRankType.N].Color) + ">"
                              + DataManager.inst.LevelRanks[DataManager.LevelRankType.N].ASCII.GetLocalizedString();
            
            entry.Score.text = "X";
        }
    }

    private void UpdateEntry(ulong playerId, PlayerRank playerRank)
    {
        if (!_playerEntries.TryGetValue(playerId, out var entry))
        {
            return;
        }
        
        DataManager.LevelRankType rank = playerRank.Disqualify ? DataManager.LevelRankType.N : DataManager.LevelRank.GetLevelRankTypeFromHits(playerRank.Hits);
        entry.Rank.text = "<color=#" + LSColors.ColorToHex(DataManager.inst.LevelRanks[rank].Color) + ">"
                          + DataManager.inst.LevelRanks[rank].ASCII.GetLocalizedString();
            UIStateManager.Inst.RefreshTextCache(entry.Rank, entry.Rank.text);
            
        entry.Stats.text =
            $"<b><color=#fe0932>{playerRank.Hits} </color>/ <color=#00aeef>{playerRank.Boosts} </color>/ <color=#00efbe>{playerRank.Cc}</color>";
        UIStateManager.Inst.RefreshTextCache(entry.Stats, entry.Stats.text);
        
        entry.Score.text = $"<b><size=36>X<size=31>{playerRank.Score}";
        UIStateManager.Inst.RefreshTextCache(entry.Score, entry.Score.text);
    }

    [ServerRpc]
    private static void Client_SendResults(ClientNetworkConnection conn, int hit, int boost, int cc, int score, bool midLevel)
    {
        if (!Inst)
        {
            return;
        }
        
        if (!conn.TryGetSteamId(out SteamId id))
        {
            PAM.Logger.LogError($"couldnt find id {conn.Address}");
            return;
        }
        
        CallRpc_Multi_SetPlayerResults(id, hit, boost, cc, score, midLevel);
    }

    [MultiRpc]
    private static void Multi_SetPlayerResults(ulong steamId, int hit, int boost, int cc, int score, bool midLevel)
    {
        if (!Inst)
        {
            return;
        }
        
        PAM.Logger.LogError($"data from {steamId}");
        Inst._playerRanks[steamId] = new PlayerRank(hit, boost, cc, score, midLevel);
        Inst.UpdateEntry(steamId, Inst._playerRanks[steamId]);
    }
    
    public void AddBoost(float duration)
    {
        _localBoosts++;
        _averageBoostDuration += duration;
    }

    public void AddCloseCall()
    {
        _localCc++;
    }

    public void AddHit()
    {
        _localHits++;
    }
    public void AddTimeAlive(float time)
    {
        _timeAlive += time;
    }

    public void AddTimeMoving(float time)
    {
        _timeMoving += time;
    }

    public void AddDeath()
    {
        _localDeaths++;
        _localHits++;
        _timeOfDeath = Time.timeSinceLevelLoad;
    }

    public void AddPosition(Vector3 position)
    {
        _sumOfPos += position / 10;
        _posSaved++;
    }

    public void AddCheckpointWithOneHealth()
    {
        _checkpointLow++;
    }

    public void PlayerHasDied(ulong playerId)
    {
        if (!_playersDead.Contains(playerId))
        {
            _playersDead.Add(playerId);
        }
    }

    public void PlayerLeft(ulong playerId)
    {
        _playersDead.Remove(playerId);
    }
    
    public int GetWonChallenges()
    {
        List<PointChallenge> challenges = new();
        
        DataManager.LevelRankType levelRank = !SingletonBase<GameManager>.Inst.IsPractice ? DataManager.LevelRank.GetLevelRankTypeFromHits(_localHits) : DataManager.LevelRankType.N;
        switch (levelRank)
        {
            case DataManager.LevelRankType.N:
                challenges.Add(new PointChallenge("Participation trophy", 1));
                break;
            case DataManager.LevelRankType.D:
                challenges.Add(new PointChallenge("Maybe next time", 2));
                break;
            case DataManager.LevelRankType.C:
                challenges.Add(new PointChallenge("Could be better", 4));
                break;
            case DataManager.LevelRankType.B:
                challenges.Add(new PointChallenge("Not bad", 6));
                break;
            case DataManager.LevelRankType.A:
                challenges.Add(new PointChallenge("Very good", 8));
                break;
            case DataManager.LevelRankType.S:
                challenges.Add(new PointChallenge("Well done", 10));
                break;
            case DataManager.LevelRankType.P:
                challenges.Add(new PointChallenge("Perfection", 12));
                break;
        }

        if (GlobalsManager.IsMultiplayer)
        {
            if (GlobalsManager.Players.Count > 1 &&
                _playersDead.Count - 1 == GlobalsManager.Players.Count &&
                !_playersDead.Contains(GlobalsManager.LocalPlayerId.Value))
            {
                challenges.Add(new PointChallenge("Carry", 10));
            }
        }
        
        //boost challenges
        {
            _averageBoostDuration /= _localBoosts;
            float boostsPerSecond = _localBoosts / _levelDuration;

            PAM.Logger.LogFatal($"average Boost [{boostsPerSecond}]");
            if (_localBoosts == 0)
            {
                challenges.Add(new PointChallenge("Didnt even try", 12));
            }
            else
            {
                if (boostsPerSecond > 5)
                {
                    challenges.Add(new PointChallenge("Impatient", 3));
                }
                else if (boostsPerSecond < 0.15f)
                {
                    challenges.Add(new PointChallenge("Patience is a virtue", 6));
                }

                if (_averageBoostDuration > 0.22f)
                {
                    challenges.Add(new PointChallenge("Looong", 3));
                }
                else if (_averageBoostDuration < 0.09f)
                {
                    challenges.Add(new PointChallenge("You know you can hold it right?", 5));
                }
            }
        }
        
        //CC
        {
            float ccPerSeconds = _localCc / _levelDuration;
             if (ccPerSeconds > 0.5f)
            {
                challenges.Add(new PointChallenge("A little too close", 6));
            }
            else if (ccPerSeconds < 0.05f)
            {
                challenges.Add(new PointChallenge("Not even close", 9));
            }
        }
        
        //movement
        {
            float ratioMoving = _timeMoving / _timeAlive;

            if (ratioMoving > 0.93f)
            {
                challenges.Add(new PointChallenge("Good cardio", 2));
            }
            else if (ratioMoving < 0.1f)
            {
                challenges.Add(new PointChallenge("Why even bother", 4));
            }
        }
        
        //alive time
        {
            float ratioAlive = _timeAlive /  _levelDuration;

            if (_localDeaths == 0)
            {
                challenges.Add(new PointChallenge("Not even a death", 11));
            }
            else if (ratioAlive < 0.4f)
            {
                challenges.Add(new PointChallenge("Gamemode 3", 1));
            }

            if (_localDeaths > 0 && (_levelDuration - _timeOfDeath) < 5)
            {
                challenges.Add(new PointChallenge("Right at the end", 4));
            }
        }

        //position
        {
            Vector3 averagePos = _sumOfPos / _posSaved;
            averagePos.z = 0;

            float mag = averagePos.magnitude;
            PAM.Logger.LogFatal($"MAG [{mag}]");
            if (mag < 1)
            {
                challenges.Add(new PointChallenge("Center of attentions", 3));
            }
            else if (mag > 3.7f)
            {
                challenges.Add(new PointChallenge("Shy nano", 2));
            }
        }
        
        
        //checkpoint
        {
            int checkCount = DataManager.inst.gameData.beatmapData.checkpoints.Count;
            if (checkCount > 1 && checkCount - 1 == _checkpointLow)
            {
                challenges.Add(new PointChallenge("On edge", 5));
            }
        }


        VGLevel level = GameManager.Inst.FetchLevel();
        float difficultyMult;
        
        switch (level.Difficulty)
        {
            case DataManager.DifficultyType.Basic:
                difficultyMult = 0.6f;
                break;
            case DataManager.DifficultyType.Moderate:
                difficultyMult = 0.8f;
                break;
            case DataManager.DifficultyType.Advanced:
                difficultyMult = 1;
                break;
            case DataManager.DifficultyType.Expert:
                difficultyMult = 1.2f;
                break;
            case DataManager.DifficultyType.Master:
                difficultyMult = 1.4f;
                break;
            default:
                difficultyMult = 0;
                break;
        }

        float durationMult = level.LevelMusic.length / 90; //1:30 level for 1x

        int wonAmount = 0;
        int raw = 0;
        foreach (var challenge in challenges)
        {
            raw += challenge.Points;
            challenge.Points = (int)(challenge.Points * difficultyMult * durationMult);
            wonAmount += challenge.Points;
            PAM.Logger.LogError(challenge.Name);
        }
        PAM.Logger.LogFatal($"Raw [{raw}], time and difficulty scaled [{wonAmount}]");
        return wonAmount;
    }
    //screen side
    //rewind
}