using Lidgren.Network;
using PAMultiplayer.Managers;

namespace PAMultiplayer.Packets
{
    public class PlayerDisconnectPacket : Packet
    {
        public string Player { get; set; }

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            Plugin.Inst.Log.LogWarning($"Removing player {Player}");

            var player = StaticManager.Players[Player].PlayerObject;
            player.PlayerDeath();
            VGPlayerManager.Inst.players.Remove(StaticManager.Players[Player]);
            StaticManager.PlayerPositions.Remove(Player);
            StaticManager.Players.Remove(Player);

        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            return;
            Player = message.ReadString();
            Server.Server server = Server.Server.Inst;        
            server.Players.Remove(Player);
            
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
