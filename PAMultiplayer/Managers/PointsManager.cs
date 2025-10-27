using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AttributeNetworkWrapperV2;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Cpp2IL.Core.Extensions;
using Il2CppInterop.Runtime;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VGFunctions;
using Random = UnityEngine.Random;
using Type = Il2CppSystem.Type;

namespace PAMultiplayer.Managers;

public partial class PointsManager : MonoBehaviour
{
    private class PointChallenge(string name, int points)
    {
        public string Name = name;
        public int Points = points;
    }

    private struct PlayerRank(int hits, int boosts, int cc, int score)
    {
        public int Hits = hits;
        public int Boosts = boosts;
        public int Cc = cc;
        public int Score = score;
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
    
    private Dictionary<ulong, PlayerRank> _playerRanks = new();
    
    private AssetBundle _pointsBundle;
    private UI_Menu _menu;
    private Transform _playerList;
    private GameObject _playerEntryPrefab;
    
    private Texture2D[] _pointsSprites = new Texture2D[5];

    private static readonly int[] COIN_VALUES = [10000, 1000, 100, 10, 1];

    private void Awake()
    {
        if (Inst)
        {
            Destroy(this);
        }
        
        Inst = this;
        GameManager.Inst.add_levelRestartEvent(new Action(() => Inst.LevelRestarted()));
        
        using (var stream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("PAMultiplayer.Assets.points"))
        {
            _pointsBundle = AssetBundle.LoadFromMemory(stream!.ReadBytes());
            foreach (var allAssetName in _pointsBundle.AllAssetNames())
            {
                PAM.Logger.LogFatal(allAssetName);
            }
        }

        _playerEntryPrefab = _pointsBundle.LoadAsset("assets/playerentry.prefab").Cast<GameObject>();
        
        _pointsSprites[4] = _pointsBundle.LoadAsset("assets/challenge/pamcoin-01.png").Cast<Texture2D>();
        _pointsSprites[3] = _pointsBundle.LoadAsset("assets/challenge/pamcoin-02.png").Cast<Texture2D>();
        _pointsSprites[2] = _pointsBundle.LoadAsset("assets/challenge/pamcoin-03.png").Cast<Texture2D>();
        _pointsSprites[1] = _pointsBundle.LoadAsset("assets/challenge/pamcoin-05.png").Cast<Texture2D>();
        _pointsSprites[0] = _pointsBundle.LoadAsset("assets/challenge/pamcoin-04.png").Cast<Texture2D>();
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

    public void LevelRestarted()
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
    }

    private void Start()
    {
        GameManager.Inst.add_levelStartEvent(new Action(() => Inst.StartCoroutine(Inst.ShowLobby().WrapToIl2Cpp())));
        //StartCoroutine(ShowLobby().WrapToIl2Cpp());
    }

    IEnumerator ShowLobby()
    {
        Transform uiManager = PauseUIManager.Inst.transform.parent;
        _menu = Instantiate(_pointsBundle.LoadAsset("assets/level result.prefab").Cast<GameObject>(), uiManager).GetComponent<UI_Menu>();

        _playerList = _menu.transform.Find("PlayersList");
        
        yield return new WaitForUpdate();
        yield return new WaitForUpdate();
        
        _menu.ShowBase();
        _menu.SwapView("main");
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);

        for (int i = 0; i < 16; i++)
        {
           GenerateEntry();
        }
    }

    public void ShowEndScreen()
    {
        Transform uiManager = PauseUIManager.Inst.transform.parent;
        var screen = Instantiate(_pointsBundle.LoadAsset("assets/level result.prefab").Cast<GameObject>(), uiManager).GetComponent<UI_Menu>();
        
        screen.ShowBase();
        screen.SwapView("main");
        CameraDB.Inst.SetUIVolumeWeightIn(0.2f);
    }

    private void GenerateEntry()
    {
         var entry = new PlayerEntry(Instantiate(_playerEntryPrefab, _playerList).transform);
            entry.Name.text = $"player test";
            
            var rank = (DataManager.LevelRankType)Random.RandomRangeInt(0, 7);
            
            entry.Rank.text = "<color=#" + LSColors.ColorToHex(DataManager.inst.LevelRanks[rank].Color) + ">"
            + DataManager.inst.LevelRanks[rank].ASCII.GetLocalizedString();

            entry.Stats.rectTransform.anchoredPosition = new Vector2(100, 0);
            entry.Stats.text = $"<b><color=#fe0932>{Random.RandomRangeInt(0, 100)} </color>/ <color=#00aeef>{Random.RandomRangeInt(0, 100)} </color>/ <color=#00efbe>{Random.RandomRangeInt(0, 100)}</color>";
            entry.Stats.transform.Cast<RectTransform>().anchoredPosition = new Vector2(100, 7);
            int randScore = Random.RandomRangeInt(0, 100000);
            
            Dictionary<int, int> scores = new();
            
            for (var j = 0; j < COIN_VALUES.Length; j++)
            {
                int value = COIN_VALUES[j];

                int amount = randScore / value;
                int curr = scores.GetValueOrDefault(j);
                scores[j] = curr + amount;
                randScore -= value * amount;
            }

            int count = 0;
            foreach (var keyValuePair in scores)
            {
                if (keyValuePair.Value == 0)
                {
                    continue;
                }
                
                if (++count >= 50)
                {
                    break;
                }
                
                var go = new GameObject($"icon {keyValuePair.Key}");
                go.transform.SetParent(entry.Score, false);
               
                go.AddComponent<RawImage>().texture = _pointsSprites[keyValuePair.Key];
               
                
                for (int j = 0; j < keyValuePair.Value - 1; j++)
                {
                    if (++count > 50)
                    {
                        break;
                    }
                    Instantiate(go, entry.Score);
                }
                
                if (++count > 50)
                {
                    break;
                }
                
                new GameObject("spacer", Il2CppType.Of<RectTransform>()).transform.SetParent(entry.Score);
            }
    }

    [MultiRpc]
    private static void Multi_RequestClientResults()
    {
        if (!Inst)
        {
            return;
        }
        
        Inst._playerRanks.Clear();
        CallRpc_Client_SendResults(null, Inst._localHits, Inst._localBoosts, Inst._localCc, 0);
    }

    [ServerRpc]
    private static void Client_SendResults(ClientNetworkConnection conn, int hit, int boost, int cc, int score)
    {
        if (!Inst)
        {
            return;
        }
        
        if (!conn.TryGetSteamId(out SteamId id))
        {
            return;
        }
        
        CallRpc_Multi_SetPlayerResults(id, hit, boost, cc, score);
    }

    [MultiRpc]
    private static void Multi_SetPlayerResults(ulong steamId, int hit, int boost, int cc, int score)
    {
        Inst._playerRanks[steamId] = new PlayerRank(hit, boost, cc, score);
    }
    
    public void AddBoost()
    {
        _localBoosts++;
    }

    public void AddCloseCall()
    {
        _localCc++;
    }

    public void AddHit()
    {
        _localHits++;
    }

    public void AddBoostDuration(float duration)
    {
        _averageBoostDuration += duration;
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
    
    public void GetWonChallenges()
    {
        List<PointChallenge> challenges = new();
        
        DataManager.LevelRankType levelRank = !SingletonBase<GameManager>.Inst.IsPractice ? DataManager.LevelRank.GetLevelRankTypeFromHits(_localHits) : DataManager.LevelRankType.N;
        switch (levelRank)
        {
            case DataManager.LevelRankType.N:
                challenges.Add(new PointChallenge("Participation trophy", 1));
                break;
            case DataManager.LevelRankType.D:
                challenges.Add(new PointChallenge("Maybe next time", 5));
                break;
            case DataManager.LevelRankType.C:
                challenges.Add(new PointChallenge("Could be better", 10));
                break;
            case DataManager.LevelRankType.B:
                challenges.Add(new PointChallenge("Not bad", 15));
                break;
            case DataManager.LevelRankType.A:
                challenges.Add(new PointChallenge("Very good", 20));
                break;
            case DataManager.LevelRankType.S:
                challenges.Add(new PointChallenge("Well done", 25));
                break;
            case DataManager.LevelRankType.P:
                challenges.Add(new PointChallenge("Perfection", 30));
                break;
        }
        
        //boost challengess
        {
            _averageBoostDuration /= _localBoosts;
            float boostsPerSecond = _localBoosts / _levelDuration;

            PAM.Logger.LogFatal($"average Boost [{boostsPerSecond}]");
            
            if (_localBoosts == 0)
            {
                challenges.Add(new PointChallenge("Patience is a virtue", 30));
            }
            else
            {
                if (boostsPerSecond > 5)
                {
                    challenges.Add(new PointChallenge("Impatient", 10));
                }
                else if (boostsPerSecond < 0.3f)
                {
                    challenges.Add(new PointChallenge("Patience is a virtue", 15));
                }

                if (_averageBoostDuration > 0.22f)
                {
                    challenges.Add(new PointChallenge("Looong", 10));
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
             if (ccPerSeconds > 1)
            {
                challenges.Add(new PointChallenge("A little too close", 10));
            }
            else if (ccPerSeconds < 0.3f)
            {
                challenges.Add(new PointChallenge("Not even close", 10));
            }
        }
        
        //movement
        {
            float ratioMoving = _timeMoving / _timeAlive;

            if (ratioMoving > 0.93f)
            {
                challenges.Add(new PointChallenge("Good cardio", 10));
            }
            else if (ratioMoving < 0.1f)
            {
                challenges.Add(new PointChallenge("Why even bother", 10));
            }
        }
        
        //alive time
        {
            float ratioAlive = _timeAlive /  _levelDuration;

            if (_localDeaths == 0)
            {
                challenges.Add(new PointChallenge("Not even a death", 15));
            }
            else if (ratioAlive < 0.4f)
            {
                challenges.Add(new PointChallenge("Gamemode 3", 5));
            }

            if (_localDeaths > 0 && (_levelDuration - _timeOfDeath) < 5)
            {
                challenges.Add(new PointChallenge("Right at the end", 5));
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
                challenges.Add(new PointChallenge("Center of attentions", 15));
            }
            else if (mag > 3)
            {
                challenges.Add(new PointChallenge("Shy nano", 5));
            }
        }
        
        
        //checkpoint
        {
            if (DataManager.inst.gameData.beatmapData.checkpoints.Count - 1 == _checkpointLow)
            {
                challenges.Add(new PointChallenge("On edge", 15));
            }
        }
        
        
        float difficultyMult = 1;
        
        switch (GameManager.Inst.FetchLevel().Difficulty)
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
        
        foreach (var challenge in challenges)
        {
            challenge.Points = (int)(challenge.Points * difficultyMult);
        }
    }
    //screen side
    //rewind
}