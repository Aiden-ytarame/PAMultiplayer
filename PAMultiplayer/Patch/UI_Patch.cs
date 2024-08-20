using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystems.SceneManagement;
using UnityEngine;
using TMPro;
using PAMultiplayer.Managers;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch
{
  
    /// <summary>
    /// adds the Multiplayer modifier to the UI
    /// </summary>
    [HarmonyPatch(typeof(ModifiersManager))]
    public static class UI_Patch
    {
        [HarmonyPatch(nameof(ModifiersManager.Start))]
        [HarmonyPostfix]
        static void AddUIToSettings(ref ModifiersManager __instance)
        {
            //modifier prefab
            Transform modifier = __instance.transform.GetChild(0).GetChild(0);

            Transform multiplayer = Object.Instantiate(modifier, __instance.transform);

            Object.Destroy(multiplayer.GetComponent<ToggleGroup>()); //don't remember why this here, might remove it

            var toggle = multiplayer.GetComponent<MultiElementToggle>();
            toggle.isOn = false;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            //Pidge removed these UI sounds in a recent update so ill removed them for now too
            //toggle.onValueChanged.AddListener(new System.Action<bool>(_ => {AudioManager.Inst.PlaySound("UI_Select", 1);})); 
            toggle.onValueChanged.AddListener(new Action<bool>(x =>
            {
                GlobalsManager.IsHosting = x;
                GlobalsManager.IsMultiplayer = x;
            }));
         
            //this is so the localization doesn't override the text
            multiplayer.GetComponent<GhostUIElement>().subGraphics = null;
            //sprite0 is the controller sprite
            multiplayer.GetComponentInChildren<TextMeshProUGUI>().text =
                "<size=85%><voffset=3><sprite=0><voffset=0><size=100%> Multiplayer ";
        }
    }

    [HarmonyPatch(typeof(PauseMenu))]
    public static class PauseMenuPatch
    {
        [HarmonyPatch(nameof(PauseMenu.RestartLevel))]
        [HarmonyPrefix]
        static bool PreRestartLevel()
        {
            return !GlobalsManager.IsMultiplayer;
        }
    }
    
      //the reason there's both unpause functions here, its cuz the UI unpause calls PauseMenu.UnPause() and pressing ESC calls GameManager.UnPause().
    [HarmonyPatch]
    public class PauseLobbyPatch
    {
        [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.UnPause))]
        [HarmonyPrefix]
        static bool PreMenuUnpause()
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            if (GlobalsManager.HasStarted || (GlobalsManager.IsHosting && SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return true;
            }

            return false;
        }

        public static IEnumerator ShowNames()
        {
            //stupid hack lmao
            yield return new WaitForUpdate();
            
            foreach (var currentLobbyMember in SteamLobbyManager.Inst.CurrentLobby.Members)
            {
                if (GlobalsManager.Players.TryGetValue(currentLobbyMember.Id, out var player))
                {
                    string text = "YOU";
                    if (currentLobbyMember.Id != GlobalsManager.LocalPlayer)
                    {
                        text = currentLobbyMember.Name;
                    }
                    
                    //band-aid fix for an error here
                    try
                    {
                        player.PlayerObject?.SpeechBubble?.DisplayText(text, 3);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
        }
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.UnPause))]
        [HarmonyPrefix]
        static bool PreGameUnpause(ref GameManager __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;
            
            if (!GlobalsManager.HasStarted && (!GlobalsManager.IsHosting || !SteamLobbyManager.Inst.IsEveryoneLoaded))
            {
                return false;
            }
            if (GlobalsManager.IsHosting)
            {
                SteamManager.Inst.Server.StartLevel();
            }

            if (LobbyScreenManager.Instance)
            {
                Plugin.Logger.LogError(GlobalsManager.Players.Count);
                Plugin.Logger.LogError(SteamLobbyManager.Inst.CurrentLobby.MemberCount);
                Plugin.Logger.LogError(VGPlayerManager.Inst.players.Count);
                
                VGPlayerManager.inst.RespawnPlayers();
                GameManager.Inst.StartCoroutine(ShowNames().WrapToIl2Cpp());
                Object.Destroy(LobbyScreenManager.Instance);
            }
            __instance.SetUIVolumeWeight(0.25f);
            return true;
        }
    }

    /// <summary>
    /// just having fun with loading screen Tips
    /// </summary>
    [HarmonyPatch(typeof(SceneLoader))]
    public static class LoadingTips
    {
        [HarmonyPatch(nameof(SceneLoader.Start))]
        [HarmonyPostfix]
        static void GetterTips(ref SceneLoader __instance)
        {
            List<string> newVals = new List<string>(__instance.Tips)
            {
                "You should try the log Fallen Kingdom!",
                "You can always call other Nanos for help!",
                "Git Gud",
                "I'm in your walls.",
                "Good Nano.",
                "No tips for you >:)",
                "Boykisser sent kisses!",
                "The developer wants me to say something here.",
                "I'm a furry. So what?",
                "Ready to be carried by another Nano again?",
                "You might be an Nano but you should hydrate anyways."
            };
            //thanks Pidge for making this public after I complained lol
            __instance.Tips = newVals.ToArray();
        }
    }
}
