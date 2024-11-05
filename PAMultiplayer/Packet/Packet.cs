using UnityEngine;

namespace PAMultiplayer.Packet;

public enum PacketType : ushort
{
    Damage,
    Position,
    Start,
    PlayerId,
    Checkpoint,
    Rewind,
    Boost,
    nextLevel
}

public struct NetPacket 
{
    public PacketType PacketType;
    public ulong SenderId;
    public Vector2 Data = Vector2.zero;

    public NetPacket(Vector2 data)
    {
        Data = data;
    }

    public NetPacket(int data)
    {
        Data.x = data;
    }
}
