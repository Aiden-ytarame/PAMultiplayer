using Lidgren.Network;


namespace PAMultiplayer.Packets
{
    public class LocalPlayerPacket : Packet
    {
        public string Player { get; set; }
        public override void ClientProcessPacket(NetIncomingMessage message)
        {
            Player = message.ReadString();


            Plugin.Instance.Log.LogWarning($"Local ID is {Player}");
            StaticManager.LocalPlayer = Player;
            Plugin.Instance.Log.LogWarning($"Local ID is {StaticManager.LocalPlayer}");
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Isnt supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            Plugin.Instance.Log.LogWarning($"Local is {Player}");
            message.Write(Player);
        }
    }
}
