
using Il2CppSystem.Collections;
using Lidgren.Network;
using YtaramMultiplayer.Packets;
using System;
using System.Collections;
using UnityEngine;
namespace YtaramMultiplayer.Client
{
    public class Client : MonoBehaviour
    {
        public NetClient client;
        public Client(int Port, string Server, string ServerName)
        {
            var config = new NetPeerConfiguration(ServerName);
            config.AutoFlushSendQueue = false;
    

            System.Threading.Thread thread;

            thread = new System.Threading.Thread(Listen);
            client = new NetClient(config);
           
            // client.RegisterReceivedCallback(new System.Threading.SendOrPostCallback(ReciveMessage), System.Threading.SynchronizationContext.Current);
            thread.Start();
            client.Start();
            client.Connect(Server, Port);

        }

        public void Listen()
        {
            NetIncomingMessage message;
            while (true)
            {
                while ((message = client.ReadMessage()) != null)
                {

                    switch (message.MessageType)
                    {
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
                            Plugin.Instance.Log.LogError("Unhandled type: " + message.MessageType + " " + message.LengthBytes + " bytes " + message.DeliveryMethod + "|" + message.SequenceChannel);
                            break;
                    }
                    client.Recycle(message);

                }
            }
        }

        public void SendPosition(float X, float Y)
        {

            NetOutgoingMessage message = client.CreateMessage();
            new PlayerPositionPacket { Player = StaticManager.LocalPlayer, X = X, Y = Y }.PacketToNetOutgoing(message);
            client.SendMessage(message, NetDeliveryMethod.Unreliable);
            client.FlushSendQueue();
        }
        public void SendRotation(float Z)
        {
            
            NetOutgoingMessage message = client.CreateMessage();
            new PlayerRotationPacket { Player = StaticManager.LocalPlayer, Z = Z }.PacketToNetOutgoing(message);
            client.SendMessage(message, NetDeliveryMethod.Unreliable);
            client.FlushSendQueue();
        }

        public void SendDisconnect()
        {
            NetOutgoingMessage message = client.CreateMessage();
            new PlayerDisconnectPacket() { Player = StaticManager.LocalPlayer }.PacketToNetOutgoing(message);
            client.SendMessage(message, NetDeliveryMethod.ReliableOrdered, 0);
            client.FlushSendQueue();

            client.Disconnect("Bye!");
        }
        public void SendDamage()
        {
            NetOutgoingMessage message = client.CreateMessage();
            new PlayerDamagePacket() { Player = StaticManager.LocalPlayer }.PacketToNetOutgoing(message);
            client.SendMessage(message, NetDeliveryMethod.ReliableOrdered);
            client.FlushSendQueue();
        }
    }
}
