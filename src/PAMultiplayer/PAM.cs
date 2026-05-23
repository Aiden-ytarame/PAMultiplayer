using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Crosstales;
using HarmonyLib;
using UnityEngine;

namespace PAMultiplayer;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("me.ytarame.PaApi")]
public class PAM : BaseUnityPlugin
{
    internal new static ManualLogSource Logger;

    internal static GameObject ErrorScreenPrefab { get; private set; }
    internal static GameObject LobbyScreenPrefab { get; private set; }
    internal static GameObject LobbyPlayerEntryPrefab { get; private set; }
    internal static GameObject LobbyQueueEntryPrefab { get; private set; }

    private Harmony _harmony;
    public const string Guid = "me.ytarame.Multiplayer";
    const string Name = "Multiplayer";
    public const string Version = "1.3.0";

    private void Awake()
    {
        Logger = base.Logger;

        _harmony = new Harmony(Guid);
        _harmony.PatchAll();
        
        Settings.Initialize(Config);
       
        using (var stream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream("PAMultiplayer.Assets.lobbymenuv2"))
        {
            var lobbyBundle = AssetBundle.LoadFromMemory(stream!.CTReadFully());
            
            ErrorScreenPrefab = lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[0]) as GameObject;
            LobbyScreenPrefab = lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[2]) as GameObject;
            LobbyPlayerEntryPrefab = lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[3]) as GameObject;
            LobbyQueueEntryPrefab = lobbyBundle.LoadAsset(lobbyBundle.GetAllAssetNames()[4]) as GameObject;
          
            lobbyBundle.Unload(false);
        }

        PaApi.SettingsHelper.RegisterModSettings(Guid, "Multiplayer", Color.red, Config, builder =>
        {
            builder.Label("<b>TRANSPARENT NANOS</b> - and related settings");
        
            builder.Toggle("Transparent Nanos", "Remote players are transparent", "Remote players are opaque", Settings.Transparent);
            builder.Slider("Transparent Opacity", Settings.TransparentAlpha, UI_Slider.VisualType.line, "35%", "50%", "85%");
            builder.Spacer();
        
            builder.Label("<b>MISCELLANEOUS</b> - other settings");
        
            builder.Slider("No Repeats in Challenge", Settings.NoRepeat, UI_Slider.VisualType.line, "0 Rounds", "1 Round", "2 Rounds", "3 Rounds", "Infinite");
            builder.Toggle("Chat Enabled", "Chat appears above their nano and terminal", "Player chat is not shown", Settings.Chat);
            builder.Toggle("Disable Rich Text", "Chat Rich text enabled", "Chat rich text disabled", Settings.DisableRichText);
            builder.Toggle("Linked Health Hit Popup", "Popup for which player caused the hit", "No popup appears", Settings.Linked);
            builder.Toggle("Allow hidden workshop levels", "Non public levels are allowed", "Only public are allowed", Settings.AllowNonPublicLevels);
        });
        
        Logger.LogInfo($"Multiplayer {Version} has loaded!");
    }
}
