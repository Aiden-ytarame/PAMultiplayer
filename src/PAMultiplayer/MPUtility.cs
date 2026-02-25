using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace PAMultiplayer;

public static class MPUtility
{
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
}