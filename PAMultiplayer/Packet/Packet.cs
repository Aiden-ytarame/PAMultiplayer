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
    public object Data;
}