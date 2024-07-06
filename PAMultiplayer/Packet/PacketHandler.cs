using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Packet;

public abstract class PacketHandler
{
    public static Dictionary<PacketType, PacketHandler> PacketHandlers = new()
    {
        { PacketType.Position, new PositionPacket() },
        { PacketType.Damage, new DamagePacket() }, 
        { PacketType.Start, new StartPacket() },
        { PacketType.Loaded, new LoadedPacket() },
        { PacketType.Spawn , new SpawnPacket()},
        { PacketType.Checkpoint , new CheckpointPacket()},
        { PacketType.Rewind , new RewindPacket()}
    };
    public abstract void ProcessPacket(SteamId senderId, object data);
}

public class PositionPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        if (senderId == StaticManager.LocalPlayer) return;
        var pos = (Vector2)data;
        if (StaticManager.Players.TryGetValue(senderId, out var playerData))
        {
            if (playerData.PlayerObject)
            {
                VGPlayer player = StaticManager.Players[senderId].PlayerObject;
                
                if(!player) return;
                
                Rigidbody2D rb = player.Player_Rigidbody;
               // Vector2 DeltaPos = rb.position - PosEnu.Current.Value;
                //StaticManager.Players[PosEnu.Current.Key].PlayerObject.Player_Wrapper.transform.Rotate(new Vector3(0, 0, Mathf.Atan2(DeltaPos.x, DeltaPos.y)), Space.World);

                var rot = pos - rb.position;
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

public class DamagePacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        
        Plugin.Inst.Log.LogWarning($"Damaging player { senderId}");

        if ( senderId == StaticManager.LocalPlayer) return;

        int health = (int)(Single)data;
        VGPlayer player = StaticManager.Players[senderId].PlayerObject;
        if (!player) return;
        player.Health = health;
        player.PlayerHit();
    }
}

public class StartPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        LobbyManager.Instance?.StartLevel();
    }
}

public class LoadedPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        Plugin.Logger.LogInfo($"Received Loaded Confirmation from [{senderId}]");
        
        if (senderId == StaticManager.LocalPlayer) return;

        SteamLobbyManager.Inst.SetLoaded(senderId);
        LobbyManager.Instance.SetPlayerLoaded(senderId);
        
    }
}

public class SpawnPacket : PacketHandler
{
    private static int _amountOfInfo;

    public override void ProcessPacket(SteamId senderId, object data)
    {
        Vector2 info = (Vector2)data;

        int id = (int)info.x;
        int amount = (int)info.y;
        StaticManager.HasLoadedAllInfo = false;
        _amountOfInfo++;
        Plugin.Logger.LogInfo($"Players Id from [{senderId}] Received");

        try
        {
            var player = StaticManager.Players[senderId];
            player.PlayerID = id;
            if (player.PlayerObject)
            {
                Plugin.Logger.LogError("Player Object");
                player.PlayerObject.PlayerID = id;
            }

            StaticManager.Players[senderId].PlayerID = id;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        if (_amountOfInfo >= amount)
        {
            amount = 0;
            StaticManager.HasLoadedAllInfo = true;
        }
    }
}

public class CheckpointPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        Plugin.Logger.LogInfo($"Checkpoint [{(int)(Single)data}] Received");
        GameManager.Inst.playingCheckpointAnimation = true;
        VGPlayerManager.Inst.RespawnPlayers();

        GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation((int)(Single)data));
    }
}

public class RewindPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, object data)
    {
        Plugin.Logger.LogInfo($"Rewind to Checkpoint [{(int)(Single)data}] Received");
        VGPlayerManager.Inst.players.ForEach(new System.Action<VGPlayerManager.VGPlayerData>(x =>
        {
            if (!x.PlayerObject.isDead)
            {
                x.PlayerObject.Health = 1;
                x.PlayerObject.PlayerHit();
                //forcekill() fucked up the game.
            }
        }));
        GameManager.Inst.RewindToCheckpoint((int)(Single)data);
    }
}