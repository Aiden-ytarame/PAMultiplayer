using System;
using Lidgren.Network;

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
        public virtual void ServerProcessPacket(NetIncomingMessage message)
        {
            NetServer netServer = Server.Server.Inst.netServer;
            NetOutgoingMessage NewMessage = netServer.CreateMessage();
            PacketToNetOutgoing(NewMessage);

            netServer.SendMessage(NewMessage, netServer.Connections, NetDeliveryMethod.ReliableOrdered, 0);
        }

    }

}
