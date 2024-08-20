using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;

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
    const string Version = "0.4.1";

    public override void Load()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NetworkManager>();
        ClassInjector.RegisterTypeInIl2Cpp<LobbyScreenManager>();
        ClassInjector.RegisterTypeInIl2Cpp<SteamManager>();
        ClassInjector.RegisterTypeInIl2Cpp<SteamLobbyManager>();
        Inst = this;
        harmony = new Harmony(Guid);
        harmony.PatchAll();
        
        var loadGameMoveNext = typeof(GameManager).GetNestedTypes().FirstOrDefault(t => t.Name.Contains("LoadGame"))?
            .GetMethod("MoveNext");
        
        var ReverseMoveNext = typeof(GameManager).GetNestedTypes().FirstOrDefault(t => t.Name.Contains("ReverseToCheckpointLoop"))?
            .GetMethod("MoveNext");
       
        var prefix = new HarmonyMethod(typeof(GameManagerPatch).GetMethod("OverrideLoadGame"));
        var reversePostfix = new HarmonyMethod(typeof(GameManagerPatch).GetMethod("PostRewind"));
        
        harmony.Patch(loadGameMoveNext, prefix);
        harmony.Patch(ReverseMoveNext, null, reversePostfix);
        
        Log.LogInfo($"Plugin {Guid} is loaded!");
    }
    
}
