using Lidgren.Network;
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
            Plugin.Instance.Log.LogWarning($"attempting player spawn on player {Player}");


            StaticManager.LobbyInfo.AddPlayerInfo(Player, SteamName);

            if (LobbyManager.instance)
                LobbyManager.instance.AddPlayerToLobby(Player, SteamName);

        

            if (Player == StaticManager.LocalPlayer)
                return;

            Plugin.Instance.Log.LogWarning($"Spawning player {Player}");

            VGPlayerManager.VGPlayerData NewData = new VGPlayerManager.VGPlayerData();
            NewData.PlayerID = StaticManager.Players.Count + 1; //by the way, this can cause problems
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
            message.Write(SteamName);
        }
    }
}
