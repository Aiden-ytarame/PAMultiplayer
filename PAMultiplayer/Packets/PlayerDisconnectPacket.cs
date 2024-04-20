using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class PlayerDisconnectPacket : Packet
    {
        public string Player { get; set; }

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            Plugin.Instance.Log.LogWarning($"Removing player {Player}");

            var player = StaticManager.Players[Player].PlayerObject;
            player.PlayerDeath();
            VGPlayerManager.Inst.players.Remove(StaticManager.Players[Player]);
            StaticManager.Players.Remove(Player);

        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
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
