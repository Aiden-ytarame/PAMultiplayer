using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Packet;

public abstract class PacketHandler
{
    public static readonly Dictionary<PacketType, PacketHandler> PacketHandlers = new()
    {
        { PacketType.Position, new PositionPacket()},
        { PacketType.Damage, new DamagePacket()}, 
        { PacketType.Start, new StartPacket()},
        { PacketType.PlayerId, new PlayerIdPacket()},
        { PacketType.Checkpoint, new CheckpointPacket()},
        { PacketType.Rewind, new RewindPacket()},
        { PacketType.Boost, new BoostPacket()},
        { PacketType.NextLevel, new NextLevelPacket()},
        { PacketType.DamageAll, new DamageAllPacket()}
    };

   /// <summary>
   /// Checks if there's enough bytes for the type of packet received
   /// </summary>
    public void TryProcessPacket(BinaryReader reader, int size)
    {
        if (size < DataSize)
        {
            PAM.Logger.LogWarning($"Packet size is too small [{size}], type [{GetType().Name}]");
        }
        
        ProcessPacket(reader);
    }

    /// <summary>
    /// Overriden method to handle the data received using a BinaryReader.
    /// </summary>
    /// <param name="reader"></param>
    protected abstract void ProcessPacket(BinaryReader reader);
    
    /// <summary>
    /// The amount of Bytes required by this packet for it to be handled.
    /// </summary>
    protected abstract int DataSize { get; }
}

public class PositionPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        if (steamID.IsLocalPlayer()) return;
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var playerData))
        {
            if (playerData.VGPlayerData.PlayerObject)
            {
                VGPlayer player = GlobalsManager.Players[steamID].VGPlayerData.PlayerObject;
                
                if(!player) return;
                
                Transform rb = player.Player_Wrapper;
                
                Vector2 pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            
                var rot = pos - (Vector2)rb.position;
                rb.position = pos;
                if (rot.sqrMagnitude > 0.0001f)
                {
                    rot.Normalize();
                    player.p_lastMoveX = rot.x;
                    player.p_lastMoveY = rot.y;
                }
            }
        }
    }

    protected override int DataSize => 16;
}

public class DamagePacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        PAM.Inst.Log.LogDebug($"Damaging player {steamID}");

        if (steamID.IsLocalPlayer()) return;

        int health = reader.ReadInt32();
        if(GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (!player.VGPlayerData.PlayerObject.IsValidPlayer()) return;
            player.VGPlayerData.PlayerObject.Health = health;
            player.VGPlayerData.PlayerObject.PlayerHit();
        }
      
    }

    protected override int DataSize => 12;
}

public class DamageAllPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
   {
       SteamId steamID = reader.ReadUInt64();
       int healthPreHit = reader.ReadInt32();
        PAM.Inst.Log.LogInfo($"Damaging all players {healthPreHit}");

        if (DataManager.inst.GetSettingBool("MpLinkedHealthPopup", true))
        {
            if (GlobalsManager.Players.TryGetValue(steamID, out var playerData))
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
                    PAM.Inst.Log.LogWarning($"Old message");
                    continue;
                }

                if (player.IsLocalPlayer())
                {
                    PAM.Inst.Log.LogWarning($"LOCAL");
                    Player_Patch.IsDamageAll = true;
                }
                
                player.Health = healthPreHit;
                player.PlayerHit();
            }
        }
   }

    protected override int DataSize => 12;
}

public class StartPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        LobbyScreenManager.Instance?.StartLevel();
    }

    protected override int DataSize => 0;
}

public class PlayerIdPacket : PacketHandler
{
    private static int _amountOfInfo;

   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        int id = reader.ReadInt32();
        
        //will likely remove this, this is useless
        int amount = reader.ReadInt32();
        
        GlobalsManager.HasLoadedBasePlayerIds = false;
        
        _amountOfInfo++;
        PAM.Logger.LogInfo($"Player Id from [{reader}] Received");

        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            if (steamID.IsLocalPlayer())
                GlobalsManager.LocalPlayerObjectId = id;
            
            player.VGPlayerData.PlayerID = id;
        }
        else
        {
            VGPlayerManager.VGPlayerData newData = new()
            {
                PlayerID = id,
                ControllerID = id
            };
            GlobalsManager.Players.Add(steamID, new PlayerData(newData, "placeHolder"));
            VGPlayerManager.Inst.players.Add(newData);

        }
        if (_amountOfInfo >= amount)
        {
            _amountOfInfo = 0;
            GlobalsManager.HasLoadedBasePlayerIds = true;
        }
    }
   
    protected override int DataSize => 16;
}

public class CheckpointPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
   {
       int index = reader.ReadInt32();
        PAM.Logger.LogInfo($"Checkpoint [{index}] Received");
        GameManager.Inst.playingCheckpointAnimation = true;
        VGPlayerManager.Inst.RespawnPlayers();
        VGPlayerManager.Inst.HealPlayers();

        GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation(index));
    }

    protected override int DataSize => 4;
}

public class RewindPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        int index = reader.ReadInt32();
        PAM.Logger.LogInfo($"Rewind to Checkpoint [{index}] Received");
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject.IsValidPlayer())
            {
                vgPlayerData.PlayerObject.Health = 0;
                vgPlayerData.PlayerObject.ClearEvents();
                vgPlayerData.PlayerObject.PlayerDeath();
            }
        }
        GameManager.Inst.RewindToCheckpoint(index);
    }

    protected override int DataSize => 4;
}

public class BoostPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        SteamId steamID = reader.ReadUInt64();
        if(steamID.IsLocalPlayer()) return;
        
        if (GlobalsManager.Players.TryGetValue(steamID, out var player))
        {
            player.VGPlayerData.PlayerObject?.PlayParticles(VGPlayer.ParticleTypes.Boost);
        }
    }

    protected override int DataSize => 8;
}

public class NextLevelPacket : PacketHandler
{
   protected override void ProcessPacket(BinaryReader reader)
    {
        ulong levelID = reader.ReadUInt64();
        int seed = reader.ReadInt32();
        
        PAM.Logger.LogInfo($"New random seed : {seed}");

        GlobalsManager.LevelId = levelID.ToString();
        SteamLobbyManager.Inst.RandSeed = seed;
        GlobalsManager.IsReloadingLobby = true;
        
        VGLevel level = ArcadeLevelDataManager.Inst.GetLocalCustomLevel(GlobalsManager.LevelId);
        if (level)
        {
            ArcadeManager.Inst.CurrentArcadeLevel = level;
            SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
            return;
        }

        GlobalsManager.IsDownloading = true;
        PAM.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }

    protected override int DataSize => 12;
}
