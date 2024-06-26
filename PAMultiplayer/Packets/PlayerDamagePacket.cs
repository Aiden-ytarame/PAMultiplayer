using Lidgren.Network;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Packets
{
    public class PlayerDamagePacket : Packet
    {
        public string Player { get; set; }

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            if (Player == StaticManager.LocalPlayer)
                return;
            
            Plugin.Inst.Log.LogWarning($"Damaging player {Player}");
           
            VGPlayer player = StaticManager.Players[Player].PlayerObject;
            if (player == null || !player.gameObject)
                return;

            player.PlayerHit();
           // StaticManager.Players[Player].PlayerObject.PlayerHit();
           // StaticManager.DamageQueue.Add(player.PlayerID);
            

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
