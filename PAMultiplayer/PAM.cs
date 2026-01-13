using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.Utils;
using PAMultiplayer.Helper;
using PAMultiplayer.Patch;
using Unity.TLS;
using UnityEngine;

namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
public class PAM : BaseUnityPlugin
{
    public static PAM Inst;
    internal new static ManualLogSource Logger;
    
    Harmony harmony;
    const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    public const string Version = "1.2.0";

    private void Awake()
    {
        Logger = base.Logger;
        Inst = this;
        
        harmony = new Harmony(Guid);
        harmony.PatchAll();
     
        Logger.LogFatal($"Multiplayer has loaded!");
    }
}
