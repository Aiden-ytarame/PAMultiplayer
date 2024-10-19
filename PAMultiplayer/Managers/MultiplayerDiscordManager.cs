using System;
using DiscordRPC.Message;
using BepInEx;
using DiscordRPC;
using DiscordRPC.Logging;
using PAMultiplayer;
using PAMultiplayer.Managers;
using Steamworks;
using UnityEngine;

/// <summary>
/// The discord manager the game uses is very finicky and broken so we use this instead.
/// </summary>
public class MultiplayerDiscordManager : MonoBehaviour
{
	public static MultiplayerDiscordManager Instance{get; private set;}

	public static bool IsInitialized
	{
		get
		{
			return Instance && Instance.client != null && Instance.client.CurrentUser != null;
		}
	}

	private DiscordRpcClient client = null;
	private RichPresence presence = null;

	private const string ApplicationId = "1282511280833298483";

	private static readonly Button[] Buttons = new []
	{
		new Button() {Label = "Get the game!", Url = "steam://advertise/440310"},
		new Button() {Label = "Get the multiplayer mod!", Url = "https://github.com/Aiden-ytarame/PAMultiplayer"}
	};

	private void FixedUpdate()
	{
		if (client != null)
		{
			client.Invoke();
		}
	}

	private void Start()
	{
		if (Instance != null)
		{
			Destroy(this);
			return;
		}
		
		Instance = this;

		client = new DiscordRpcClient(
			ApplicationId,
			-1,
			new ConsoleLogger(LogLevel.Warning),
			false);
		
		
		client.OnError += (_, e) => Plugin.Logger.LogError($"An error occurred with Discord RPC Client: {e.Message} ({e.Code})");
		client.OnReady += onReady;

		client.OnJoin += ClientOnOnJoin;
		client.Subscribe(EventType.Join);
		client.RegisterUriScheme("440310", Paths.ExecutablePath);

		client.Initialize();
	}

	private void ClientOnOnJoin(object sender, JoinMessage joinSecret)
	{
		if (SteamClient.IsValid && !GlobalsManager.IsMultiplayer)
		{
			if (ulong.TryParse(joinSecret.Secret, out ulong id))
			{
				Plugin.Logger.LogInfo("Attempting to join lobby from discord invite.");
				GlobalsManager.IsHosting = false;
				GlobalsManager.IsMultiplayer = true;
				SteamMatchmaking.JoinLobbyAsync(id);
			}
			else
			{
				Plugin.Logger.LogError("Failed to parse secret.");
			}
		}
		Plugin.Logger.LogError("Failed to join lobby from discord, steam wasn't initialized.");
	}

	private void onReady(object _, ReadyMessage __)
	{
		presence = new RichPresence();
		presence.Assets = new Assets()
		{
			SmallImageKey = "pamplogo2",
			SmallImageText = "Multiplayer Logo"
		};
		SetMenuPresence();
		client.SetPresence(presence);
	}

	public void SetLevelPresence(string state, string details, string levelCoverUrl)
	{
		presence.State = state;
		presence.Details = details;
		presence.Assets.LargeImageKey = levelCoverUrl;
		presence.Assets.LargeImageText = "Level Cover";
		presence.Timestamps = new Timestamps(DateTime.UtcNow);

		if (GlobalsManager.IsMultiplayer)
		{
			string id = SteamLobbyManager.Inst.CurrentLobby.Id.ToString();
			presence.Party = new Party()
			{
				ID = id + SteamLobbyManager.Inst.CurrentLobby.Owner.Id,
				Max = 16,
				Size = SteamLobbyManager.Inst.CurrentLobby.MemberCount,
				Privacy = Party.PrivacySetting.Public
			};
			presence.Secrets = new Secrets()
			{
				JoinSecret = id
			};

			presence.Buttons = null;
		}
		client.SetPresence(presence);
	}

	public void UpdatePartySize(int size)
	{
		if (presence.Party != null)
		{
			presence.Party.Size = size; 
			client.SetPresence(presence);
		}
	}
	
	public void SetMenuPresence()
	{
		presence.State = "Navigating Menus";
		presence.Details = "";

		presence.Assets.LargeImageKey = "palogo";
		presence.Assets.LargeImageText = "Game Logo";
		presence.Timestamps = null;
		
		presence.Buttons = Buttons;
		
		//discord does not handle buttons and parties at the same time.
		presence.Party = null;
		presence.Secrets = null;
		client.SetPresence(presence);
	}
}