using Lidgren.Network;
using PAMultiplayer.Managers;
using PAMultiplayer.Patch;


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

            Plugin.Inst.Log.LogWarning($"Local ID is {Player}");
            StaticManager.LocalPlayer = Player;
            StaticManager.IsLobby = isLobby;

            if(!isLobby)
            {
                if (LobbyManager.Instance)
                {
                    LobbyManager.Instance.StartLevel();
                }
            }
        }

        public override void ServerProcessPacket(NetIncomingMessage message)
        {
            //Isnt supposed to recieve anything
        }

        protected override void PacketToNetOut(NetOutgoingMessage message)
        {
            Plugin.Inst.Log.LogWarning($"Local is {Player}");
            message.Write(Player);
            message.Write(StaticManager.IsLobby);
        }
    }
}
