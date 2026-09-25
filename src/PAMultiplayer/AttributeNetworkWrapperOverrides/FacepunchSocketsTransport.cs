using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AttributeNetworkWrapperV2;
using PAMultiplayer.Managers;
using PAMultiplayer.UI;
using Steamworks;
using Steamworks.Data;
using SendType = AttributeNetworkWrapperV2.SendType;

namespace PAMultiplayer.AttributeNetworkWrapperOverrides;

public class FacepunchSocketsTransport : Transport, ISocketManager, IConnectionManager
{
    private const int KickEndReasonCode = 1111;
    
    private SocketManager _server;
    private ConnectionManager _client;
    
    internal readonly Dictionary<int, Connection?> IDToConnection = new();
    internal readonly Dictionary<Connection, int> ConnectionToID = new();
    internal readonly Dictionary<ulong, int> SteamIdToNetId = new();

    public int GetNextConnectionId()
    {
        int id = 0;
        while (IDToConnection.ContainsKey(id))
        {
            id++;
        }
        
        return id;
    }
    
    int GetIdFromSteamConnection(Connection steamConnection)
    {
        return ConnectionToID.GetValueOrDefault(steamConnection, -1);
    }

    public void Receive()
    {
        _server?.Receive();
        _client?.Receive();
    }

    public int GetPing()
    {
        if (IsServer)
        {
            return 0;
        }

        return _client?.Connection.QuickStatus().Ping ?? 9999;
    }
    //Transport
    public override void ConnectClient(string address)
    {
        IDToConnection.Clear();
        SteamIdToNetId.Clear();
        ConnectionToID.Clear();
        if (ulong.TryParse(address, out var id))
        {
            _client = SteamNetworkingSockets.ConnectRelay(id, 0, this);
            IsActive = true;
            OnClientConnected?.Invoke(new ServerNetworkConnection(id.ToString()));
            return;
        }
        
        throw new ArgumentException($"{address} is not a valid SteamID");
    }

    public override void StopClient()
    {
        IsActive = false;
        _client?.Close();
    }

    public override void StartServer()
    {
        IDToConnection.Clear();
        SteamIdToNetId.Clear();
        _server = SteamNetworkingSockets.CreateRelaySocket(0, this);
        IsActive = true;
    }

    public override void StopServer()
    {
        IsActive = false;
        _server?.Close();
    }

    public override void KickConnection(int connectionId)
    {
        if (IDToConnection.TryGetValue(connectionId, out var connection))
        {
            connection?.Close(false, KickEndReasonCode);
        }
    }

    public override void SendMessageToServer(ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable)
    {
        var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable : Steamworks.Data.SendType.Unreliable;
        unsafe
        {
            fixed (byte* numPtr = data)
            {
                _client?.Connection.SendMessage((IntPtr) numPtr, data.Length, steamSendType);
            }
        }
    }

    public override void SendMessageToClient(int connectionId, ReadOnlySpan<byte> data, SendType sendType = SendType.Reliable)
    {
        if (IDToConnection.TryGetValue(connectionId, out var connection))
        {
            var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable | Steamworks.Data.SendType.NoNagle : Steamworks.Data.SendType.Unreliable | Steamworks.Data.SendType.NoDelay;
            unsafe
            {
                fixed (byte* numPtr = data)
                {
                    connection?.SendMessage((IntPtr) numPtr, data.Length, steamSendType);
                }
            }
        }
    }

    public override void Shutdown()
    {
        _server?.Close();
        _client?.Close();
        IDToConnection.Clear();
        SteamIdToNetId.Clear();
        ConnectionToID.Clear();
        IsActive = false;
    }

    //SocketManager (server)
    public void OnConnecting(Connection connection, ConnectionInfo info)
    {
        connection.Accept();
        PAM.Logger.LogInfo($"Player {info.Identity.SteamId} is connecting to game server.");
    }

    public void OnConnected(Connection connection, ConnectionInfo info)
    {
        int id = GetNextConnectionId();
        
        IDToConnection.Add(id, connection);
        ConnectionToID.Add(connection, id);
        SteamIdToNetId.Add(info.Identity.SteamId, id);
        
        OnServerClientConnected?.Invoke(new ClientNetworkConnection(id, info.Identity.SteamId.ToString()));
    }

    public void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        connection.Close();

        if (!SteamIdToNetId.TryGetValue(info.Identity.SteamId, out var id))
        {
            return;
        }
        
        IDToConnection.Remove(id);
        ConnectionToID.Remove(connection);
        SteamIdToNetId.Remove(info.Identity.SteamId);
        OnServerClientDisconnected?.Invoke(new ClientNetworkConnection(id, info.Identity.SteamId.ToString()));

    }

    public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime,
        int channel)
    {
        int id = GetIdFromSteamConnection(connection);
        if (id == -1)
        {
            PAM.Logger.LogError("Received data from someone not in the id to connection list");
            return;
        }

        if (size < 2)
        {
            PAM.Logger.LogError("Received too little data, disconnecting");
            connection.Close();
            return;
        }

        if (size > 524288)
        {
            PAM.Logger.LogError("Received too much data from someone, disconnecting");
            connection.Close();
            return;
        }

        unsafe
        {
            try
            {
                OnServerDataReceived?.Invoke(new ClientNetworkConnection(id, identity.SteamId.ToString()), new ReadOnlySpan<byte>(data.ToPointer(), size));
            }
            catch (Exception e)
            {
                PAM.Logger.LogError(e);
            }
        }
    }
    
    
    // ConnectionManager (client)

    public void OnConnecting(ConnectionInfo info)
    {
      
    }

    public void OnConnected(ConnectionInfo info)
    {
        
    }

    public void OnDisconnected(ConnectionInfo info)
    {
        IsActive = false;
        OnClientDisconnected?.Invoke();
     
        switch (info.State)
        {
            case ConnectionState.ClosedByPeer when info.EndReason == (NetConnectionEnd)KickEndReasonCode:
                ErrorScreen.CreateErrorScreen("You were kicked from the lobby.");
                return;
            case ConnectionState.ClosedByPeer:
                ErrorScreen.CreateErrorScreen("Server has been closed.");
                break;
            case ConnectionState.Dead or ConnectionState.ProblemDetectedLocally:
                ErrorScreen.CreateErrorScreen("Connection lost.");
                break;
        }
    }

    public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        if (size < 2)
        {
            PAM.Logger.LogError("Received too little data");
            return;
        }

        if (size > 524288)
        {
            PAM.Logger.LogError("Received too much data from host");
            return;
        }
        
        unsafe
        {
            try
            {
                OnClientDataReceived?.Invoke(new ReadOnlySpan<byte>(data.ToPointer(), size));
            }
            catch (Exception e)
            {
                PAM.Logger.LogError(e);
            }
        }
    }
}
