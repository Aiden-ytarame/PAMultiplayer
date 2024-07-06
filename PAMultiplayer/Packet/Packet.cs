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
    Float,
    Vector
}

public struct FloatNetPacket 
{
    public PacketDataType DataType = PacketDataType.Float;
    public PacketType PacketType;
    public SteamId SenderId;
    public float data;

    public FloatNetPacket()
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
public struct PlayerPacket
{
    public PlayerPacket( SteamId id, int playerId)
    {
        Id = id;
        PlayerId = playerId;
    }
    public SteamId Id;
    public int PlayerId;
}