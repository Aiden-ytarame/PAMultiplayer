﻿using Il2CppSystem;
using Lidgren.Network;
using System;
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
            Plugin.Instance.Log.LogWarning($"LocalPlayerId{StaticManager.Players[StaticManager.LocalPlayer].PlayerID}");
            Plugin.Instance.Log.LogWarning($"LocalControllerId{StaticManager.Players[StaticManager.LocalPlayer].ControllerID}");
            Plugin.Instance.Log.LogWarning($"Damaging player {Player}");
           
            var player = StaticManager.Players[Player].PlayerObject;
            if (player == null || !player.gameObject)
                return;

            StaticManager.DamageQueue.Add(player.PlayerID);
            

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
