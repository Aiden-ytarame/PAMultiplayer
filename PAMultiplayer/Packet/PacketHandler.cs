using System;
using System.Runtime.InteropServices;
using PAMultiplayer.Managers;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Packet;

public abstract class PacketHandler
{
    public abstract void ProcessPacket(SteamId senderId, byte[] bytes);
}

public class PositionPacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, byte[] bytes)
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        Vector2 pos = new();
        Marshal.PtrToStructure(handle.AddrOfPinnedObject(), pos);
        handle.Free();
        
        if (senderId == StaticManager.LocalPlayer)
            return;
        
        if (StaticManager.Players.TryGetValue(senderId, out var playerData))
        {
            if (playerData.PlayerObject)
            {
                if (!StaticManager.PlayerPositions.ContainsKey(senderId))
                {
                    StaticManager.PlayerPositions.Add(senderId, Vector2.zero);
                    return;
                }

                VGPlayer player;
                var rot = pos - StaticManager.PlayerPositions[senderId];
                if (rot.sqrMagnitude > 0.0001f && (player = StaticManager.Players[senderId].PlayerObject))
                {
                    rot.Normalize();
                    player.p_lastMoveX = rot.x;
                    player.p_lastMoveY = rot.y;
                }

                StaticManager.PlayerPositions[senderId] = pos;
            }
        }
    }
}

public class DamagePacket : PacketHandler
{
    public override void ProcessPacket(SteamId senderId, byte[] bytes)
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        int health = 3;
        Marshal.PtrToStructure(handle.AddrOfPinnedObject(), health);
        handle.Free();
        
        Plugin.Inst.Log.LogWarning($"Damaging player { senderId}");

        if ( senderId == StaticManager.LocalPlayer) return;

        VGPlayer player = StaticManager.Players[ senderId].PlayerObject;
        if (!player) return;
        player.Health = health;
        player.PlayerHit();
    }
}
public static class test
{
    public static void HandleClientPacket(NetPacket packet)
    {
        switch (packet.PacketType)
        {
            case PacketType.Damage:
                Plugin.Inst.Log.LogWarning($"Damaging player {packet.SenderId}");

                if (packet.SenderId == StaticManager.LocalPlayer) return;

                VGPlayer player = StaticManager.Players[packet.SenderId].PlayerObject;
                if (!player) return;
                player.Health = (int)packet.Data;
                player.PlayerHit();
                break;

            case PacketType.Start:
                LobbyManager.Instance.StartLevel();
                break;

            case PacketType.Loaded:
                if (packet.SenderId == StaticManager.LocalPlayer) return;

                SteamLobbyManager.Inst.SetLoaded(packet.SenderId);
                if (LobbyManager.Instance)
                {
                    LobbyManager.Instance.SetPlayerLoaded(packet.SenderId);
                }

                break;

            case PacketType.Position:
                if (packet.SenderId == StaticManager.LocalPlayer)
                    return;

                SteamId sender = packet.SenderId;
                Vector2 pos = (Vector2)packet.Data;
                if (StaticManager.Players.TryGetValue(packet.SenderId, out var playerData))
                {
                    if (playerData.PlayerObject)
                    {
                        if (!StaticManager.PlayerPositions.ContainsKey(sender))
                        {
                            StaticManager.PlayerPositions.Add(sender, Vector2.zero);
                            return;
                        }

                        var rot = pos - StaticManager.PlayerPositions[sender];
                        if (rot.sqrMagnitude > 0.0001f && (player = StaticManager.Players[sender].PlayerObject))
                        {
                            rot.Normalize();
                            player.p_lastMoveX = rot.x;
                            player.p_lastMoveY = rot.y;
                        }

                        StaticManager.PlayerPositions[sender] = pos;
                    }
                }

                break;

            case PacketType.Rotation:
                break;

            case PacketType.Spawn:
                var info = (PlayersPacket)packet.Data;
                Plugin.Logger.LogInfo($"Players Id with [{info.Info.Length}] Members Received");
                foreach (var playerInfo in info.Info)
                {
                    if (playerInfo.Id == StaticManager.LocalPlayer)
                        StaticManager.LocalPlayerId = playerInfo.PlayerId;
                    StaticManager.Players[playerInfo.Id].PlayerID = playerInfo.PlayerId;
                }

                break;
            case PacketType.Checkpoint:
                Plugin.Logger.LogInfo($"Checkpoint [{(int)packet.Data}] Received");
                GameManager.Inst.playingCheckpointAnimation = true;
                VGPlayerManager.Inst.RespawnPlayers();

                GameManager.Inst.StartCoroutine(GameManager.Inst.PlayCheckpointAnimation((int)packet.Data));
                break;
            case PacketType.Rewind:
                Plugin.Logger.LogInfo($"Rewind to Checkpoint [{(int)packet.Data}] Received");
                VGPlayerManager.Inst.players.ForEach(new System.Action<VGPlayerManager.VGPlayerData>(x =>
                {
                    if (!x.PlayerObject.isDead)
                    {
                        x.PlayerObject.Health = 1;
                        x.PlayerObject.PlayerHit();
                        //forcekill() fucked up the game.
                    }
                }));
                GameManager.Inst.RewindToCheckpoint((int)packet.Data);
                break;
        }
    }

}