using System.Collections.Generic;
using Il2CppSystems.SceneManagement;
using PAMultiplayer.Managers;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Packet;

public interface IPacketHandler
{
    public static readonly Dictionary<PacketType, IPacketHandler> PacketHandlers = new()
    {
        { PacketType.Position, new PositionPacket()},
        { PacketType.Damage, new DamagePacket()}, 
        { PacketType.Start, new StartPacket()},
        { PacketType.PlayerId, new PlayerIdPacket()},
        { PacketType.Checkpoint, new CheckpointPacket()},
        { PacketType.Rewind, new RewindPacket()},
        { PacketType.Boost, new BoostPacket()},
        { PacketType.nextLevel, new NextLevelPacket()}
    };
    public void ProcessPacket(SteamId senderId, object data);
}

public class PositionPacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        if (senderId.IsLocalPlayer()) return;
        
        var pos = (Vector2)data;
        if (GlobalsManager.Players.TryGetValue(senderId, out var playerData))
        {
            if (playerData.PlayerObject)
            {
                VGPlayer player = GlobalsManager.Players[senderId].PlayerObject;
                
                if(!player) return;
                
                Transform rb = player.Player_Wrapper;
               // Vector2 DeltaPos = rb.position - PosEnu.Current.Value;
                //StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Wrapper.transform.Rotate(new Vector3(0, 0, Mathf.Atan2(DeltaPos.x, DeltaPos.y)), Space.World);

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
}

public class DamagePacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        PAM.Inst.Log.LogWarning($"Damaging player { senderId}");

        if ( senderId.IsLocalPlayer()) return;

        int health = (int)data;
        if(GlobalsManager.Players.TryGetValue(senderId, out var player))
        {
            if (!player.PlayerObject || player.PlayerObject.isDead) return;
            player.PlayerObject.Health = health;
            player.PlayerObject.PlayerHit();
        }
      
    }
}

public class StartPacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        LobbyScreenManager.Instance?.StartLevel();
    }
}

public class PlayerIdPacket : IPacketHandler
{
    private static int _amountOfInfo;

    public void ProcessPacket(SteamId senderId, object data)
    {
        Vector2 info = (Vector2)data;

        int id = (int)info.x;
        
        //will likely remove this, this is useless
        int amount = (int)info.y;
        
        GlobalsManager.HasLoadedAllInfo = false;
        _amountOfInfo++;
        PAM.Logger.LogInfo($"Player Id from [{senderId}] Received");

        if (GlobalsManager.Players.TryGetValue(senderId, out var player))
        {
            if (senderId.IsLocalPlayer())
                GlobalsManager.LocalPlayerObjectId = id;
            
            player.PlayerID = id;
        }
        else
        {
            VGPlayerManager.VGPlayerData newData = new()
            {
                PlayerID = id,
                ControllerID = id
            };
            GlobalsManager.Players.Add(senderId, newData);
            VGPlayerManager.Inst.players.Add(newData);

        }
        if (_amountOfInfo >= amount)
        {
            _amountOfInfo = 0;
            GlobalsManager.HasLoadedAllInfo = true;
        }
    }
}

public class CheckpointPacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        PAM.Logger.LogInfo($"Checkpoint [{(int)data}] Received");
        GameManager.Inst.playingCheckpointAnimation = true;
        VGPlayerManager.Inst.RespawnPlayers();

        GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation((int)data));
    }
}

public class RewindPacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        PAM.Logger.LogInfo($"Rewind to Checkpoint [{(int)data}] Received");
        foreach (var vgPlayerData in VGPlayerManager.Inst.players)
        {
            if (vgPlayerData.PlayerObject && !vgPlayerData.PlayerObject.isDead)
            {
                vgPlayerData.PlayerObject.Health = 0;
                vgPlayerData.PlayerObject.ClearEvents();
                vgPlayerData.PlayerObject.PlayerDeath();
            }
        };
        GameManager.Inst.RewindToCheckpoint((int)data);
    }
}

public class BoostPacket : IPacketHandler
{
    public void ProcessPacket(SteamId senderId, object data)
    {
        if(senderId.IsLocalPlayer()) return;
        
        if (GlobalsManager.Players.TryGetValue(senderId, out var player))
        {
            player.PlayerObject?.PlayParticles(VGPlayer.ParticleTypes.Boost);
        }
    }
}

public class NextLevelPacket : IPacketHandler
{
    public void ProcessPacket(SteamId levelId, object data)
    {
        int seed = (int)data;
        GlobalsManager.LevelId = levelId;
        SteamLobbyManager.Inst.RandSeed = seed;
        GlobalsManager.IsReloadingLobby = true;
     
        PAM.Logger.LogInfo($"SEED : {seed}");

        foreach (var level in ArcadeLevelDataManager.Inst.ArcadeLevels)
        {
            if (level.SteamInfo.ItemID.Value == GlobalsManager.LevelId)
            {
                ArcadeManager.Inst.CurrentArcadeLevel = level;
                SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
                return;
            }
        }

        GlobalsManager.IsDownloading = true;
        PAM.Logger.LogError($"You did not have the lobby's level downloaded!, Downloading Level...");
        SceneLoader.Inst.LoadSceneGroup("Arcade_Level");
    }
}
