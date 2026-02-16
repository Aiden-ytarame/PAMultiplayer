using System;
using BepInEx;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using EventType = DiscordRPC.EventType;

namespace PAMultiplayer.Managers;

/// <summary>
/// The discord manager the game uses is very finicky and broken so we use this instead.
/// </summary>
public class MultiplayerDiscordManager : MonoBehaviour
{
	public static MultiplayerDiscordManager Instance{get; private set;}

	public static bool IsInitialized => Instance && Instance.client != null && Instance.client.CurrentUser != null;

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
		
		client.OnError += (_, e) => PAM.Logger.LogError($"An error occurred with Discord RPC Client: {e.Message} ({e.Code})");
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
				PAM.Logger.LogInfo("Attempting to join lobby from discord invite.");
				
				GlobalsManager.IsHosting = false;
				GlobalsManager.IsMultiplayer = true;
				
				SteamMatchmaking.JoinLobbyAsync(id);
			}
			else
			{
				PAM.Logger.LogError("Failed to parse secret.");
			}

			return;
		}
		PAM.Logger.LogError("Failed to join lobby from discord, steam wasn't initialized or you're already in a lobby");
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
		try
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
					Max = SteamLobbyManager.Inst.CurrentLobby.MaxMembers,
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
		catch (Exception e)
		{
			PAM.Logger.LogError(e);
		}
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

	public void SetChallengePresence()
	{
		if (client == null || presence == null)
		{
			return;
		}
		presence.State = "Choosing Level";
		presence.Details = "Playing Challenge";

		presence.Assets.LargeImageKey = "palogo";
		presence.Assets.LargeImageText = "Game Logo";
		presence.Timestamps = null;
		presence.Buttons = Buttons;
		presence.Party = null;
		presence.Secrets = null;
		
		if (GlobalsManager.IsMultiplayer)
		{
			string id = SteamLobbyManager.Inst.CurrentLobby.Id.ToString();
			presence.Party = new Party()
			{
				ID = id + SteamLobbyManager.Inst.CurrentLobby.Owner.Id,
				Max = SteamLobbyManager.Inst.CurrentLobby.MaxMembers,
				Size = SteamLobbyManager.Inst.CurrentLobby.MemberCount,
				Privacy = Party.PrivacySetting.Public
			};
			presence.Secrets = new Secrets()
			{
				JoinSecret = id
			};

			presence.Buttons = null;
		}
		
		//discord does not handle buttons and parties at the same time.
		client.SetPresence(presence);
	}
}