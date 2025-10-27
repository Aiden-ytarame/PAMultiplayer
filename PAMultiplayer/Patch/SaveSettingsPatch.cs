using BepInEx.Configuration;
using HarmonyLib;

namespace PAMultiplayer.Patch;

/// <summary>
/// responsible for saving custom settings to the settings folder.
/// </summary>
[HarmonyPatch(typeof(SettingsManager))]
public static class SaveSettingsPatch
{
    private static ConfigEntry<int> _warpSfx;
    private static ConfigEntry<int> _hitSfx;
    private static ConfigEntry<bool> _trasnparent;
    private static ConfigEntry<int> _transparentAlpha;
    private static ConfigEntry<int> _noRepeat;
    private static ConfigEntry<bool> _linked;
    private static ConfigEntry<bool> _chat;
    private static ConfigEntry<bool> _allowNonPublicLevels;

    [HarmonyPatch(nameof(SettingsManager.UpdateSettingsFile))]
    [HarmonyPrefix]
    static void PreSaveSettings()
    {
        _warpSfx.Value = DataManager.inst.GetSettingInt("MpPlayerWarpSFX", 1);
        _hitSfx.Value = DataManager.inst.GetSettingInt("MpPlayerSFX", 0);
        _trasnparent.Value = DataManager.inst.GetSettingBool("MpTransparentPlayer", true);
        _transparentAlpha.Value = DataManager.inst.GetSettingInt("MpTransparentPlayerAlpha", 0);
        _noRepeat.Value = DataManager.inst.GetSettingInt("MpNoRepeat", 0);
        _linked.Value = DataManager.inst.GetSettingBool("MpLinkedHealthPopup", true);
        _chat.Value = DataManager.inst.GetSettingBool("MpChatEnabled", true);
        _allowNonPublicLevels.Value = DataManager.inst.GetSettingBool("MpAllowNonPublicLevels", false);
        
        PAM.Inst.Config.Save();
    }

    [HarmonyPatch(nameof(SettingsManager.OnAwake))]
    [HarmonyPrefix]
    static void PreStart()
    {
        _warpSfx = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Player Warp SFX"), 1);
        _hitSfx = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Player Hit SFX"), 0);
        _trasnparent = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Transparent Nanos"), true);
        _transparentAlpha = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Transparent Alpha"), 0);
        _noRepeat = PAM.Inst.Config.Bind(new ConfigDefinition("General", "No Repeat"), 0);
        _linked = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Linked Health Popup"), true);
        _chat = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Chat Enabled"), true);
        _allowNonPublicLevels = PAM.Inst.Config.Bind(new ConfigDefinition("General", "Allow Private Levels"), false);
        
        DataManager.inst.UpdateSettingInt("MpPlayerWarpSFX", _warpSfx.Value);
        DataManager.inst.UpdateSettingInt("MpPlayerSFX", _hitSfx.Value);
        DataManager.inst.UpdateSettingBool("MpTransparentPlayer", _trasnparent.Value);
        DataManager.inst.UpdateSettingInt("MpTransparentPlayerAlpha", _transparentAlpha.Value);
        DataManager.inst.UpdateSettingInt("MpNoRepeat", _noRepeat.Value);
        DataManager.inst.UpdateSettingBool("MpLinkedHealthPopup", _linked.Value);
        DataManager.inst.UpdateSettingBool("MpChatEnabled", _chat.Value);
        DataManager.inst.UpdateSettingBool("MpAllowNonPublicLevels", _allowNonPublicLevels.Value);
        
    }
}
