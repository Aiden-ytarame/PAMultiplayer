using System;
using HarmonyLib;
using AttributeNetworkWrapperV2;
using PaApi;
using PAMultiplayer.AttributeNetworkWrapperOverrides;
using PAMultiplayer.Managers;
using Rewired;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PAMultiplayer.Patch;

[HarmonyPatch(typeof(VGPlayer))]
public partial class Player_Patch
{
    
    [HarmonyPatch(nameof(VGPlayer.CheckForObjectCollision))]
    [HarmonyPrefix]
    static bool PreCollision(VGPlayer __instance, Collider2D _other, ref bool __result)
    {
        if (!GlobalsManager.IsMultiplayer)
        {
            return true;
        }

        if (!__instance.IsLocalPlayer())
        {
            __result = false;
            return false;
        }

        return true; //only collide if is local player
    }


    public static bool IsDamageAll = false;
    [HarmonyPatch(nameof(VGPlayer.PlayerHit))]
    [HarmonyPrefix]
    static bool Hit_Pre(ref VGPlayer __instance)
    {
        if (!SingletonBase<GameManager>.Inst.IsArcade || GameManager.Inst.IsEditor)
        {
            return true;
        }
        
        if (GameManager.Inst.Paused)
            return false;
            

        if (DataManager.inst.GetSettingEnum("ArcadeHealthMod", 0) == 1)
            return false;
            
            
        VGPlayer player = __instance;
        bool isLocal = !GlobalsManager.IsMultiplayer || player.IsLocalPlayer();

        //hit is valid
        if (isLocal)
        {
            bool linked = DataManager.inst.GetSettingBool("mp_linkedHealth", false);
            if (GlobalsManager.IsMultiplayer)
            {
                if (GlobalsManager.IsHosting)
                {
                    if (linked)
                    {
                        if (!IsDamageAll)
                        {
                            CallRpc_Multi_DamageAll(player.Health, GlobalsManager.LocalPlayerId);
                            return false;
                        }
                    }
                    else
                        CallRpc_Multi_PlayerDamaged(GlobalsManager.LocalPlayerId, player.Health);
                    
                }
                else
                {
                    if (!linked || !IsDamageAll)
                        CallRpc_Server_PlayerDamaged(player.Health);
                }

                IsDamageAll = false;
                --player.Health;
            }
            else // not multiplayer
            {
                --player.Health;
                if (linked)
                {
                    foreach (var vgPlayerData in VGPlayerManager.Inst.players)
                    {
                        if (vgPlayerData.PlayerObject.IsValidPlayer() && vgPlayerData.PlayerObject != __instance && vgPlayerData.PlayerObject.Health > player.Health)
                        {
                            vgPlayerData.PlayerObject.PlayerHit();
                        }
                    }
                }
            }
        }
        else
        {
            --player.Health;
        }
       
        player.StopCoroutine("RegisterCloseCall");
        
        if (player.GetDeathEvent() != null && player.Health <= 0)
        {
            player.GetDeathEvent().Invoke(player.Player_Wrapper.position);
        }
        else if (player.GetHitEvent() != null)
        {
            player.GetHitEvent().Invoke(player.Health, player.Player_Wrapper.position);
        }
        
        if (player.Health > 0)
        {
            player.StartHurtDecay();

            int warp = Settings.WarpSfx.Value; 
            if (warp == 0 || (warp == 1 && player.IsLocalPlayer()))
            {
                AudioManager.Inst.ApplyLowPass(0.05f, 0.8f, 1.5f);
                AudioManager.Inst.ApplyLowPassResonance(0, 0.6f, 0.2f);
            }

            int hit = Settings.HitSfx.Value;
            if (hit == 0 || (hit == 1 && player.IsLocalPlayer()))
            {
                AudioManager.Inst.PlaySound("HurtPlayer", 0.6f);
            }
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

    [ServerRpc]
    private static void Server_PlayerDamaged(ClientNetworkConnection conn, int healthPreHit)
    {
        if(!conn.TryGetSteamId(out SteamId steamID))
        {
            return;
        }
        
        if (DataManager.inst.GetSettingBool("mp_linkedHealth", false))
        {
            if (GlobalsManager.LocalPlayerObj.Health >= healthPreHit)
            {
                CallRpc_Multi_DamageAll(healthPreHit, steamID);
            }
            return;
        }
        
        CallRpc_Multi_PlayerDamaged(steamID, healthPreHit);
    }
    
    [MultiRpc]
    private static void Multi_PlayerDamaged(SteamId steamID, int healthPreHit)
    {
        if (healthPreHit == 1)
        {
            PointsManager.Inst?.PlayerHasDied(steamID);
        }
        
        PAM.Logger.LogDebug($"Damaging player {steamID}");
        
        if (steamID.IsLocalPlayer()) return;
       
        if(GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (!player.VGPlayerData.PlayerObject.IsValidPlayer()) return;
            player.VGPlayerData.PlayerObject.Health = healthPreHit;
            player.VGPlayerData.PlayerObject.PlayerHit();
        }
    }

    [MultiRpc]
    private static void Multi_DamageAll(int healthPreHit, SteamId playerHit)
    {
        DamageAll(healthPreHit, playerHit);
    }
    static void DamageAll(int healthPreHit, ulong hitPlayerId)
    {
        if (Settings.Linked.Value)
        {
            if (GlobalsManager.Players.TryGetValue(hitPlayerId, out var playerData))
            {
                string hex = VGPlayerManager.Inst.GetPlayerColorHex(playerData.VGPlayerData.PlayerID);
                VGPlayerManager.Inst.DisplayNotification($"Nano [<color=#{hex}>{playerData.Name}</color>] got hit!", 1f);
            }
        }
       
        
        foreach (var vgPlayerData in GlobalsManager.Players)
        {
            VGPlayer player = vgPlayerData.Value.VGPlayerData.PlayerObject;
            if (player.IsValidPlayer())
            {
                if (player.Health < healthPreHit)
                {
                    PAM.Logger.LogWarning($"Old message");
                    continue;
                }

                if (player.IsLocalPlayer())
                {
                    IsDamageAll = true;
                }
                
                player.Health = healthPreHit;
                player.PlayerHit();
            }
        }
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
                break;
            }
            case VGPlayer.ParticleTypes.Boost:
            {
                if (GlobalsManager.IsMultiplayer && __instance.IsLocalPlayer())
                {
                    if (GlobalsManager.IsHosting)
                    {
                        CallRpc_Multi_PlayerBoost(GlobalsManager.LocalPlayerId);
                    }
                    else
                    {
                        CallRpc_Server_PlayerBoost();
                    }
                }
                ps = Object.Instantiate(__instance.PS_Boost, __instance.Player_Wrapper.position, rot);

                ps.transform.SetParent(__instance.Player_Wrapper);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.GetPlayerColor(__instance.PlayerID));
                break;
            }
            case VGPlayer.ParticleTypes.Hit:
            {
                ps = Object.Instantiate(__instance.PS_Hit, __instance.Player_Wrapper.position, rot);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.guiAccent);
                break;
            }
            case VGPlayer.ParticleTypes.Die:
            {
                ps = Object.Instantiate(__instance.PS_Die, __instance.Player_Wrapper.position, rot);

                ParticleSystem.MainModule settings = ps.main;
                settings.startColor = new ParticleSystem.MinMaxGradient(beatmapTheme.guiAccent);
                break;
            }
        }

        if (ps)
        {
            ps.Play();
            SystemManager.inst.StartCoroutine(__instance.KillParticleSystem(ps));
        }

        return false;
    }

    [ServerRpc(SendType.Unreliable)]
    public static void Server_PlayerBoost(ClientNetworkConnection conn)
    {
        if (!conn.TryGetSteamId(out SteamId steamID))
        {
            return;
        }
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (player.VGPlayerData.PlayerObject && !player.VGPlayerData.PlayerObject.isDead)
            {
                player.VGPlayerData.PlayerObject.PlayParticles(VGPlayer.ParticleTypes.Boost);
            }
        }
        
        CallRpc_Multi_PlayerBoost(steamID);
    }
      
    [MultiRpc(SendType.Unreliable)]
    public static void Multi_PlayerBoost(SteamId steamID)
    {
        if (steamID.IsLocalPlayer())
        {
            return;
        }
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (player.VGPlayerData.PlayerObject && !player.VGPlayerData.PlayerObject.isDead)
            {
                player.VGPlayerData.PlayerObject.PlayParticles(VGPlayer.ParticleTypes.Boost);
            }
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
        if (__instance.RPlayer.id == 0 && PointsManager.Inst)
        {
            __instance.CloseCallEvent += _ => PointsManager.Inst.AddCloseCall();
            __instance.HitEvent += (_, _) => PointsManager.Inst.AddHit();
            __instance.DeathEvent += _ => PointsManager.Inst.AddDeath();
            _holdingBoost = 0;
        }
        
        void SetPlayerMesh(VGPlayer player, Mesh mesh)
        {
            Transform playerWrapper = player.transform.GetChild(2);
            
            playerWrapper.Find("core").GetComponent<MeshFilter>().mesh = mesh;
            playerWrapper.Find("zen-marker").GetComponent<MeshFilter>().mesh = mesh; //is this needed?
            playerWrapper.Find("boost").GetComponent<MeshFilter>().mesh = mesh;
        }

        if (!GlobalsManager.IsMultiplayer)
        {
            return;
        }

        if (!__instance.IsLocalPlayer())
        {
            __instance.Player_Rigidbody.simulated = false;
            
            if (Settings.Transparent.Value)
            {
                foreach (var trail in __instance.Player_Trail.Trail)
                {
                    trail.Render_Trail.enabled = false;
                }
            }
        }
        
        switch (__instance.PlayerID)
        {
            case < 4:
                return;
            case < 8:
                SetPlayerMesh(__instance, CircleMesh);
                break;
            case < 12:
            {
                SetPlayerMesh(__instance, TriangleMesh);
                
                Vector3 offsetRot = new Vector3(0, 0, -90);
                Transform player = __instance.Player_Wrapper.transform;
                
                player.Find("core").Rotate(offsetRot);
                player.Find("zen-marker").Rotate(offsetRot);
                player.transform.Find("boost").Rotate(offsetRot);
                break;
            }
            default: //id > 12
            {
                SetPlayerMesh(__instance, ArrowMesh);
                Transform player = __instance.Player_Wrapper.transform;

                Vector3 newScale = new Vector3(2, 2, 1);

                player.Find("core").localScale = newScale;
                player.Find("zen-marker").localScale = newScale;
                player.transform.Find("boost").localScale = newScale;
                break;
            }
        }
    }

    [HarmonyPatch(nameof(VGPlayer.SetColor))]
    [HarmonyPrefix]
    static bool PreSetColor(VGPlayer __instance, Color _col, Color _colTail)
    {
        if (GlobalsManager.IsMultiplayer && Settings.Transparent.Value &&
            !__instance.IsLocalPlayer())
        {
            float alpha;
            switch (Settings.TransparentAlpha.Value)
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

    static float _holdingBoost = 0;
    private static bool _revertMoveX = false;
    private static bool _revertMoveY = false;
    private static bool _revertBoost = false;

    [HarmonyPatch(nameof(VGPlayer.Update))]
    [HarmonyPrefix]
    static void PreUpdate(VGPlayer __instance)
    {
        if (!__instance.IsLocalPlayer() || !GlobalsManager.IsMultiplayer || !DebugController.inst.IsOpen)
        {
            return;
        }

        if (__instance.Control_MoveX)
        {
            _revertMoveX = true;
            __instance.Control_MoveX = false;
        }

        if (__instance.Control_MoveY)
        {
            _revertMoveY = true;
            __instance.Control_MoveY = false;
        }

        if (__instance.CanBoost)
        {
            _revertBoost = true;
            __instance.CanBoost = false;
        }
    }

    [HarmonyPatch(nameof(VGPlayer.Update))]
    [HarmonyPostfix]
    static void PostUpdate(VGPlayer __instance)
    {
        if (!__instance.IsLocalPlayer())
        {
            return;
        }
        
        if (_revertMoveX)
        {
            _revertMoveX = false;
            __instance.Control_MoveX = true;
        }

        if (_revertMoveY)
        {
            _revertMoveY = false;
            __instance.Control_MoveY = true;
        }

        if (_revertBoost)
        {
            _revertBoost = false;
            __instance.CanBoost = true;
        }
        
        if (!GameManager.Inst || GameManager.Inst.CurGameState != GameManager.GameState.Playing || 
            __instance.isDead || !PointsManager.Inst)
        {
            return;
        }
        
        PointsManager.Inst.AddTimeAlive(Time.deltaTime);

        if (__instance.internalVelocity != Vector2.zero)
        {
            PointsManager.Inst.AddTimeMoving(Time.deltaTime);
        }

        PointsManager.Inst.AddPosition(CameraDB.Inst.CamerasRoot.transform.InverseTransformPoint(__instance.Player_Wrapper.position));
    }
    
    [HarmonyPatch(nameof(VGPlayer.HandleBoost))]
    [HarmonyPostfix]
    static void PostHandleBoost(VGPlayer __instance)
    {
        if (GlobalsManager.IsMultiplayer && !__instance.IsLocalPlayer())
        {
            return;
        }
        
        if (__instance.BoostDuration < _holdingBoost)
        {
            PointsManager.Inst?.AddBoost(_holdingBoost);
        }
        _holdingBoost = __instance.BoostDuration;
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
    
    [HarmonyPatch(nameof(VGPlayerManager.RespawnPlayers))]
    [HarmonyPrefix]
    static void OnCheckpoint(VGPlayerManager __instance)
    {
        foreach (var vgPlayerData in __instance.players)
        {
            if (vgPlayerData.ControllerID == 0 && vgPlayerData.hasSpawnedObject())
            {
                if (vgPlayerData.PlayerObject.Health == 1)
                {
                    PointsManager.Inst.AddCheckpointWithOneHealth();
                }
            }
        }
    }

    [HarmonyPatch(nameof(VGPlayerManager.GetPlayerColorHex))]
    [HarmonyPostfix]
    static void PostGetColorHex(ref string __result)
    {
        if (__result == "#ffffff")
        {
            __result = "ffffff";
        }
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

[HarmonyPatch(typeof(PlayerTrail))]
public static class TrailPatch
{
    [HarmonyPatch(nameof(PlayerTrail.UpdateTailFull))]
    [HarmonyPrefix]
    static bool PreTailUpdate(PlayerTrail __instance, ref int _health)
    {
        if (_health > __instance.Trail.Count)
        {
            PAM.Logger.LogFatal($"Tried to update tail with invalid health value of [{_health}]");
            return false;
        }

        return true;
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