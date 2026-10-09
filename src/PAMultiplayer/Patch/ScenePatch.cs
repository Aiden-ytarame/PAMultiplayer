using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Crosstales;
using Eflatun.SceneReference;
using HarmonyLib;
using PAMultiplayer.Managers;
using Systems.SceneManagement;
using UnityEngine.SceneManagement;
using VGFunctions;
using Object = UnityEngine.Object;


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
            "Adding more bloom...",
            "Wait... what multiplayer?",
            "Afterbeat multiplayer...? Too long... BeatMultiplayer... Beat Toget-\nLets keep it at Project Arrhythmia Multiplayer"
        };
        //thanks Pidge for making this public after I complained lol
        __instance.Tips = customTips.ToArray();
        
        SceneManager.sceneLoaded += (scene, _) =>
        {
            try
            {
                //just in-casse
                if (scene.name == "Arcade" || scene.name == "Menu")
                {
                    ChallengeManager.RecentLevels.Clear();
                    GlobalsManager.IsChallenge = false;
                    GlobalsManager.LocalPlayerObjectId = 0;
                    GlobalsManager.Players.Clear();
                    VGPlayerManager.Inst.players.Clear();
                    VGPlayerManager.Inst.players.Add(new VGPlayerManager.VGPlayerData()
                        { ControllerID = 0, PlayerID = 0 });

                    SteamManager.Inst.DisconnectAll();
                }
            }
            catch (Exception e)
            {
                PAM.Logger.LogError(e);
            }

            
            if (scene.name == "Menu")
            {
                LSHelpers.Delay(.1f, () =>
                {
                    ShowChangeLog show = Object.FindObjectOfType<ShowChangeLog>();
                    if (show)
                    {
                        UpdateModButtonPatches.HandleMenuCreation(show);
                    }
                });
            }
        };
    }
    
}