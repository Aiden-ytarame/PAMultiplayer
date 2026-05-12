using System;
using HarmonyLib;
using Newtonsoft.Json;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers;
using PAMultiplayer.UI;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(DebugController))]
public static class DebugControllerPatch
{
    [HarmonyPatch(nameof(DebugController.Awake))]
    [HarmonyPostfix]

    static void PostAwake(DebugController __instance)
    {
        DebugCommand pingCommand = new("Ping", "Prints current ping (multiplayer mod, any)",
            () =>
            {
                if (!GlobalsManager.IsMultiplayer)
                {
                    __instance.AddLog("-1 ms");
                    return;
                }
                
                __instance.AddLog($"{PaMNetworkManager.PamInstance.GetPing()} ms");
            });
        __instance.CommandList.Add(pingCommand);
        
        DebugCommand<int> leaderboardCommand = new("MPLeaderboard", "Top 10 leaderboard, page starts at 0 (multiplayer mod, any)", "int(page)",
            async void (page) =>
            {
                try
                {
                    var leaderboard = await SteamUserStats.FindOrCreateLeaderboardAsync("MP_MOD_Points",
                        LeaderboardSort.Descending,
                        LeaderboardDisplay.Numeric);

                    if (!leaderboard.HasValue)
                    {
                        __instance.AddLog($"Failed to get leaderboard");
                        return;
                    }

                    var top = await leaderboard.Value.GetScoresAsync(10, 1 + 10 * page);
                    if (top == null)
                    {
                        __instance.AddLog($"Failed to get leaderboard");
                        return;
                    }
                    
                    for (var i = 0; i < top.Length; i++)
                    {
                        var entry = top[i];
                        __instance.AddLog($"{entry.GlobalRank}. {entry.User.Name} // {entry.Score}");
                    }
                }
                catch (Exception e)
                {
                    PAM.Logger.LogError(e);
                }
            });
        __instance.CommandList.Add(leaderboardCommand);
        
        DebugCommand selfLeaderboardCommand = new("SelfMPLeaderboard", "your leaderboard (multiplayer mod, any)",
            async void () =>
            {
                try
                {
                    var leaderboard = await SteamUserStats.FindOrCreateLeaderboardAsync("MP_MOD_Points",
                        LeaderboardSort.Descending,
                        LeaderboardDisplay.Numeric);

                    if (!leaderboard.HasValue)
                    {
                        __instance.AddLog($"Failed to get leaderboard");
                        return;
                    }

                    var top = await leaderboard.Value.GetScoresAroundUserAsync(-5, 5);
                    if (top == null)
                    {
                        __instance.AddLog($"Failed to get leaderboard");
                        return;
                    }
                    
                    for (var i = 0; i < top.Length; i++)
                    {
                        var entry = top[i];
                        __instance.AddLog($"{entry.GlobalRank}. {entry.User.Name} // {entry.Score}");
                    }
                }
                catch (Exception e)
                {
                    PAM.Logger.LogError(e);
                }
            });
        __instance.CommandList.Add(selfLeaderboardCommand);
        
        DebugCommand killCommand = new("KillAll", "Kills all players (multiplayer mod, any)", 
            () =>
            {
                if (GlobalsManager.IsMultiplayer)
                {
                    if (GlobalsManager.IsHosting)
                    {
                        __instance.AddLog("Is host, Killing all players.");
                        
                        int index = 0;
        
                        if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) <= 1)
                        {
                            index = GameManager.Inst.currentCheckpointIndex;
                        }
                        
                        RewindHandler.CallRpc_Multi_RewindToCheckpoint(index);
                    }
                    else
                    {
                        __instance.AddLog("Is client, Killing self only.");
                        if (GlobalsManager.LocalPlayerObj.IsValidPlayer())
                        {
                            GlobalsManager.LocalPlayerObj.Health = 1;
                            GlobalsManager.LocalPlayerObj.PlayerHit();
                        }
                    }
                    return;
                }

                __instance.AddLog("Killing All Players.");
                foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                {
                    if (vgPlayerData.PlayerObject.IsValidPlayer())
                    {
                        vgPlayerData.PlayerObject.Health = 1;
                        vgPlayerData.PlayerObject.PlayerHit();
                    }
                }
            });
        __instance.CommandList.Add(killCommand);
        
        DebugCommand disconnectCommand = new("ForceDisconnect", "Disconnects you from the lobby (multiplayer mod, any)", 
            () =>
            {
                __instance.AddLog("Attempting to disconnect from the lobby.");
                GlobalsManager.LocalPlayerObjectId = 0;
                SteamManager.Inst.EndServer();
                SteamManager.Inst.EndClient();
                GlobalsManager.Players.Clear();
            });
        __instance.CommandList.Add(disconnectCommand);



        var chatAction = new Action<string>(
            message =>
            {
                __instance.OnToggleDebug();
                if (!GlobalsManager.IsMultiplayer)
                {
                    __instance.AddLog("Not currently in a lobby.");
                    return;
                }
                
                PAM.Logger.LogInfo($"Sending message: [{message}]");
                if (message.Length > 25)
                {
                    PAM.Logger.LogInfo("Message is longer than 25 characters. Cutting message.");
                    message = message.Substring(0, 25);
                }

                SteamLobbyManager.Inst.CurrentLobby.SendChatString(message);
            });
        
        DebugCommand<string> chatCommand = new("chat",
            "Makes your Nano say something. (multiplayer mod, any)",
            "string(message)",chatAction
            );
        __instance.CommandList.Add(chatCommand);
        
        DebugCommand<string> chatCommand2 = new("c",
            "Same as Chat (multiplayer mod, any)",
            "string(message)",chatAction
        );
        __instance.CommandList.Add(chatCommand2);
        
        DebugCommand<string> queueCommand = new("AddQueue",
            "Adds a level to the queue, the level has to be downloaded. (multiplayer mod, host)",
            "string(level_id)",
            levelId =>
            {
                if (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting)
                {
                    __instance.AddLog("You're not the host.");
                    return;
                }

                if (ArcadeLevelDataManager.Inst.GetLocalCustomLevel(levelId.ToString()))
                {
                    GlobalsManager.Queue.Insert(0, ArcadeManager.Inst.CurrentArcadeLevel.TrackName);
                    GlobalsManager.Queue.Add(levelId);
                    __instance.AddLog($"Adding level with id [{levelId}] to queue.");
                  
                    SteamLobbyManager.Inst.CurrentLobby.SetData("LevelQueue", JsonConvert.SerializeObject(GlobalsManager.GetQueueLevelNames()));
                    GlobalsManager.Queue.RemoveAt(0);
                    return;
                }
                __instance.AddLog($"Level with id [{levelId}] wasn't found downloaded.");
            });
        __instance.CommandList.Add(queueCommand);
        
        DebugCommand toggleTransparencyCommand = new("ToggleTransparency",
            "toggles Transparent Nanos. (multiplayer mod, any)",
            () =>
            {
                if (!GlobalsManager.IsMultiplayer)
                {
                    __instance.AddLog("Not in multiplayer, not toggling transparency.");
                    return;
                }

                bool isTransparent = !Settings.Transparent.Value;
                Settings.Transparent.Value = isTransparent;

                foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                {
                    if (!vgPlayerData.PlayerObject.IsValidPlayer() || vgPlayerData.PlayerObject.IsLocalPlayer())
                    {
                        continue;
                    }

                    foreach (var trail in vgPlayerData.PlayerObject.Player_Trail.Trail)
                    {
                        trail.Render_Trail.enabled = isTransparent;
                    }
                }

                __instance.AddLog($"Toggle Transparency to [{isTransparent}].");
            });
        __instance.CommandList.Add(toggleTransparencyCommand);
        
        DebugCommand toggleLinkedHealthCommand = new("ToggleLinked",
            "Toggles the modifier Linked Health. (multiplayer mod, host)",
            () =>
            {
                bool isLinked = !DataManager.inst.GetSettingBool("mp_linkedHealth", false);
                DataManager.inst.UpdateSettingBool("mp_linkedHealth", isLinked);
                    
                if (GlobalsManager.IsMultiplayer)
                {
                    SteamLobbyManager.Inst.CurrentLobby.SetData("LinkedMod", isLinked.ToString());
                    PaMNetworkManager.CallRpc_Multi_UpdateModifier(2, (byte)(isLinked ? 1 : 0));
                }
                __instance.AddLog($"Toggle Linked Health to [{isLinked}].");
            });
        __instance.CommandList.Add(toggleLinkedHealthCommand);
        
        DebugCommand playerListCommand = new("PlayerList",
            "shows all player ids. (multiplayer mod, any)",
            () =>
            {
                if (!GlobalsManager.IsMultiplayer)
                {
                    __instance.AddLog("Not in multiplayer.");
                    return;
                }

                __instance.AddLog("Showing all players.");
                if (GlobalsManager.IsHosting)
                {
                    foreach (var player in PaMNetworkManager.PamInstance.SteamIdToNetId)
                    {
                        __instance.AddLog($"ID [{player.Value}], Name [{GlobalsManager.Players[player.Key].Name}]");
                    }
                    return;
                }
                    
                foreach (var player in GlobalsManager.Players)
                {
                    __instance.AddLog(player.Value.Name);
                }
            });
        __instance.CommandList.Add(playerListCommand);
        
        DebugCommand<int> kickPlayerCommand = new("Kick",
            "attempts to kick a player from the lobby. (multiplayer mod, host)",
            "int(player_id)",
            playerId =>
            {
                if (!GlobalsManager.IsMultiplayer || !GlobalsManager.IsHosting)
                {
                    __instance.AddLog("Not in multiplayer or not the host. not kicking player.");
                    return;
                }
                    
                PaMNetworkManager.PamInstance.KickClient(playerId);
                __instance.AddLog("Attempting to kick player from server.");
            });
        __instance.CommandList.Add(kickPlayerCommand);
        
        DebugCommand<string> privateCommand = new("SetLobbyPrivacy",
            "set the lobby privacy setting. (multiplayer mod, host)",
            "bool(private)",
            isPrivateStr =>
            {
                if (!bool.TryParse(isPrivateStr.ToLower(), out bool isPrivate))
                {
                    __instance.AddLog("Invalid parameter, pass \"true\" or \"false\".");
                    return;
                }
                    
                if (!GlobalsManager.IsMultiplayer || !GlobalsManager.IsHosting)
                {
                    __instance.AddLog("Not in multiplayer or not the host.");
                    return;
                }

                if (isPrivate)
                {
                    SteamLobbyManager.Inst.CurrentLobby.SetFriendsOnly();
                    __instance.AddLog("Lobby was made private.");
                }
                else
                {
                    SteamLobbyManager.Inst.CurrentLobby.SetPublic();
                    __instance.AddLog("Lobby was made public.");
                }
                   
            });
        __instance.CommandList.Add(privateCommand);
        
        DebugCommand<int> lobbySizeCommand = new("SetLobbySize",
            "attempts to change the lobby size. (multiplayer mod, host) | 1 - 4 players. | 2 - 8 players. | 3 - 12 players. | 4 - 16 players.",
            "int(player_count)",
            playerCount =>
            {
                if (!GlobalsManager.IsMultiplayer || !GlobalsManager.IsHosting)
                {
                    __instance.AddLog("Not in multiplayer or not the host.");
                    return;
                }

                playerCount = Mathf.Clamp(playerCount * 4, 4, 16);

                if (SteamLobbyManager.Inst.CurrentLobby.MemberCount < playerCount)
                {
                    __instance.AddLog("Tried to set lobby size to less than the lobby player count.");
                }
                else
                {
                    SteamLobbyManager.Inst.CurrentLobby.MaxMembers = playerCount;
                    LobbyCreationManager.Instance.PlayerCount = playerCount;
                    __instance.AddLog($"Set lobby max players to [{playerCount}]");
                }
            });
        __instance.CommandList.Add(lobbySizeCommand);

        DebugCommand<string> testErrorCommand = new("DebugErrorScreen",
            "Creates a error screen with the desired message. (multiplayer mod, any)",
            "string(errorMessage)",
            ErrorScreen.CreateErrorScreen);
        __instance.CommandList.Add(testErrorCommand);

        DebugCommand<int> packetLossCommand = new("DebugPacketLoss",
            "sets packet loss 0-100. (multiplayer mod, any)",
            "int(loss%)",
            loss =>
            {
                SteamNetworkingUtils.FakeRecvPacketLoss = loss;
                SteamNetworkingUtils.FakeSendPacketLoss = loss;
            });
        __instance.CommandList.Add(packetLossCommand);
        
        DebugCommand<int> packetDelayCommand = new("DebugSetPacketDelay",
            "sets delay on packets. (multiplayer mod, any)",
            "int(ms delay)",
            ms =>
            {
                SteamNetworkingUtils.FakeRecvPacketLag = ms;
                SteamNetworkingUtils.FakeSendPacketLag = ms;
            });
        __instance.CommandList.Add(packetDelayCommand);
        
        
    }

    [HarmonyPatch(nameof(DebugController.HandleInput))]
    [HarmonyPrefix]
    static void PreHandleInput(ref string _input)
    {
        
        if (_input.Length >= 5 && _input.ToLower().Substring(0, 5) == "chat ")
        {
            _input = "chat " + _input.Substring(5).Replace(' ', '_');
        }
        
        if (_input.Length >= 2 && _input.ToLower().Substring(0, 2) == "c ")
        {
            _input = "c " + _input.Substring(2).Replace(' ', '_');
        }
    }
}