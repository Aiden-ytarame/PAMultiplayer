﻿using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PAMultiplayer.Patch;
using Il2CppInterop.Runtime.Injection;
using PAMultiplayer.Managers;

namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
public class Plugin : BasePlugin
{
    public static Plugin Inst;
    public static ManualLogSource Logger => Inst.Log;
    Harmony harmony;
    const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    const string Version = "0.0.1";


    public override void Load()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NetworkManager>();
        ClassInjector.RegisterTypeInIl2Cpp<LobbyManager>();
        ClassInjector.RegisterTypeInIl2Cpp<SteamManager>();
        ClassInjector.RegisterTypeInIl2Cpp<SteamLobbyManager>();
        
    
        Inst = this;
        harmony = new Harmony(Guid);
        harmony.PatchAll();

        // Plugin startup logic
        Log.LogInfo($"Plugin {Guid} is loaded!");

    }

}
