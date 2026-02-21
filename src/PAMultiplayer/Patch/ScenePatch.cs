using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Crosstales;
using Eflatun.SceneReference;
using HarmonyLib;
using PAMultiplayer.Managers;
using Systems.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace PAMultiplayer.Patch;

/// <summary>
/// just having fun with loading screen Tips
/// </summary>
[HarmonyPatch(typeof(SceneLoader))]
public static class LoadingTipsPatch
{
    [HarmonyPatch(nameof(SceneLoader.Start))]
    [HarmonyPostfix]
    static void GetterTips(ref SceneLoader __instance)
    {
        var customTips = new List<string>(__instance.Tips)
        {
            "You should try the log Unerfed Fallen Kingdom!",
            "You can always call other Nanos for help!",
            "Git Gud",
            "I'm in your walls.",
            "Good Nano~",
            "No tips for you >:)",
            "Boykisser sent kisses!",
            "Girlkisser sent kisses!",
            "Theykisser sent kisses!",
            "The developer wants me to say something here.",
            "You might be a Nano but you should hydrate anyways.",
            "Before time began there was The Cube...",
            "Ready to be carried by another Nano again?",
            "Squeezing your Nano through the internet wire...",
            "The triangle is the simplest shape a computer can render",
            "Make sure to check out the game's official forum!",
            "Meow!",
            "Some Nanos seem to keep replaying some logs until they master it\nUnsure how productive that may be",
            "Cats rule the world",
            "Don't die... That's probably a good idea?",
            "Try to be the top Nano of your generation for once",
            "lol is is likej sab",
            "I got a box of chocolates for you! One of them has rat poison.",
            "Adding more bloom..."
        };
        //thanks Pidge for making this public after I complained lol
        __instance.Tips = customTips.ToArray();
        
        AddChallengeScene();
    }

    static void AddChallengeScene()
    {
        //this adds a new scene group for the challenge mode, terrible code
        //adding to an il2cpp list is kinda trash, so we do this horrible hack
        List<SceneGroup> groups = new(SceneLoader.Inst.sceneGroups);
        if (groups.All(x => x.GroupName != "Challenge"))
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("PAMultiplayer.Assets.challenge");

            var lobbyBundle = AssetBundle.LoadFromMemory(stream!.CTReadFully());

            var scene = lobbyBundle.GetAllScenePaths()[0];
            var guid = "11f830737ff4bc41a4ffe792d073f41f";

            SceneGroup sceneGroup = new()
            {
                GroupName = "Challenge",
                GroupType = SceneGroupType.GAME,
                Scenes = new()
            };

            SceneData sceneData = new()
            {
                SceneType = SceneType.ACTIVE,
                Reference = new SceneReference()
                {
                    guid = guid
                }
            };

            //load scene group makes use of these
            SceneGuidToPathMapProvider._sceneGuidToPathMap.Add(guid, scene);
            SceneGuidToPathMapProvider._scenePathToGuidMap.Add(scene, guid);

            sceneGroup.Scenes.Add(sceneData);
            groups.Add(sceneGroup);
            SceneLoader.Inst.sceneGroups = groups.ToArray();
            SceneManager.sceneLoaded += (scene, _) =>
            {
                //just in-casse
                if (scene.name == "Arcade" || scene.name == "Menu")
                {
                    ChallengeManager.RecentLevels.Clear();
                    
                    if (GlobalsManager.IsMultiplayer)
                    {
                        SteamManager.Inst.EndServer();
                        SteamManager.Inst.EndClient();
                    }
                }
             
                if (scene.name != "Challenge")
                    return;

                //asset bundles dont load custom scripts, we gotta add it here on scene load
                //horrible code, please end me
                var manager = scene.GetRootGameObjects().First(x => x.name == "Managers");
                manager.AddComponent<ChallengeManager>();
            };

            //unloading the asset bundle unloads the scene
            //lobbyBundle.Unload(false);
        }
    }
}


// what was I cooking? did il2cpp require this? I am no longer sure but we leave it here 
[HarmonyPatch(typeof(SceneReference))]
public static class SceneReferencePatch
{
    [HarmonyPatch(nameof(SceneReference.State), MethodType.Getter)]
    [HarmonyPrefix]
    static bool GetStatePatch(SceneReference __instance, ref SceneReferenceState __result)
    {
        __result = SceneReferenceState.Unsafe;
        if (__instance.HasValue)
        {
            if (SceneGuidToPathMapProvider.SceneGuidToPathMap.TryGetValue(__instance.Guid, out var path))
            {
                __result = SceneReferenceState.Regular;
            }
        }
        return false;
    }
}
