using Steamworks;
using UnityEngine;

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
    public int data;

    public IntNetPacket()
    {
        PacketType = PacketType.Damage;
        SenderId = default;
        data = default;
    }
}

public struct VectorNetPacket
{
    public PacketDataType DataType = PacketDataType.Vector;
    public PacketType PacketType;
    public SteamId SenderId;
    public Vector2 data;

    public VectorNetPacket()
    {
        PacketType = PacketType.Damage;
        SenderId = default;
        data = default;
    }
}