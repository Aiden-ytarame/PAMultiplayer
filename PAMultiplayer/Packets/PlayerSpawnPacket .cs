using Lidgren.Network;

namespace PAMultiplayer.Packets
{
    public class PlayerSpawnPacket : Packet
    {
        public string Player { get; set; }

        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            Plugin.Instance.Log.LogWarning($"attempting player spawn on player {Player}");
            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Instance.Log.LogWarning($"Spawning player {Player}");

            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = StaticManager.Players.Count + 1;
            NewData.ControllerID = StaticManager.Players.Count + 1;
            

            if (!VGPlayerManager.Inst.players.Contains(NewData))
                VGPlayerManager.Inst.players.Add(NewData);
            //StaticManager.SpawnPending = true;

            if (!StaticManager.Players.ContainsKey(Player))
                StaticManager.Players.Add(Player, NewData);

            if(!SceneManager.inst.isLoading)
                VGPlayerManager.inst.RespawnPlayers();
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Inst supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
        }
    }
}
