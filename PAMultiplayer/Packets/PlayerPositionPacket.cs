﻿using Lidgren.Network;
using UnityEngine;
using YtaramMultiplayer.Client;

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
            Plugin.Instance.Log.LogInfo("TRY");
            if (StaticManager.Players.ContainsKey(Player))
            {
                Plugin.Instance.Log.LogWarning("Contains");
                if (StaticManager.Players[Player].PlayerObject)
                {
                    Plugin.Instance.Log.LogError("WORKED");
                    if (!StaticManager.PlayerPositions.ContainsKey(Player))
                    {
                        StaticManager.PlayerPositions.Add(Player, new Vector2(X, Y));
                        return;
                    }
                    StaticManager.PlayerPositions[Player] = new Vector2(X, Y);                  
                }
            }
        }
    
        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            X = message.ReadFloat();
            Y = message.ReadFloat();

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
