using System.IO;
using HarmonyLib;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PAMultiplayer.Patch;

/// <summary>
/// responsible for saving custom settings to the settings folder.
/// </summary>
[HarmonyPatch(typeof(SettingsManager))]
public static class SaveSettingsPatch
{
    [HarmonyPatch(nameof(SettingsManager.UpdateSettingsFile))]
    [HarmonyPrefix]
    static void PreSaveSettings()
    {
        string settingsPath = Directory.GetCurrentDirectory() + "\\settings\\";
        if (!Directory.Exists(settingsPath)) return;
        
        var settings = new MultiplayerSettings()
        {
            PlayerWarpSFX = DataManager.inst.GetSettingInt("MpPlayerWarpSFX", 1),
            PlayerHitSFX = DataManager.inst.GetSettingInt("MpPlayerSFX", 0),
            TransparentPlayer = DataManager.inst.GetSettingBool("MpTransparentPlayer", false),
            LinkedHealthPopup = DataManager.inst.GetSettingBool("MpLinkedHealthPopup", true)
        };
        
        string json = JsonSerializer.Serialize(settings);
        File.WriteAllText(settingsPath + "multiplayer-settings.vgc", json);
    }

    [HarmonyPatch(nameof(SettingsManager.OnAwake))]
    [HarmonyPrefix]
    static void PreStart()
    {
        string path = Directory.GetCurrentDirectory() + "\\settings\\multiplayer-settings.vgc";
        
        MultiplayerSettings settings = new();
        
        if (File.Exists(path))
        {
            settings = JsonSerializer.Deserialize<MultiplayerSettings>(File.ReadAllText(path));
        }
        
        DataManager.inst.UpdateSettingInt("MpPlayerWarpSFX", settings.PlayerWarpSFX);
        DataManager.inst.UpdateSettingInt("MpPlayerSFX", settings.PlayerHitSFX);
        DataManager.inst.UpdateSettingBool("MpTransparentPlayer", settings.TransparentPlayer);
        DataManager.inst.UpdateSettingBool("MpLinkedHealthPopup", settings.LinkedHealthPopup);
    }
}

public class MultiplayerSettings
{
    public int PlayerWarpSFX { get; set; } = 1;
    public int PlayerHitSFX { get; set; }
    
    public bool TransparentPlayer { get; set; }
    public bool LinkedHealthPopup { get; set; } = true;
}