using PAMultiplayer.Packets2;

namespace PAMultiplayer.Packets;

public struct NetPacket
{
    public PacketType PacketType;
    public byte[] Data;
}