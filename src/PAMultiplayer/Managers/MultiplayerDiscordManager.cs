using System;
using BepInEx;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using PAMultiplayer.UI;
using Steamworks;
using UnityEngine;
using EventType = DiscordRPC.EventType;

namespace PAMultiplayer.Managers;

/// <summary>
/// The discord manager the game uses is very finicky and broken so we use this instead.
/// </summary>
public class MultiplayerDiscordManager : MonoBehaviour
{
	public static MultiplayerDiscordManager Instance{get; private set;}

	public static bool IsInitialized => Instance && Instance._client != null && Instance._client.CurrentUser != null;

	private DiscordRpcClient _client;
	private RichPresence _presence;

	private const string ApplicationId = "1282511280833298483";

	private static readonly Button[] Buttons = new []
	{
		new Button() {Label = "Get the game!", Url = "steam://advertise/440310"},
		new Button() {Label = "Get the multiplayer mod!", Url = "https://github.com/Aiden-ytarame/PAMultiplayer"}
	};

	private void FixedUpdate()
	{
		if (_client != null)
		{
			_client.Invoke();
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

		_client = new DiscordRpcClient(
			ApplicationId,
			-1,
			new ConsoleLogger(LogLevel.Warning),
			false);
		
		_client.OnError += (_, e) => PAM.Logger.LogError($"An error occurred with Discord RPC Client: {e.Message} ({e.Code})");
		_client.OnReady += OnReady;

		_client.OnJoin += ClientOnOnJoin;
		_client.Subscribe(EventType.Join);
		_client.RegisterUriScheme("440310", Paths.ExecutablePath);

		_client.Initialize();
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
				ErrorScreen.CreateErrorScreen("Tried to join discord invite which didnt contain lobby data");

				PAM.Logger.LogError("Failed to parse secret.");
			}

			return;
		}
		ErrorScreen.CreateErrorScreen($"Failed to Join lobby from discord, Was Already In Lobby[{GlobalsManager.IsMultiplayer}], Steam Initialized [{SteamClient.IsValid}]");

		PAM.Logger.LogError("Failed to join lobby from discord, steam wasn't initialized or you're already in a lobby");
	}

	private void OnReady(object _, ReadyMessage __)
	{
		_presence = new RichPresence();
		_presence.Assets = new Assets()
		{
			SmallImageKey = "pamplogo2",
			SmallImageText = "Multiplayer Logo"
		};
		SetMenuPresence();
		_client.SetPresence(_presence);
	}

	public void SetLevelPresence(string state, string details, string levelCoverUrl)
	{
		try
		{
			_presence.State = state;
			_presence.Details = details;
			_presence.Assets.LargeImageKey = levelCoverUrl;
			_presence.Assets.LargeImageText = "Level Cover";
			_presence.Timestamps = new Timestamps(DateTime.UtcNow);
		
			if (GlobalsManager.IsMultiplayer)
			{
				string id = SteamLobbyManager.Inst.CurrentLobby.Id.ToString();
				_presence.Party = new Party()
				{
					ID = id + SteamLobbyManager.Inst.CurrentLobby.Owner.Id,
					Max = SteamLobbyManager.Inst.CurrentLobby.MaxMembers,
					Size = SteamLobbyManager.Inst.CurrentLobby.MemberCount,
					Privacy = Party.PrivacySetting.Public
				};
				_presence.Secrets = new Secrets()
				{
					Join = id
				};

				_presence.Buttons = null;
			}
			_client.SetPresence(_presence);
		}
		catch (Exception e)
		{
			PAM.Logger.LogError(e);
		}
	}

	public void UpdatePartySize(int size)
	{
		if (_presence.Party != null)
		{
			_presence.Party.Size = size; 
			_client.SetPresence(_presence);
		}
	}
	
	public void SetMenuPresence()
	{
		_presence.State = "Navigating Menus";
		_presence.Details = "";

		_presence.Assets.LargeImageKey = "palogo";
		_presence.Assets.LargeImageText = "Game Logo";
		_presence.Timestamps = null;
		
		_presence.Buttons = Buttons;
		
		//discord does not handle buttons and parties at the same time.
		_presence.Party = null;
		_presence.Secrets = null;
		_client.SetPresence(_presence);
	}

	public void SetChallengePresence()
	{
		if (_client == null || _presence == null)
		{
			return;
		}
		_presence.State = "Choosing Level";
		_presence.Details = "Playing Challenge";

		_presence.Assets.LargeImageKey = "palogo";
		_presence.Assets.LargeImageText = "Game Logo";
		_presence.Timestamps = null;
		_presence.Buttons = Buttons;
		_presence.Party = null;
		_presence.Secrets = null;
		
		if (GlobalsManager.IsMultiplayer)
		{
			string id = SteamLobbyManager.Inst.CurrentLobby.Id.ToString();
			_presence.Party = new Party()
			{
				ID = id + SteamLobbyManager.Inst.CurrentLobby.Owner.Id,
				Max = SteamLobbyManager.Inst.CurrentLobby.MaxMembers,
				Size = SteamLobbyManager.Inst.CurrentLobby.MemberCount,
				Privacy = Party.PrivacySetting.Public
			};
			_presence.Secrets = new Secrets()
			{
				Join = id
			};

			_presence.Buttons = null;
		}
		
		//discord does not handle buttons and parties at the same time.
		_client.SetPresence(_presence);
	}
}