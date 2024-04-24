using Lidgren.Network;
using PAMultiplayer.Patch;


namespace PAMultiplayer.Packets
{
    public class PlayerLoadedPacket : Packet
    {
        public string Player { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            StaticManager.LobbyInfo.SetLoaded(Player);
            if(LobbyManager.instance)
            {
                LobbyManager.instance.SetPlayerLoaded(Player);
            }
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            NetServer netServer = Server.Server.Inst.NetServer;
            NetOutgoingMessage NewMessage = netServer.CreateMessage();
            PacketToNetOutgoing(NewMessage);

            netServer.SendMessage(NewMessage, netServer.Connections, NetDeliveryMethod.ReliableOrdered, 0);
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
        }
    }
}
