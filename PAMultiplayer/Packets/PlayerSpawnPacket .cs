using Lidgren.Network;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;
using Rewired;

namespace PAMultiplayer.Packets
{
    public class PlayerSpawnPacket : Packet
    {
        public string Player { get; set; }
        public string SteamName { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            SteamName = message.ReadString();
            Plugin.Inst.Log.LogWarning($"attempting player spawn on player {Player}");

            

        

            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Inst.Log.LogWarning($"Spawning player {Player}");

            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = StaticManager.Players.Count + 1; //by the way, this can cause problems
            NewData.ControllerID = StaticManager.Players.Count + 1;
            

            if (!VGPlayerManager.Inst.players.Contains(NewData))
                VGPlayerManager.Inst.players.Add(NewData);
            //StaticManager.SpawnPending = true;

            if (!StaticManager.Players.ContainsKey(Player))
                StaticManager.Players.Add(Player, NewData);

            if (!SceneManager.inst.isLoading && !LobbyManager.Instance)
                    VGPlayerManager.inst.RespawnPlayers();

            

        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Inst supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            message.Write(Player);
            message.Write(SteamName);
        }
    }
}
