using Lidgren.Network;
using YtaramMultiplayer.Packets;
using System;
using UnityEngine;
using System.Linq;

namespace YtaramMultiplayer.Client
{
    public class Client : MonoBehaviour
    {
        public NetClient NetClient;
        public Client(int Port, string Server, string ServerName)
        {
            var config = new NetPeerConfiguration(ServerName);
            config.AutoFlushSendQueue = false;
    

            System.Threading.Thread thread;

            thread = new System.Threading.Thread(Listen);
            NetClient = new NetClient(config);
           
            // client.RegisterReceivedCallback(new System.Threading.SendOrPostCallback(ReciveMessage), System.Threading.SynchronizationContext.Current);
            thread.Start();
            NetClient.Start();
            NetClient.Connect(Server, Port);

        }

        public void Listen()
        {
            NetIncomingMessage message;
            while (true)
            {
                while ((message = NetClient.ReadMessage()) != null)
                {

                    switch (message.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
                            string reason = message.ReadString();
                            if (status == NetConnectionStatus.Disconnected)
                            {
                                var _player = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                                StaticManager.Players.Clear();
      

                                //kill all players but the host
                            }
                            break;
                        case NetIncomingMessageType.Data:
                            string TypeStr = message.ReadString();
                            Type PacketType = Type.GetType(TypeStr);
                            try
                            {
                                ((Packet)Activator.CreateInstance(PacketType)).ClientProcessPacket(message);
                            }
                            catch(Exception ex)
                            {
                                Plugin.Instance.Log.LogError("CLIENT: Unhandled packet Recieved");
                                Plugin.Instance.Log.LogError(ex);
                            }
                            break;

                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.ErrorMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.VerboseDebugMessage:
                            string text = message.ReadString();
                            Plugin.Instance.Log.LogWarning(text);
                            break;
                        default:
                            Plugin.Instance.Log.LogError($"Unhandled type: { message.MessageType} { message.LengthBytes} bytes { message.DeliveryMethod}|{ message.SequenceChannel}");
                            break;
                    }
                    NetClient.Recycle(message);

                }
            }
        }

        public void SendPosition(float X, float Y)
        {

            NetOutgoingMessage message = NetClient.CreateMessage();
            new PlayerPositionPacket { Player = StaticManager.LocalPlayer, X = X, Y = Y }.PacketToNetOutgoing(message);
            NetClient.SendMessage(message, NetDeliveryMethod.Unreliable);
            NetClient.FlushSendQueue();
        }
        public void SendRotation(float z)
        {
            
            NetOutgoingMessage message = NetClient.CreateMessage();
            new PlayerRotationPacket { Player = StaticManager.LocalPlayer, Z = z }.PacketToNetOutgoing(message);
            NetClient.SendMessage(message, NetDeliveryMethod.Unreliable, 0);
            NetClient.FlushSendQueue();
        }

        public void SendDisconnect()
        {
            NetOutgoingMessage message = NetClient.CreateMessage();
            new PlayerDisconnectPacket() { Player = StaticManager.LocalPlayer }.PacketToNetOutgoing(message);
            NetClient.SendMessage(message, NetDeliveryMethod.ReliableOrdered, 0);
            NetClient.FlushSendQueue();

            NetClient.Disconnect("Bye!");
        }
        public void SendDamage()
        {
            Plugin.Instance.Log.LogWarning($"Damaged player {StaticManager.LocalPlayer}");
            NetOutgoingMessage message = NetClient.CreateMessage();
            new PlayerDamagePacket() { Player = StaticManager.LocalPlayer }.PacketToNetOutgoing(message);
            
            NetClient.SendMessage(message, NetDeliveryMethod.ReliableOrdered, 0);
            NetClient.FlushSendQueue();
        }
    }
}
