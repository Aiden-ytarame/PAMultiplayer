using System;
using BepInEx.Configuration;

namespace PAMultiplayer;

internal static class Settings
{
    public static ConfigEntry<string> ChallengeBlacklist;
    public static ConfigEntry<int> WarpSfx {get; private set;}
    public static ConfigEntry<int> HitSfx {get; private set;}
    public static ConfigEntry<bool> Transparent {get; private set;}
    public static ConfigEntry<int> TransparentAlpha {get; private set;}
    public static ConfigEntry<int> NoRepeat {get; private set;}
    public static ConfigEntry<bool> Chat {get; private set;}
    public static ConfigEntry<bool> Linked {get; private set;}
    public static ConfigEntry<bool> AllowNonPublicLevels {get; private set;}
    public static ConfigEntry<int> Score {get; private set;}

    internal static void Initialize(ConfigFile config)
    {
        if (WarpSfx != null)
        {
            return;
        }

        ChallengeBlacklist = config.Bind(new ConfigDefinition("Challenge", "BlackList"), "", new ConfigDescription("List of level id's that are never chosen in challenge, separated by \'/\'. like id/id2/id3, no spaces"));
        
        WarpSfx = config.Bind(new ConfigDefinition("General", "Player Warp SFX"), 1);
        HitSfx = config.Bind(new ConfigDefinition("General", "Player Hit SFX"), 0);
        Transparent = config.Bind(new ConfigDefinition("General", "Transparent Nanos"), true);
        TransparentAlpha = config.Bind(new ConfigDefinition("General", "Transparent Alpha"), 0);
        NoRepeat = config.Bind(new ConfigDefinition("General", "No Repeat"), 0);
        Chat = config.Bind(new ConfigDefinition("General", "Chat Enabled"), true);
        Linked = config.Bind(new ConfigDefinition("General", "Linked Health Popup"), true);
        AllowNonPublicLevels = config.Bind(new ConfigDefinition("General", "Allow Private Levels"), false);
        Score = config.Bind(new ConfigDefinition("General", "Score"), 0, new ConfigDescription("I know you\'re thinking about it, dont do it... be legitimate..."));
    }
}