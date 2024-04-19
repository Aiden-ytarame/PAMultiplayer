using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
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

            StaticManager.Players[Player].PlayerObject.Player_Rigidbody.transform.eulerAngles = new Vector3(0, 0, Z);
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
            message.Write(Z);
        }
    }
}
