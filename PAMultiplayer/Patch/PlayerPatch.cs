﻿using System;
using HarmonyLib;
using PAMultiplayer.Managers;
using Rewired;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch;

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
    [HarmonyPrefix]
    static bool BoostParticle_Post(ref VGPlayer __instance, VGPlayer.ParticleTypes _type)
    {
        ParticleSystem ps = null;
        Quaternion rot = Quaternion.Euler(0, 0, __instance.Player_Wrapper.rotation.eulerAngles.z - 90);

        DataManager.BeatmapTheme beatmapTheme = GameManager.Inst
            ? GameManager.Inst.LiveTheme
            : ChallengeManager.Inst.ChallengeTheme;
        switch (_type)
        {
            case VGPlayer.ParticleTypes.Spawn:
            {
                ps = Object.Instantiate(__instance.PS_Spawn, __instance.Player_Wrapper.position, rot);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.GetPlayerColor(__instance.PlayerID));
            }
                break;
            case VGPlayer.ParticleTypes.Boost:
            {
                if (GlobalsManager.IsMultiplayer && __instance.IsLocalPlayer())
                {
                    if (GlobalsManager.IsHosting)
                    {
                        SteamManager.Inst.Server.SendHostBoost();
                    }
                    else
                    {
                        SteamManager.Inst.Client.SendBoost();
                    }
                }
                ps = Object.Instantiate(__instance.PS_Boost, __instance.Player_Wrapper.position, rot);

                ps.transform.SetParent(__instance.Player_Wrapper);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.GetPlayerColor(__instance.PlayerID));
            }
                break;
            case VGPlayer.ParticleTypes.Hit:
            {
                ps = Object.Instantiate(__instance.PS_Hit, __instance.Player_Wrapper.position, rot);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.guiAccent);
            }
                break;
            case VGPlayer.ParticleTypes.Die:
            {
                ps = Object.Instantiate(__instance.PS_Die, __instance.Player_Wrapper.position, rot);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.guiAccent);
            }
                break;
        }

        if (ps != null)
        {
            ps.Play();
            SystemManager.inst.StartCoroutine(__instance.KillParticleSystem(ps));
        }

        return false;
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
            foreach (var trail in __instance.Player_Trail.Trail)
            {
                trail.Render_Trail.enabled = false;
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
    static bool PreSetColor(VGPlayer __instance, Color _col, Color _colTail)
    {
        if (GlobalsManager.IsMultiplayer && DataManager.inst.GetSettingBool("MpTransparentPlayer", false) &&
            !__instance.IsLocalPlayer())
        {
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
        int colorId = GameManager.Inst ? GameManager.Inst.ColorID : ChallengeManager.Inst.ColorID;
         
        __instance.Player_Core_Rend.material.SetColor(colorId, _col);
        __instance.Player_Boost_Rend.material.SetColor(colorId, _colTail);

        foreach (var obj in __instance.Player_Trail.Trail)
        {
            if (obj == null) continue;

            obj.Render_Mesh.material.color = _colTail;
            obj.Render_Trail.startColor = _colTail;
            obj.Render_Trail.endColor = VGFunctions.LSColors.fadeColor(_colTail, 0);
            if (obj.ParticleSystem)
            {
                ParticleSystem.MainModule trailSettings = obj.ParticleSystem.main;
                trailSettings.startColor = new ParticleSystem.MinMaxGradient(_colTail);
            }
        }
        return false;
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


[HarmonyPatch(typeof(VGPlayerManager.VGPlayerData))]
public static class PlayerDataPatch
{
    [HarmonyPatch(nameof(VGPlayerManager.VGPlayerData.SpawnPlayerObject))]
    [HarmonyPrefix]
    static bool PreSpawn(VGPlayerManager.VGPlayerData __instance, Vector3 _pos, ref VGPlayer __result)
    {
        if (GameManager.Inst) //this means the player is being spawned in the challenge scene, which does not contain game manager
            return true;
        
        if(!ChallengeManager.Inst || __instance.PlayerObject)
            return false;
        
        
        string name = "player" + __instance.PlayerID;
        var player = Object.Instantiate(VGPlayerManager.Inst.PlayerPrefab, ChallengeManager.Inst.playersParent).GetComponent<VGPlayer>();
        player.name = name;
        player.transform.position = _pos;
        player.Init(__instance.PlayerID, CameraDB.Inst.foregroundCamera);
        
        __instance.PlayerObject = player;
        __result = player;
        return false;
    }
   
}

[HarmonyPatch(typeof(MeshDeformation))]
public static class MeshDeformationPatch
{
    [HarmonyPatch(nameof(MeshDeformation.LateUpdate))]
    [HarmonyPrefix]
    static bool PreLateUpdate(ref MeshDeformation __instance)
    {
        float adjustedSharpness = 1f - Mathf.Pow(1f - 0.1f, Time.deltaTime * 60);
        
        for (int i = 0; i < __instance.originalVertices.Length; i++)
        {
            var tmp = __instance.tmpVerticies[i];
            tmp.x =  __instance.tmpVerticies[i].x + ( __instance.newVertices[i].x -  __instance.tmpVerticies[i].x) * adjustedSharpness;
            tmp.y =  __instance.tmpVerticies[i].y + ( __instance.newVertices[i].y -  __instance.tmpVerticies[i].y) * adjustedSharpness;
            tmp.z =  __instance.tmpVerticies[i].z + ( __instance.newVertices[i].z -  __instance.tmpVerticies[i].z) * adjustedSharpness;
            __instance.tmpVerticies[i] = tmp;
        }

        __instance.mesh.vertices =  __instance.tmpVerticies;
        __instance.mesh.RecalculateBounds();
        return false;
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