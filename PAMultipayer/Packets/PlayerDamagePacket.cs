using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class PlayerDamagePacket : Packet
    {
        public string Player;

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Instance.Log.LogWarning("Damaging player " + Player);

            var player = StaticManager.Players[Player].PlayerObject;
            StaticManager.DamageQueue = player.PlayerID;
            player.PlayerHit();
        }


        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
        }
    }
}
