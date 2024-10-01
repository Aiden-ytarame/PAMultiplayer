using HarmonyLib;
using Lachee.Discord;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(DiscordRPTest))]
public static class DiscordTestPatch
{
    [HarmonyPatch(nameof(DiscordRPTest.Start))]
    [HarmonyPrefix]
    static bool stopPresence(ref DiscordRPTest __instance)
    {
        return false;
    }
}


[HarmonyPatch(typeof(DiscordManager))]
public static class DiscordManagerPatch
{
    public readonly static Button[] DefaultButtons = new[]
    {
        new Button() {label = "Get the game!", url = "steam://advertise/440310"},
        new Button() {label = "Get the multiplayer mod!", url = "https://github.com/Aiden-ytarame/PAMultiplayer"}
    };
    
    [HarmonyPatch(nameof(DiscordManager.Awake))]
    [HarmonyPrefix]
    static void stopPresence(ref DiscordManager __instance)
    {
        var presence = new Presence()
        {
            state = "Navigating Menus",
            details = ""
        };
        presence.largeAsset = new Asset() { image = "palogo", tooltip = "Game Logo"};
        presence.smallAsset = new Asset() { image = "pamplogo2", tooltip = "Multiplayer Logo" };
        presence.buttons = DefaultButtons;
        __instance.applicationID = "1282511280833298483";
        __instance._currentPresence = presence;
       
    }
}
