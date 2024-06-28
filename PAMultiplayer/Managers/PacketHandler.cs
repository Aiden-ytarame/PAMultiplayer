using System.Runtime.InteropServices;
using PAMultiplayer.Packet;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Managers;

public static class PacketHandler
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
                if(LobbyManager.Instance)
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

                        StaticManager.PlayerPositions[sender] = pos;
                    }
                }
                break;
            
            case PacketType.Rotation:
                break;
            
            case PacketType.Spawn:
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