using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AttributeNetworkWrapperV2;
using Steamworks;
using Steamworks.Data;
using SendType = AttributeNetworkWrapperV2.SendType;

namespace PAMultiplayer.AttributeNetworkWrapperOverrides;

public class FacepunchSocketsTransport : Transport, ISocketManager, IConnectionManager
{
    private SocketManager _server;
    private ConnectionManager _client;
    
    internal readonly Dictionary<int, Connection?> IDToConnection = new();
    internal readonly Dictionary<ulong, int> SteamIdToNetId = new();

    private static byte[] _buffer = new byte[1024];

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
        foreach (var keyValuePair in IDToConnection)
        {
            if (keyValuePair.Value == steamConnection)
            {
                return keyValuePair.Key;
            }
        }
        return -1;
    }

    void AssureBufferSpace(int size)
    {
        if (_buffer.Length >= size || size <= 0)
        {
            return;
        }
        
        // taken from MemoryStream
        // Check for overflow
        if (size > _buffer.Length)
        {
            int newCapacity = Math.Max(size, 256);

            // We are ok with this overflowing since the next statement will deal
            // with the cases where _capacity*2 overflows.
            if (newCapacity < _buffer.Length * 2)
            {
                newCapacity = _buffer.Length * 2;
            }

            // We want to expand the array up to Array.MaxLength.
            // And we want to give the user the value that they asked for
            if ((uint)(_buffer.Length * 2) > 999999)
            {
                newCapacity = Math.Max(size, 9999999);
            }

            byte[] newBuffer = new byte[newCapacity];
           
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
            
            _buffer = newBuffer;
        } 
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
            connection?.Close();
        }
    }

    public override void SendMessageToServer(ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
        var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable : Steamworks.Data.SendType.Unreliable;
        
        _client?.Connection.SendMessage(data.Array, data.Offset, data.Count, steamSendType);
    }

    public override void SendMessageToClient(int connectionId, ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
        if (IDToConnection.TryGetValue(connectionId, out var connection))
        {
            var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable | Steamworks.Data.SendType.NoNagle : Steamworks.Data.SendType.Unreliable | Steamworks.Data.SendType.NoDelay;
            
            connection?.SendMessage(data.Array, data.Offset, data.Count, steamSendType);
        }
    }

    public override void Shutdown()
    {
        _server?.Close();
        _client?.Close();
        IDToConnection.Clear();
        SteamIdToNetId.Clear();
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

        AssureBufferSpace(size);
        Marshal.Copy(data, _buffer, 0, size);
        ArraySegment<byte> dataArr = new ArraySegment<byte>(_buffer, 0, size);
        
        OnServerDataReceived?.Invoke(new ClientNetworkConnection(id, identity.SteamId.ToString()), dataArr);
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
        
        AssureBufferSpace(size);
        Marshal.Copy(data, _buffer, 0, size);
        ArraySegment<byte> dataArr = new ArraySegment<byte>(_buffer, 0, size);
        
        OnClientDataReceived?.Invoke(dataArr);
    }
}
