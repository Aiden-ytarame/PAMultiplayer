using HarmonyLib;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(DebugController))]
public static class DebugControllerPatch
{
    [HarmonyPatch(nameof(DebugController.Awake))]
    [HarmonyPostfix]

    static void PostAwake(DebugController __instance)
    {
        DebugCommand killCommand = new("Kill_All", "Kills all players (multiplayer mod)", new System.Action(
            () =>
            {
                if (GlobalsManager.IsMultiplayer)
                {
                    if (GlobalsManager.IsHosting)
                    {
                        __instance.AddLog("Is host, Killing all players.");
                        SteamManager.Inst.Server?.SendRewindToCheckpoint();
                    }
                    else
                    {
                        __instance.AddLog("Is client, Killing self only.");
                        if (GlobalsManager.LocalPlayerObj && !GlobalsManager.LocalPlayerObj.isDead)
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
                    if (vgPlayerData.PlayerObject && !vgPlayerData.PlayerObject.isDead)
                    {
                        vgPlayerData.PlayerObject.Health = 1;
                        vgPlayerData.PlayerObject.PlayerHit();
                    }
                }
            }));
        __instance.CommandList.Add(killCommand);
        
        DebugCommand disconnectCommand = new("Force_Disconnect", "Disconnects you from the lobby (multiplayer mod)", new System.Action(
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
            "Makes your Nano say something. (multiplayer mod)",
            "string(message)",chatAction
            );
        __instance.CommandList.Add(chatCommand);
        
        DebugCommand<string> chatCommand2 = new("c",
            "Same as Chat (multiplayer mod)",
            "string(message)",chatAction
        );
        __instance.CommandList.Add(chatCommand2);
        
        DebugCommand<string> queueCommand = new("add_queue",
            "Adds a level to the queue, the level has to be downloaded. (multiplayer mod)",
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
                        return;
                    }
                    __instance.AddLog($"Level with id [{levelId}] wasn't found downloaded.");
                }));
        __instance.CommandList.Add(queueCommand);
    }

    [HarmonyPatch(nameof(DebugController.HandleInput))]
    [HarmonyPrefix]
    static void PreHandleInput(ref string _input)
    {
        if (_input.ToLower().Substring(0, 5) == "chat ")
        {
            _input = "chat " + _input.Substring(5).Replace(' ', '_');
        }
    }
}