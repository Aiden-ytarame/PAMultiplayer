using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
[BepInDependency("me.ytarame.PaApi")]
public class PAM : BaseUnityPlugin
{
    public static PAM Inst;
    internal new static ManualLogSource Logger;
    
    Harmony harmony;
    const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    public const string Version = "1.1.0";

    private void Awake()
    {
        Logger = base.Logger;
        Inst = this;
        
        harmony = new Harmony(Guid);
        harmony.PatchAll();

        Settings.Initialize(Config);
        
        PaApi.SettingsHelper.RegisterModSettings(Guid, "Multiplayer", Config, builder =>
        {
            builder.InstantiateLabel("<b>TRANSPARENT NANOS</b> - and related settings");
        
            builder.InstantiateToggle("Transparent Nanos", Settings.Transparent);
            builder.InstantiateSlider("Transparent Opacity", Settings.TransparentAlpha, UI_Slider.VisualType.line, "35%", "50%", "85%");
            builder.InstantiateSpacer();
        
            builder.InstantiateLabel("<b>MISCELLANEOUS</b> - other settings");
        
            builder.InstantiateSlider("No Repeats in Challenge", Settings.NoRepeat, UI_Slider.VisualType.line, "0 Rounds", "1 Round", "2 Rounds", "3 Rounds", "Infinite");
            builder.InstantiateToggle("Chat Enabled", Settings.Chat);
            builder.InstantiateToggle("Linked Health Hit Popup", Settings.Linked);
            builder.InstantiateToggle("Allow hidden workshop levels", Settings.AllowNonPublicLevels);
            builder.InstantiateSpacer();
        });
        Logger.LogFatal($"Multiplayer has loaded!");
    }
}
