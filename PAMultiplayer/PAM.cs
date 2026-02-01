using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Project Arrhythmia.exe")]
[BepInDependency("me.ytarame.PaApi")]
public class PAM : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;

    private Harmony _harmony;
    public const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    public const string Version = "1.1.1";

    private void Awake()
    {
        Logger = base.Logger;

        _harmony = new Harmony(Guid);
        _harmony.PatchAll();

        Settings.Initialize(Config);
       
        PaApi.SettingsHelper.RegisterModSettings(Guid, "Multiplayer", Color.red, Config, builder =>
        {
            builder.Label("<b>TRANSPARENT NANOS</b> - and related settings");
        
            builder.Toggle("Transparent Nanos", Settings.Transparent);
            builder.Slider("Transparent Opacity", Settings.TransparentAlpha, UI_Slider.VisualType.line, "35%", "50%", "85%");
            builder.Spacer();
        
            builder.Label("<b>MISCELLANEOUS</b> - other settings");
        
            builder.Slider("No Repeats in Challenge", Settings.NoRepeat, UI_Slider.VisualType.line, "0 Rounds", "1 Round", "2 Rounds", "3 Rounds", "Infinite");
            builder.Toggle("Chat Enabled", Settings.Chat);
            builder.Toggle("Linked Health Hit Popup", Settings.Linked);
            builder.Toggle("Allow hidden workshop levels", Settings.AllowNonPublicLevels);
        });
        Logger.LogFatal($"Multiplayer has loaded!");
    }
}
