using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Data;

public class SongData
{
    public struct SongInfo(short[] data, int frequency)
    {
        public short[] Data = data;
        public int Frequency = frequency;
    }
    
    
    public Action<int> SongDataUpdated;

    private ConcurrentDictionary<string, SongInfo> _data = new(6, 6);
    
    public SongInfo? GetData(string steamId)
    {
        if (_data.TryGetValue(steamId, out var info))
        {
            return info;
        }
       
        return null;
    }
    
    public bool Ready() => _data.Count >= 6;

    public IEnumerator SetupSongData(MonoBehaviour mb, IEnumerable<VGLevel> levels)
    {
        List<Coroutine> levelCoroutines = new(6);

        foreach (var vgLevel in levels)
        {
            var wrapper = mb.StartCoroutine(ProcessLevel(vgLevel));
            levelCoroutines.Add(wrapper);
        }
        
        foreach (var curr in levelCoroutines)
        {
            yield return curr;
        }

    }

    private IEnumerator ProcessLevel(VGLevel level)
    {
        string id = level.BaseLevelData.LevelID;
        string localFolder = level.BaseLevelData.LocalFolder;

        if (level.IsBakedLevel)
        {
            _data[id] = new([], 0);
            SongDataUpdated?.Invoke(_data.Count);
            yield break;
        }
        
        string path = "";
        if (File.Exists(localFolder + "/audio.ogg"))
        {
            path = localFolder + "/audio.ogg";
        }
        else if (File.Exists(localFolder + "/level.ogg"))
        {
            path = localFolder + "/level.ogg";
        }

        if (string.IsNullOrEmpty(path))
        {
            PAM.Logger.LogFatal($"{level.name} with id {level.BaseLevelData.LevelID} at {localFolder} had no valid song path found");
            _data[id] = new([], 0);
            SongDataUpdated?.Invoke(_data.Count);
            yield break;
        }

        AudioClip clip;
        using (UnityWebRequest webr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS))
        {
            yield return webr.SendWebRequest(); //cant be inside Try block
            
            if (webr.result != UnityWebRequest.Result.Success)
            {
                PAM.Logger.LogFatal($"{level.name} with id {level.BaseLevelData.LevelID} at {localFolder} failed to get the song request\n" +
                                    $"result {webr.result.ToString()} with reason {webr.error}");
                _data[id] = new([], 0);
                SongDataUpdated?.Invoke(_data.Count);
                yield break;
            }
            
            try
            {
                clip = DownloadHandlerAudioClip.GetContent(webr);
            }
            catch (Exception e)
            {
                PAM.Logger.LogError(e);
                PAM.Logger.LogFatal(
                    $"{level.name} with id {level.BaseLevelData.LevelID} at {localFolder} had a problem on getting the audio clip");
                _data[id] = new([], 0);
                SongDataUpdated?.Invoke(_data.Count);
                yield break;
            }
        }
        
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
        
        float[] songData = new float[Mathf.FloorToInt(4 /*seconds*/ * clip.frequency * clip.channels)];
        short[] songDataShort = new short[Mathf.FloorToInt(4 /*seconds*/ * frequency)];
        clip.GetData(songData, clip.samples / 2);

        //reduces frequency to 22-24k hz~ and makes it mono
        int index = 0;
        for (var i = 0; i < songData.Length; i += divider * clip.channels)
        {
            songDataShort[index] = 0;
            float curr = 0;

            for (int j = 0; j < clip.channels; j++)
            {
                curr += songData[i + j];
            }

            songDataShort[index] = (short)(Mathf.Clamp(curr / clip.channels, -1f, 1f) * short.MaxValue);
            index++;
        }
        
        clip.UnloadAudioData();
        Object.Destroy(clip); 
        
        PAM.Logger.LogInfo($"Level is [{songDataShort.Length * 2}] bytes");
        _data[id] = new(songDataShort, frequency);
        SongDataUpdated?.Invoke(_data.Count);
    }
}