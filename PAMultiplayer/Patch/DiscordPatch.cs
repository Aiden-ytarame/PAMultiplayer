using System;
using System.Diagnostics;
using DiscordRPC.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Lachee.Discord;
using Microsoft.Extensions.Logging;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = DiscordRPC.Logging.LogLevel;
using Object = Il2CppSystem.Object;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(DiscordRPTest))]
public static class DiscordTestPatch
{
    [HarmonyPatch(nameof(DiscordRPTest.Start))]
    [HarmonyPrefix]
    static bool stopPresence(ref DiscordRPTest __instance)
    {
        return false;
    }
}


[HarmonyPatch(typeof(DiscordManager))]
public static class DiscordManagerPatch
{
    public readonly static Button[] DefaultButtons = new[]
    {
        new Button() {label = "Get the game!", url = "steam://advertise/440310"},
        new Button() {label = "Get the multiplayer mod!", url = "https://github.com/Aiden-ytarame/PAMultiplayer"}
    };
    
    [HarmonyPatch(nameof(DiscordManager.Awake))]
    [HarmonyPrefix]
    static void stopPresence(ref DiscordManager __instance)
    {
        var presence = new Presence()
        {
            state = "Navigating Menus",
            details = ""
        };
        
        presence.largeAsset = new Asset() { image = "pamplogo2", tooltip = "Multiplayer Mod Logo"};
        presence.smallAsset = new Asset() { image = "", tooltip = "" };
        presence.buttons = DefaultButtons;
        __instance.applicationID = "1282511280833298483";
        __instance._currentPresence = presence;
    }
    
}
