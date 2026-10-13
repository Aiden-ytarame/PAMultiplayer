using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PAMultiplayer.Patch;
using Systems.SceneManagement;
using TMPro;
using UnityEngine;

namespace PAMultiplayer;

public static class MPUtility
{
    private static List<VGLevel> _storyLevels = null;

    public static VGLevel LookForLevel(string levelID)
    {
        VGLevel level = GetStoryLevel(levelID);
        if (!level)
        {
            level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(levelID);
        }

        return level;
    }
    
    public static int StoryLevelCount
    {
        get
        {
            return _storyLevels?.Count ?? 0;
        }
    }
    
    public static VGLevel GetStoryLevel(string id)
    {
        return _storyLevels?.FirstOrDefault(x => x.BaseLevelData.LevelID == id);
    }

    public static List<VGLevel> GetStoryLevels()
    {
        return _storyLevels;
    }
    
    public static bool IsStoryModeBeaten()
    {
        return SavesManager.Inst && !string.IsNullOrEmpty(SavesManager.Inst.GetLastEndingId());
    }
    
    public static void SetFullTextGradient(TMP_TextInfo textInfo, Color32 colorLeft, Color32 colorRight)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        
        for (int i = 0; i < textInfo.characterCount; ++i)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
            {
                continue;
            }
            
            int meshIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            
            Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
            
            float x = vertices[vertexIndex].x;
            min = Mathf.Min(min, x);
            
            x = vertices[vertexIndex + 2].x;
            max = Mathf.Max(max, x);
        }

        for (int i = 0; i < textInfo.characterCount; ++i)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
            {
                continue;
            }

            int meshIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            
            Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
            
            Color32 color1 = Color32.Lerp(colorLeft, colorRight, Mathf.InverseLerp(min, max, vertices[vertexIndex].x));
            Color32 color2 = Color32.Lerp(colorLeft, colorRight, Mathf.InverseLerp(min, max, vertices[vertexIndex + 2].x));
          
            Color32[] vertexColors = textInfo.meshInfo[meshIndex].colors32;
            vertexColors[vertexIndex + 0] = color1; //bottom left
            vertexColors[vertexIndex + 1] = color1; // top left
            vertexColors[vertexIndex + 2] = color2; //top right
            vertexColors[vertexIndex + 3] = color2; //bottom right
        }
    }
    
    public static void ReleaseClip(AudioClip clip, bool baked = false)
    {
        if (LevelMusicLoader.Manager.IsPlaybackClip(clip))
        {
            AudioManager.Inst?.ReleaseMusicClip(clip);
            LevelMusicLoader.playbackLevel.LevelMusic = null;
            LevelMusicLoader.Manager.ReleasePlayback();
            return;
        }
        
        if (clip)
        {
            clip.UnloadAudioData();
            if (!baked)
            {
                Object.Destroy(clip);
            }
        }
    }
    
    public static void DelayLoadScene(string sceneId)
    {
        SystemManager.inst.StartCoroutine(InternalDelayLoadScene(sceneId));
    }

    private static IEnumerator InternalDelayLoadScene(string id)
    {
        if (MultiplayerUIManager.Inst && MultiplayerUIManager.Inst.IsActive)
        {
            MultiplayerUIManager.Inst.CloseUI();
        }
        
        yield return new WaitUntil(() => SceneLoader.Inst.CanLoadSceneGroup);
        
        if (_storyLevels == null)
        {
            if (id != "Arcade")
            {
                LoadingTipsPatch.IgnoreArcade = true;
            }
            
            SceneLoader.Inst.LoadSceneGroup("Arcade");
            yield return new WaitUntil(() => SceneLoader.Inst.CanLoadSceneGroup);
            
            _storyLevels = new();
            var bakedLevels = Resources.FindObjectsOfTypeAll<VGLevel>().Where(x => x.IsBakedLevel);
            _storyLevels.AddRange(bakedLevels);

            if (id == "Arcade")
            {
                yield break;
            }
        }
        
        SceneLoader.Inst.LoadSceneGroup(id);
    }
    
    public static CoroutineWrapper WrapCoroutine(this MonoBehaviour mb, IEnumerator coroutine) => new CoroutineWrapper(mb, coroutine);
    public static WaitUntil WaitUntilComplete(this Task task) => new(() => task.IsCompleted);
}