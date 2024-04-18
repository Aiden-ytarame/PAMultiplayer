using Lidgren.Network;
using System.Collections.Generic;
using UnityEngine;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Server;

namespace YtaramMultiplayer.Packets
{
    public class PlayerSpawnPacket : Packet
    {
        public string Player;
 
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();

            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Instance.Log.LogWarning("Spawning player " + Player);

            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = StaticManager.Players.Count + 1;
            NewData.ControllerID = StaticManager.Players.Count + 1;

            if (VGPlayerManager.Inst.players.Contains(NewData))
                VGPlayerManager.Inst.players.Add(NewData);
            StaticManager.SpawnPending = true;

            if (!StaticManager.Players.ContainsKey(Player))
                StaticManager.Players.Add(Player, NewData);
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Inst supposed to recive anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
        }
    }
}
