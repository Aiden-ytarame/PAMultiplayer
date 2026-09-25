using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PAMultiplayer.Data;

public class SongData
{
    public struct SongInfo(short[] data, int frequency)
    {
        public short[] Data = data;
        public int Frequency = frequency;
    }
    
    public Action<int> SongDataUpdated;

    private ConcurrentDictionary<ulong, SongInfo> _data = new(6, 6);

    public void AddLevel(MonoBehaviour mb, VGLevel level)
    {
        mb.StartCoroutine(InternalGetSongData(level));
    }

    public SongInfo? GetData(ulong steamId)
    {
        if (_data.TryGetValue(steamId, out var info))
        {
            return info;
        }
       
        return null;
    }
    
    public bool Ready() => _data.Count >= 6;
    
    private IEnumerator InternalGetSongData(VGLevel level) //we could do like UniTask but ehhhhhhhhhhhhhh
    {
        var task = Task.Run(() => GetSongDataAsync(level.SteamInfo.ItemID, level.BaseLevelData.LocalFolder));
        while (task.IsCompleted == false)
        {
            yield return new WaitForUpdate();
        }
        
        SongDataUpdated?.Invoke(_data.Count);
    }

    private async Task GetSongDataAsync(ulong id, string localFolder)
    {
         string path = "";
        if (File.Exists(localFolder + "/audio.ogg"))
        {
            path = localFolder + "/audio.ogg";
        }
        else if (File.Exists(localFolder + "/level.ogg"))
        {
            path = localFolder + "/level.ogg";
        }

        AudioClip clip;
        using (UnityWebRequest webr = UnityWebRequestMultimedia.GetAudioClip(path, AudioType.OGGVORBIS))
        {

            await webr.SendWebRequest(); //cant be inside Try block

            try
            {
                clip = DownloadHandlerAudioClip.GetContent(webr);
            }
            catch (Exception e)
            {
                PAM.Logger.LogError(e);
                return;
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
            for (int j = 0; j < clip.channels; j++)
            {
                songDataShort[index] += (short)(songData[i + j] * short.MaxValue);
            }

            songDataShort[index] /= (short)clip.channels;
            index++;
        }
        
        //  clip.UnloadAudioData();
        //  Destroy(clip); leaked?
        PAM.Logger.LogInfo($"Level is [{songDataShort.Length * 2}] bytes");
        _data[id] = new(songDataShort, frequency);
    }
}