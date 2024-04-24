using Lidgren.Network;


namespace PAMultiplayer.Packets
{
    public class LocalPlayerPacket : Packet
    {
        public string Player { get; set; }
        public bool isLobby { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();
            isLobby = message.ReadBoolean();

            Plugin.Instance.Log.LogWarning($"Local ID is {Player}");
            StaticManager.LocalPlayer = Player;
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Isnt supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            Plugin.Instance.Log.LogWarning($"Local is {Player}");
            message.Write(Player);
            message.Write(StaticManager.IsLobby);
        }
    }
}
