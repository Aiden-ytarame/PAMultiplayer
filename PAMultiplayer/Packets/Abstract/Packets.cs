using System;
using Lidgren.Network;
using UnityEngine;

namespace YtaramMultiplayer.Packets
{
    public abstract class Packet
    {

        public void PacketToNetOutgoing(NetOutgoingMessage message)
        {
            message.Write(GetType().ToString());
            PacketToNetOut(message);
        }

        protected abstract void PacketToNetOut(NetOutgoingMessage message);

        public abstract void ClientProcessPacket(NetIncomingMessage message);
        public abstract void ServerProcessPacket(NetIncomingMessage message);


    }

}
