﻿using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using PAMultiplayer;
using PAMultiplayer.Managers;
using Rewired;
using UnityEngine;

namespace PAMultiplayer.Patch
{
    [HarmonyPatch(typeof(VGPlayer))]
    public class Player_Patch
    {
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerEnter))]
        [HarmonyPatch(nameof(VGPlayer.OnChildTriggerStay))]
        [HarmonyPrefix]
        static bool PreCollision(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            if (__instance.IsLocalPlayer())
                return true;
            
            return false; //only collide if is local player
        }


        public static bool IsDamageAll = false;
        [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
        [HarmonyPrefix]
        static bool Hit_Pre(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer)
            {
                if (DataManager.inst.GetSettingBool("mp_linkedHealth", false))
                {
                    if (GameManager.Inst.Paused)
                        return false;
                    
                    if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) == 1)
                        return false;
                    
                    
                    foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                    {
                        if (vgPlayerData.PlayerObject.IsValidPlayer() && vgPlayerData.PlayerObject != __instance)
                        {
                            vgPlayerData.PlayerObject.PlayerHit();
                        }
                    }
                }
                return true;
            }

            if (GameManager.Inst.Paused)
                return false;
            

            if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) == 1)
                return false;
            
            
            VGPlayer player = __instance;
            bool isLocal = player.IsLocalPlayer();

            //hit is valid
            if (isLocal)
            {
                if (GlobalsManager.IsHosting)
                {
                    if (DataManager.inst.GetSettingBool("mp_linkedHealth", false))
                    {
                        if (!IsDamageAll)
                        {
                            SteamManager.Inst.Server.SendDamageAll(player.Health, GlobalsManager.LocalPlayerId);
                            return false;
                        }

                    }
                    else
                        SteamManager.Inst.Server.SendHostDamage();
                }
                else
                {
                    if (!DataManager.inst.GetSettingBool("mp_linkedHealth", false) || !IsDamageAll)
                        SteamManager.Inst.Client.SendDamage(player.Health);
                }

                IsDamageAll = false;
            }
            
            
            --player.Health;
            player.StopCoroutine("RegisterCloseCall");
            
            if (player.DeathEvent != null && player.Health <= 0)
                player.DeathEvent.Invoke(player.Player_Wrapper.position);
            
            else if (player.HitEvent != null)
                player.HitEvent.Invoke(player.Health, player.Player_Wrapper.position);

            if (player.Health > 0)
            {
                int warp = DataManager.inst.GetSettingInt("MpPlayerWarpSFX", 0);
                if (warp == 0 || (warp == 1 && player.IsLocalPlayer()))
                {
                    AudioManager.Inst.ApplyLowPass(0.05f, 0.8f, 1.5f);
                    AudioManager.Inst.ApplyLowPassResonance(0, 0.6f, 0.2f);
                }

                int hit = DataManager.inst.GetSettingInt("MpPlayerSFX", 0);
                if (hit == 0 || (hit == 1 && player.IsLocalPlayer()))
                {
                    AudioManager.Inst.PlaySound("HurtPlayer", 0.6f);
                }
                
                player.StartHurtDecay();
             
                if(isLocal)
                    player.PlayerHitAnimation(); //this runs the camera shake, annoying in multiplayer
                else
                    player.PlayParticles(VGPlayer.ParticleTypes.Hit);
            }
            else
            {
                player.PlayerDeath();
                
                if(isLocal)
                    player.PlayerDeathAnimation(); //this runs the camera shake, annoying in multiplayer
                else
                    player.PlayParticles(VGPlayer.ParticleTypes.Die);
            }

            return false;
        }

        /// <summary>
        /// this gives a null exception if ran without a valid playerId
        /// or that's my guess lol, whatever it is this fixes it
        /// </summary>
      
        [HarmonyPatch(nameof(VGPlayer.HandleBoost))]
        [HarmonyPrefix]
        static bool Boost_Pre(ref VGPlayer __instance)
        {
            if (!GlobalsManager.IsMultiplayer) return true;
            
            return __instance.IsLocalPlayer();
        }
        
        [HarmonyPatch(nameof(VGPlayer.PlayParticles))]
        [HarmonyPostfix]
        static void BoostParticle_Post(ref VGPlayer __instance, VGPlayer.ParticleTypes _type)
        {
            if (!GlobalsManager.IsMultiplayer) return;
            
            if(_type != VGPlayer.ParticleTypes.Boost || !__instance.IsLocalPlayer()) return;

            if (GlobalsManager.IsHosting)
            {
                SteamManager.Inst.Server.SendHostBoost();
            }
            else
            {
                SteamManager.Inst.Client.SendBoost();
            }
        }

        
        //for changing the player's shape.
        public static Mesh CircleMesh;
        public static Mesh ArrowMesh;
        public static Mesh TriangleMesh;
        
        /// <summary>
        /// changes the nano's shape if there's more than 4 players
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.Init), new Type[] { })]
        [HarmonyPostfix]
        static void PostSpawn(ref VGPlayer __instance)
        {
            void SetPlayerMesh(VGPlayer player, Mesh mesh)
            {
                Transform playerWrapper = player.Player_Wrapper.transform;
                
                playerWrapper.Find("core").GetComponent<MeshFilter>().mesh = mesh;
                playerWrapper.Find("zen-marker").GetComponent<MeshFilter>().mesh = mesh; //is this needed?
                playerWrapper.Find("boost").GetComponent<MeshFilter>().mesh = mesh;
            }
            
            if (!GlobalsManager.IsMultiplayer) return;

            if (DataManager.inst.GetSettingBool("MpTransparentPlayer", false) && !__instance.IsLocalPlayer())
            {
                foreach (var trail in __instance.Player_Trail.trail)
                {
                    trail.GetComponentInChildren<TrailRenderer>().enabled = false;
                }
            }
         
            if (__instance.PlayerID < 4)
            {
                return;
            }

            if (__instance.PlayerID < 8)
            {
                SetPlayerMesh(__instance, CircleMesh);
            }
            else if (__instance.PlayerID < 12)
            {
                SetPlayerMesh(__instance, TriangleMesh);
                
                Vector3 offsetRot = new Vector3(0, 0, -90);
                Transform player = __instance.Player_Wrapper.transform;
                
                player.Find("core").Rotate(offsetRot);
                player.Find("zen-marker").Rotate(offsetRot);
                player.transform.Find("boost").Rotate(offsetRot);
              
            }
            else
            {
                SetPlayerMesh(__instance, ArrowMesh);
                Transform player = __instance.Player_Wrapper.transform;

                Vector3 newScale = new Vector3(2, 2, 1);

                player.Find("core").localScale = newScale;
                player.Find("zen-marker").localScale = newScale;
                player.transform.Find("boost").localScale = newScale;
            }
        }

        [HarmonyPatch(nameof(VGPlayer.SetColor))]
        [HarmonyPrefix]
        static void PreSetColor(VGPlayer __instance, ref Color _col, ref Color _colTail)
        {
            if (!GlobalsManager.IsMultiplayer || !DataManager.inst.GetSettingBool("MpTransparentPlayer", false) || __instance.IsLocalPlayer()) return;

            float alpha;
            switch (DataManager.inst.GetSettingInt("MpTransparentPlayerAlpha", 0))
            {
                case 1:
                    alpha = 0.50f;
                    break;
                case 2:
                    alpha = 0.85f;
                    break;
                default:
                    alpha = 0.35f;
                    break;
            }
            
            _col.a = alpha;
            _colTail.a = alpha;
        }
        
        /// <summary>
        /// this returns the player controller depending on your playerId
        /// the controller 0 is the one LocalPlayer controls
        /// we force this to return controller 0 if the playerId is the one the client is supposed to control
        /// </summary>
        [HarmonyPatch(nameof(VGPlayer.RPlayer), MethodType.Getter)]
        [HarmonyPrefix]
        static bool RPlayerGetter(ref VGPlayer __instance, ref Rewired.Player __result)
        {
            if (!GlobalsManager.IsMultiplayer) return true;

            __result = ReInput.players.GetPlayer(__instance.IsLocalPlayer() ? 0 : 1);

            return false;
        }

    }
}
[HarmonyPatch(typeof(VGPlayerManager))]
public static class PlayerManagerPatch
{
    [HarmonyPatch(nameof(VGPlayerManager.OnControllerConnected))]
    [HarmonyPrefix]
    static bool PreConnected(ControllerStatusChangedEventArgs args)
    {
        return !GlobalsManager.IsMultiplayer;
    }
    [HarmonyPatch(nameof(VGPlayerManager.OnControllerDisconnected))]
    [HarmonyPrefix]
    static bool PreDisConnected()
    {
        return !GlobalsManager.IsMultiplayer;
    }
}
[HarmonyPatch(typeof(DataManager.BeatmapTheme))]
public static class BeatmapThemePatch
{
    /// <summary>
    /// Normally every player after the 4th has the boost color of the 4th player, this is so instead it has the correct color.
    /// </summary>
    [HarmonyPatch(nameof(DataManager.BeatmapTheme.GetPlayerColor))]
    [HarmonyPrefix]
    static bool PreGetPlayerColor(DataManager.BeatmapTheme __instance, ref Color __result, int _val)
    {
        __result = __instance.playerColors[_val % 4];
        return false;
    }
}

static class PlayerExtensions
{
    public static bool IsLocalPlayer(this VGPlayer player)
    {
        return player.PlayerID == GlobalsManager.LocalPlayerObjectId;
    }
    
    public static bool IsValidPlayer(this VGPlayer player)
    {
        return player != null && !player.isDead;
    }
}