﻿using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;
using UnityEngine.Localization.Settings;


namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
public class PAM : BasePlugin
{
    public static PAM Inst;
    
    public static ManualLogSource Logger => Inst.Log;
    
    Harmony harmony;
    const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    public const string Version = "0.9.0";

    public override void Load()
    {
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<TablePostprocessor>(new RegisterTypeOptions
            {
                Interfaces = new Il2CppInterfaceCollection(new[] { typeof(ITablePostprocessor) })
            });
            var postprocessor = new TablePostprocessor();
            var provider = new ITablePostprocessor(postprocessor.Pointer);
        
            LocalizationSettings.StringDatabase.TablePostprocessor = provider;
            
            ClassInjector.RegisterTypeInIl2Cpp<NetworkManager>();
            ClassInjector.RegisterTypeInIl2Cpp<QueueButton>();
            ClassInjector.RegisterTypeInIl2Cpp<LobbyScreenManager>();
            ClassInjector.RegisterTypeInIl2Cpp<SteamManager>();
            ClassInjector.RegisterTypeInIl2Cpp<SteamLobbyManager>();
            ClassInjector.RegisterTypeInIl2Cpp<MultiplayerDiscordManager>();
            
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        //patch all
        Inst = this;
        harmony = new Harmony(Guid);
        harmony.PatchAll();
        
        var loadGameMoveNext = typeof(GameManager).GetNestedTypes().FirstOrDefault(t => t.Name.Contains("LoadGame"))?
            .GetMethod("MoveNext");
        var prefix = new HarmonyMethod(typeof(GameManagerPatch).GetMethod("OverrideLoadGame"));
        harmony.Patch(loadGameMoveNext, prefix);


        Log.LogInfo($"Plugin {Guid} is loaded!");
    }
}
