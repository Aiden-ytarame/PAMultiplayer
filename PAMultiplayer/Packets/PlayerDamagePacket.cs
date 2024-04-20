using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class PlayerDamagePacket : Packet
    {
        public string Player { get; set; }

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Instance.Log.LogWarning($"Damaging player {Player}");

            var player = StaticManager.Players[Player].PlayerObject;
            StaticManager.DamageQueue = player.PlayerID;
            player.PlayerHit();
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
