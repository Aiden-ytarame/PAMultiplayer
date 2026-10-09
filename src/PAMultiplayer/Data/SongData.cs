using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    public IEnumerator SetupSongData(IEnumerable<VGLevel> levels)
    {
        List<Task> tasks = new(6);
        
        foreach (var vgLevel in levels)
        {
            string id = vgLevel.BaseLevelData.LevelID;
            string localFolder = vgLevel.BaseLevelData.LocalFolder;
            
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
                yield return webr.SendWebRequest(); //cant be inside Try block

                try
                {
                    clip = DownloadHandlerAudioClip.GetContent(webr);
                }
                catch (Exception e)
                {
                    PAM.Logger.LogError(e);
                    continue;
                }
            }
            
            tasks.Add(Task.Run(() =>
            {
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
        
                //  clip.UnloadAudioData();
                //  Destroy(clip); leaked?
                PAM.Logger.LogInfo($"Level is [{songDataShort.Length * 2}] bytes");
                _data[id] = new(songDataShort, frequency);
            }));

            for (var i = 0; i < tasks.Count;)
            {
                var task = tasks[i];

                if (task.IsCompleted)
                {
                    tasks.RemoveAt(i);
                    SongDataUpdated?.Invoke(_data.Count);
                    continue;
                }

                i++;
            }
            yield return new WaitForUpdate();
        }

        while (tasks.Count > 0)
        {
            var any = Task.WhenAny(tasks);
            while (!any.IsCompleted)
            {
                yield return null;
            }

            tasks.Remove(any.Result);
            SongDataUpdated?.Invoke(_data.Count);
        }
    }
}