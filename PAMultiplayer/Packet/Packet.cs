using Steamworks;
using UnityEngine;

namespace PAMultiplayer.Packet;

public enum PacketType : short
{
    Damage,
    Loaded,
    Position,
    Start,
    PlayerId,
    Checkpoint,
    Rewind,
    Boost
}

public enum PacketDataType : short
{
    Int,
    Vector
}

public struct IntNetPacket 
{
    public PacketDataType DataType = PacketDataType.Int;
    public PacketType PacketType;
    public SteamId SenderId;
    public int Data;

    public IntNetPacket()
    {
        PacketType = PacketType.Damage;
        SenderId = default;
        Data = default;
    }
}

public struct VectorNetPacket
{
    public PacketDataType DataType = PacketDataType.Vector;
    public PacketType PacketType;
    public SteamId SenderId;
    public Vector2 Data;

    public VectorNetPacket()
    {
        PacketType = PacketType.Damage;
        SenderId = default;
        Data = default;
    }
}
