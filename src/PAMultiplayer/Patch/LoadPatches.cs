using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using DG.Tweening.Core;
using HarmonyLib;

namespace PAMultiplayer.Patch;

[HarmonyPatch]
public static class LoadPatches
{
    [HarmonyPatch(typeof(Sequence), nameof(Sequence.StableSortSequencedObjs))]
    [HarmonyPrefix]
    private static bool StableSortSequencedObjs(List<ABSSequentiable> list)
    {
        if (list.Count < 200)
        {
            return true;
        }
        
        var list2 = list.OrderBy(x => x.sequencedPosition).ToArray();
        list.Clear();
        list.AddRange(list2);
        return false;
    }
}