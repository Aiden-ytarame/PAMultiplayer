using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using PAMultiplayer;
using PAMultiplayer.Managers;
using PAMultiplayer.Packets;
using PAMultiplayer.Patch;
using Rewired;

namespace PAMultiplayer.Server
{
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
            if(NetServer.UPnP.ForwardPort(int.Parse(StaticManager.ServerPort), "GameStuff"))          
                Plugin.Inst.Log.LogWarning("UPnP SUCCESS");       

            thread = new Thread(Listen);
            thread.Start();
        }
        void Listen()
        {
            Plugin.Inst.Log.LogWarning("StartedServer");
            while (true)
            {
                NetIncomingMessage message;
                while ((message = NetServer.ReadMessage()) != null)
                {
                        
                    List<NetConnection> all = NetServer.Connections;
                    switch (message.MessageType)
                    {
                        case NetIncomingMessageType.StatusChanged:
                            NetConnectionStatus status = (NetConnectionStatus)message.ReadByte();
                            string reason = message.ReadString();

              
                                if (status == NetConnectionStatus.Connected)
                            {
                                var player = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                                Plugin.Inst.Log.LogWarning($"Player Connected: {player}");
                                Players.Add(player);

                                SendLocalPlayerPacket(message.SenderConnection, player);
                                SpawnPlayers(all, message.SenderConnection, player);
                            }
                            if (status == NetConnectionStatus.Disconnected)
                            {
                                var _player = NetUtility.ToHexString(message.SenderConnection.RemoteUniqueIdentifier);
                                Players.Remove(_player);

                                NetOutgoingMessage outMessage = NetServer.CreateMessage();
                                new PlayerDisconnectPacket() { Player = _player }.PacketToNetOutgoing(outMessage);
                                if (NetServer.Connections.Count < 1)
                                {
                                    continue;
                                }
                                
                                NetServer.SendMessage(outMessage, all, NetDeliveryMethod.ReliableOrdered, 0);
                            }
                            break;
                        case NetIncomingMessageType.Data:
                            if (NetServer.Connections.Count < 1)
                            {
                                continue;
                            }
                            
                            string TypeStr = message.ReadString();
                            Type PacketType = Type.GetType(TypeStr);

                            try
                            {
                                ((Packet)Activator.CreateInstance(PacketType)).ServerProcessPacket(message);
                            }
                            catch (Exception ex)
                            {
                                Plugin.Inst.Log.LogError($"SERVER: Unhandled packet. {TypeStr}");
                                Plugin.Inst.Log.LogError(ex);
                            }
                            break;

                        case NetIncomingMessageType.DebugMessage:
                        case NetIncomingMessageType.ErrorMessage:
                        case NetIncomingMessageType.WarningMessage:
                        case NetIncomingMessageType.VerboseDebugMessage:
                            string text = message.ReadString();

                            Plugin.Inst.Log.LogWarning($"DEBYUG: {text}");
                            break;
                        default:
                            Plugin.Inst.Log.LogWarning($"Unhandled type: {message.MessageType} {message.LengthBytes} bytes {message.DeliveryMethod}|{message.SequenceChannel}");
                            break;
                    }
                    NetServer.Recycle(message);
                }
            }
        }

        public void SpawnPlayers(List<NetConnection> netConnections, NetConnection Local, string _player)
        {
            Plugin.Inst.Log.LogWarning($"SERVER: Player Spawned: {_player}");
            string steamName = null;

            //spawn all player on newly connected player
            foreach (NetConnection connection in netConnections)
            {
                string player = NetUtility.ToHexString(connection.RemoteUniqueIdentifier);

                steamName = connection.RemoteHailMessage.PeekString();         
                SendSpawnPacketToLocal(Local, player, steamName);
            }
            steamName = Local.RemoteHailMessage.PeekString();

            SendSpawnPacketToAll(netConnections, _player, steamName);
        }

        public void SendLocalPlayerPacket(NetConnection Local, string _player)
        {
            Plugin.Inst.Log.LogWarning($"New Player: {_player}");
            NetOutgoingMessage message = NetServer.CreateMessage();
            new LocalPlayerPacket() { Player = _player, isLobby = StaticManager.IsLobby }.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, Local, NetDeliveryMethod.ReliableOrdered, 0);
        }
        public void SendSpawnPacketToLocal(NetConnection Local, string _player, string _steamName = null)
        {
            NetOutgoingMessage message = NetServer.CreateMessage();
            new PlayerSpawnPacket() { Player = _player,  SteamName = _steamName}.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, Local, NetDeliveryMethod.ReliableOrdered, 0);

        }

        public void SendSpawnPacketToAll(List<NetConnection> all, string _player, string _steamName = null)
        {
            NetOutgoingMessage message = NetServer.CreateMessage();
            new PlayerSpawnPacket() { Player = _player, SteamName = _steamName }.PacketToNetOutgoing(message);
            NetServer.SendMessage(message, all, NetDeliveryMethod.ReliableOrdered, 0);
        }

        public void SendStartLevel()
        {
            NetOutgoingMessage message = NetServer.CreateMessage();
            new StartLevelPacket().PacketToNetOutgoing(message);
            NetServer.SendMessage(message, NetServer.Connections, NetDeliveryMethod.ReliableOrdered, 0);
        }

    }
}
