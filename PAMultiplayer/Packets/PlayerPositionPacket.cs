using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class PlayerPositionPacket : Packet
    {
        public string Player { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            X = message.ReadFloat();
            Y = message.ReadFloat();

            if (Player == StaticManager.LocalPlayer)
                return;

            StaticManager.Players[Player].PlayerObject.Player_Rigidbody.transform.position = new Vector2(X, Y);
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            NetServer netServer = Server.Server.Inst.NetServer;
            NetOutgoingMessage NewMessage = netServer.CreateMessage();
            PacketToNetOutgoing(NewMessage);
            netServer.SendMessage(NewMessage, netServer.Connections, NetDeliveryMethod.Unreliable, 0);
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
            message.Write(X);
            message.Write(Y);
        }
    }
}
