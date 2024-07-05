using System;
using Steamworks;

namespace PAMultiplayer.Packet;

public enum PacketType : short
{
    Damage,
    Loaded,
    Position,
    Rotation,
    Start,
    Spawn,
    Checkpoint,
    Rewind
}

public struct NetPacket
{
    public PacketType PacketType;
    public SteamId SenderId;
    public unsafe fixed byte Buffer[12];
}

public struct PlayersPacket
{
    public struct PlayerInfo
    {
        public PlayerInfo( SteamId id, int playerId)
        {
            Id = id;
            PlayerId = playerId;
        }
        public SteamId Id;
        public int PlayerId;
    }

    public PlayersPacket(PlayerInfo[] info)
    {
        Info = info;
    }
    public PlayerInfo[] Info;
}