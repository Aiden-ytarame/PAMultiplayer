using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Xml.Linq;
using System;
using BepInEx.Logging;
using YtaramMultiplayer.Patch;
using Il2CppMono;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine.Events;
namespace YtaramMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
public class Plugin : BasePlugin
{
    public static Plugin Instance;

    Harmony harmony;
    public const string Guid = "me.ytarame.Multiplayer";
    public const string Name = "Multiplayer";
    public const string Version = "0.0.1";


    public override void Load()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NetworkManager>();
        ClassInjector.RegisterTypeInIl2Cpp<UpdateIpAndPort>();
        
        Instance = this;
        harmony = new Harmony(Guid);
        harmony.PatchAll();

        // Plugin startup logic
        Log.LogInfo($"Plugin {Guid} is loaded! WOOOOOOOOOOOO");

    }

}
