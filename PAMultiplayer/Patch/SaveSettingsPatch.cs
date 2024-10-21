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
        };
        
        string json = JsonSerializer.Serialize(settings);
        File.WriteAllText(settingsPath + "multiplayer-settings.vgc", json);
    }

    [HarmonyPatch(nameof(SettingsManager.OnAwake))]
    [HarmonyPrefix]
    static void PreStart()
    {
        string path = Directory.GetCurrentDirectory() + "\\settings\\multiplayer-settings.vgc";
        if (!File.Exists(path)) return;

        MultiplayerSettings settings = JsonSerializer.Deserialize<MultiplayerSettings>(File.ReadAllText(path));
        
        DataManager.inst.UpdateSettingInt("MpPlayerWarpSFX", settings.PlayerWarpSFX);
        DataManager.inst.UpdateSettingInt("MpPlayerSFX", settings.PlayerHitSFX);
        DataManager.inst.UpdateSettingBool("MpTransparentPlayer", settings.TransparentPlayer);
    }
}

public class MultiplayerSettings 
{
    public int PlayerWarpSFX { get; set; }
    public int PlayerHitSFX { get; set; }
    public bool TransparentPlayer { get; set; }
}