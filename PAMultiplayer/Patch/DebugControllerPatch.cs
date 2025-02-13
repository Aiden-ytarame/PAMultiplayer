using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json;
using PAMultiplayer.Managers;
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
        DebugCommand killCommand = new("Kill_All", "Kills all players (multiplayer mod, any)", new System.Action(
            () =>
            {
                if (GlobalsManager.IsMultiplayer)
                {
                    if (GlobalsManager.IsHosting)
                    {
                        __instance.AddLog("Is host, Killing all players.");
                        
                        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                        {
                            if (vgPlayerData.PlayerObject.IsValidPlayer())
                            {
                                vgPlayerData.PlayerObject.Health = 0;
                                vgPlayerData.PlayerObject.ClearEvents();
                                vgPlayerData.PlayerObject.PlayerDeath();
                            }
                        }
                        int index = 0;
        
                        if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) <= 1)
                        {
                            index = GameManager.Inst.currentCheckpointIndex;
                        }
                        
                        GameManager.Inst.RewindToCheckpoint(index);
                        SteamManager.Inst.Server?.SendRewindToCheckpoint(index);
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
            }));
        __instance.CommandList.Add(killCommand);
        
        DebugCommand disconnectCommand = new("Force_Disconnect", "Disconnects you from the lobby (multiplayer mod, any)", new System.Action(
            () =>
            {
                __instance.AddLog("Attempting to disconnect from the lobby.");
                GlobalsManager.LocalPlayerObjectId = 0;
                SteamManager.Inst.EndServer();
                SteamManager.Inst.EndClient();
                GlobalsManager.Players.Clear();
            }));
        __instance.CommandList.Add(disconnectCommand);



        var chatAction = new System.Action<string>(
            message =>
            {
                __instance.OnToggleDebug();
                if (!GlobalsManager.IsMultiplayer)
                {
                    __instance.AddLog("Not currently in a lobby.");
                    return;
                }

                __instance.AddLog($"Sending message: [{message}]");
                if (message.Length > 25)
                {
                    __instance.AddLog("Message is longer than 25 characters. Cutting message.");
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
        
        DebugCommand<string> queueCommand = new("add_queue",
            "Adds a level to the queue, the level has to be downloaded. (multiplayer mod, host)",
            "string(level_id)",
            new System.Action<string>(
                levelId =>
                {
                    if (GlobalsManager.IsMultiplayer && !GlobalsManager.IsHosting)
                    {
                        __instance.AddLog("You're not the host.");
                        return;
                    }

                    if (ArcadeLevelDataManager.Inst.GetLocalCustomLevel(levelId.ToString()))
                    {
                        GlobalsManager.Queue.Add(levelId);
                        __instance.AddLog($"Adding level with id [{levelId}] to queue.");
                        
                        List<string> levelNames = new();
                        levelNames.Add(ArcadeManager.Inst.CurrentArcadeLevel.name);
                        
                        foreach (var id in GlobalsManager.Queue)
                        {
                            VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(id);
                            levelNames.Add(level.TrackName);
                        }
                        SteamLobbyManager.Inst.CurrentLobby.SetData("LevelQueue", JsonConvert.SerializeObject(levelNames));
                        
                        return;
                    }
                    __instance.AddLog($"Level with id [{levelId}] wasn't found downloaded.");
                }));
        __instance.CommandList.Add(queueCommand);
        
        DebugCommand toggleTransparencyCommand = new("toggle_transparency",
            "toggles Transparent Nanos. (multiplayer mod, any)",
            new System.Action(
                () =>
                {
                    if (!GlobalsManager.IsMultiplayer)
                    {
                        __instance.AddLog("Not in multiplayer, not toggling transparency.");
                        return;
                    }

                    bool isTransparent = !DataManager.inst.GetSettingBool("MpTransparentPlayer", false);
                    DataManager.inst.UpdateSettingBool("MpTransparentPlayer", isTransparent);

                    foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                    {
                        if (!vgPlayerData.PlayerObject.IsValidPlayer() || vgPlayerData.PlayerObject.IsLocalPlayer())
                        {
                            continue;
                        }

                        foreach (var trail in vgPlayerData.PlayerObject.Player_Trail.trail)
                        {
                            trail.GetComponentInChildren<TrailRenderer>().enabled = isTransparent;
                        }
                    }

                    __instance.AddLog($"Toggle Transparency to [{isTransparent}].");
                }));
        __instance.CommandList.Add(toggleTransparencyCommand);
        
        DebugCommand toggleLinkedHealthCommand = new("toggle_linked",
            "Toggles the modifier Linked Health. (multiplayer mod, host)",
            new System.Action(
                () =>
                {
                    bool isLinked = !DataManager.inst.GetSettingBool("mp_linkedHealth", false);
                    DataManager.inst.UpdateSettingBool("mp_linkedHealth", isLinked);
                    
                    if (GlobalsManager.IsMultiplayer)
                    {
                        SteamLobbyManager.Inst.CurrentLobby.SetData("LinkedMod", isLinked.ToString());
                    }
                    __instance.AddLog($"Toggle Linked Health to [{isLinked}].");
                }));
        __instance.CommandList.Add(toggleLinkedHealthCommand);
        
        DebugCommand playerListCommand = new("player_list",
            "shows all player ids. (multiplayer mod, any)",
            new System.Action(
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
                        foreach (var player in SteamManager.Inst.Server.ConnectionWrappers)
                        {
                            __instance.AddLog($"ID [{player.Value}], Name [{GlobalsManager.Players[player.Key.ConnectionId].Name}]");
                        }
                        return;
                    }
                    
                    foreach (var player in GlobalsManager.Players)
                    {
                        __instance.AddLog(player.Value.Name);
                    }
                }));
        __instance.CommandList.Add(playerListCommand);
        
        DebugCommand<int> kickPlayerCommand = new("kick",
            "attempts to kick a player from the lobby. (multiplayer mod, host)",
            "int(player_id)",
            new System.Action<int>(
                playerId =>
                {
                    if (!GlobalsManager.IsMultiplayer || !GlobalsManager.IsHosting)
                    {
                        __instance.AddLog("Not in multiplayer or not the host. not kicking player.");
                        return;
                    }


                    if (SteamManager.Inst.Server.TryToKickPlayer(playerId))
                    {
                        __instance.AddLog("Kicked player from server.");
                        return;
                    }

                    __instance.AddLog("Failed Kicked player from server.");
                }));
        __instance.CommandList.Add(kickPlayerCommand);
    
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