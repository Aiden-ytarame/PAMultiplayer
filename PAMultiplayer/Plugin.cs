using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using PAMultiplayer.Patch;
using Il2CppInterop.Runtime.Injection;
namespace PAMultiplayer;

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
        ClassInjector.RegisterTypeInIl2Cpp<LobbyManager>();
    
        Instance = this;
        harmony = new Harmony(Guid);
        harmony.PatchAll();

        // Plugin startup logic
        Log.LogInfo($"Plugin {Guid} is loaded! WOOOOOOOOOOOO");

    }

}
