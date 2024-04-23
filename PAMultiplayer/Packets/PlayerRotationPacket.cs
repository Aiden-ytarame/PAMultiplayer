using Lidgren.Network;

namespace PAMultiplayer.Packets
{
    public class PlayerRotationPacket : Packet
    {
        public string Player { get; set; }
        public float Z { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            Z = message.ReadFloat();

            if (Player == StaticManager.LocalPlayer)
                return;
            if (StaticManager.Players.ContainsKey(Player))
            {
                if (StaticManager.Players[Player].PlayerObject)
                {
                  //  if (!StaticManager.PlayerRotations.ContainsKey(Player))
                  //  {
                    //    StaticManager.PlayerRotations.Add(Player, Z);
                    //    return;
                   // }
                   // StaticManager.PlayerRotations[Player] = Z;
                }
            }
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            Z = message.ReadFloat();

            NetServer netServer = Server.Server.Inst.NetServer;
            NetOutgoingMessage NewMessage = netServer.CreateMessage();
            PacketToNetOutgoing(NewMessage);
  
            netServer.SendMessage(NewMessage, netServer.Connections, NetDeliveryMethod.Unreliable, 0);
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
            message.Write(Z);
        }
    }
}
