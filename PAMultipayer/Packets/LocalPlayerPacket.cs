using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class LocalPlayerPacket : Packet
    {
        public string Player;
 
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();


            Plugin.Instance.Log.LogWarning("Local ID is " + Player);
            StaticManager.LocalPlayer = Player;
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Isnt supposed to recive anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
        }
    }
}
