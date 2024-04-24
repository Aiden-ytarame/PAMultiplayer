using Lidgren.Network;
using PAMultiplayer.Patch;


namespace PAMultiplayer.Packets
{
    public class StartLevelPacket : Packet
    {
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            LobbyManager.instance.StartLevel();
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Isnt supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
           
        }
    }
}
