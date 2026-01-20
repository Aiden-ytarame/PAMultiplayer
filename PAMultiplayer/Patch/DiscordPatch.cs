using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DiscordRPC.IO;
using HarmonyLib;
using Lachee.Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using PAMultiplayer;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Patch;


[HarmonyPatch(typeof(DiscordManager))]
public static class DiscordManagerPatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPrefix]
    static void stopPresence(DiscordManager __instance)
    {
        JsonConvert.DefaultSettings = null; //We hate newtonsoft.json
        __instance.gameObject.AddComponent<MultiplayerDiscordManager>();
        UnityEngine.Object.Destroy(__instance);
    }
    
}

