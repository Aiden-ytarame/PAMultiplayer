﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using YtaramMultiplayer.Client;
using YtaramMultiplayer.Packets;

namespace YtaramMultiplayer.Server
{
    public class PlayerPosition
    {
        public float x;
        public float y;
    }
 
    public class Server
    {
        public static Server Inst;
        public NetServer NetServer { get; private set; }
        Thread thread;
        public List<string> Players = new List<string>();
 
        public Server()
        {
            Inst = this;

           NetPeerConfiguration config = new NetPeerConfiguration("PAServer");
            config.MaximumConnections = 4;
            config.Port = int.Parse(StaticManager.ServerPort);
            config.EnableUPnP = true;
          
            NetServer = new NetServer(config);
            NetServer.Start();
            NetServer.UPnP.ForwardPort(int.Parse(StaticManager.ServerPort), "GameStuff");
 
              
            thread = new Thread(Listen);
            thread.Start();
        }
        void Listen()
        {
            Plugin.Instance.Log.LogWarning("StartedServer");
            while (true)
            {
                NetIncomingMessage message;
                while ((message = NetServer.ReadMessage()) != null)
                {
                    List<NetConnection> all = NetServer.Connections;
                    switch(message.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
                            string reason = message.ReadString();
                            if(status == NetConnectionStatus.Connected)
                            {
                                var player = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                                Players.Add(player);
                                SendLocalPlayerPacket(message.SenderConnection, player);

                                SpawnPlayers(all, message.SenderConnection, player); //spawn all players on new client
                            }
                            if (status == NetConnectionStatus.Disconnected)
                            {
                                var player = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);

                                //SendPlayerDisconected(all, new PlayerDisconnectsPacket() { Player = player });
                            }
                            break;
                        case NetIncomingMessageType.Data:
                            string TypeStr = message.ReadString();
                            Type PacketType = Type.GetType(TypeStr);
                            Plugin.Instance.Log.LogWarning(TypeStr);
                           
                            try
                            {
                               ((Packet)Activator.CreateInstance(PacketType)).ServerProcessPacket(message);

                            }
                            catch(Exception ex)
                            {                               
                                Plugin.Instance.Log.LogError("SERVER: Unhandled packet.");
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
                             Plugin.Instance.Log.LogWarning($"Unhandled type: {message.MessageType} {message.LengthBytes} bytes {message.DeliveryMethod}|{message.SequenceChannel}");
                            break;
                    }
                    NetServer.Recycle(message);
                }
            }
        }

        public void SpawnPlayers(List<NetConnection> netConnections, NetConnection Local, string Player)
        {
            Plugin.Instance.Log.LogWarning($"Player Spawned: {Player}");
            //spawn all player on newly connected player
            foreach (NetConnection connection in netConnections)
            {
                string _player = NetUtility.ToHexString(connection.RemoteUniqueIdentifier);

                if(_player != Player)
                {
                    SendSpawnPacketToLocal(Local, _player);
                }
            }
            //spawn new player on all other clie
            SendSpawnPacketToAll(netConnections, Player);
        }

        public void SendLocalPlayerPacket(NetConnection Local, string Player)
        {
            Plugin.Instance.Log.LogWarning($"New Player: {Player}");
            NetOutgoingMessage message = NetServer.CreateMessage();
            new LocalPlayerPacket() {Player = Player}.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, Local, NetDeliveryMethod.ReliableOrdered, 0);
        }
        public void SendSpawnPacketToLocal(NetConnection Local, string Player)
        {
            NetOutgoingMessage message = NetServer.CreateMessage();
            new PlayerSpawnPacket() { Player = Player }.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, Local, NetDeliveryMethod.ReliableOrdered, 0);

        }

        public void SendSpawnPacketToAll(List<NetConnection> all, string Player)
        {
            NetOutgoingMessage message = NetServer.CreateMessage();
            new PlayerSpawnPacket() { Player = Player }.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, all, NetDeliveryMethod.ReliableOrdered, 0);
        }
    
    }
}