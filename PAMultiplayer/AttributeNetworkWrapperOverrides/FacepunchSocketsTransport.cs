using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using AttributeNetworkWrapper.Core;
using Steamworks;
using Steamworks.Data;
using SendType = AttributeNetworkWrapper.Core.SendType;

namespace PAMultiplayer.AttributeNetworkWrapperOverrides;

public class FacepunchSocketsTransport : Transport, ISocketManager, IConnectionManager
{
    private SocketManager server;
    private ConnectionManager client;
    
    Dictionary<int, Connection> _idToConnection = new();
    public readonly Dictionary<ulong, int> SteamIdToNetId = new();
    
    protected static byte[] _buffer = new byte[1024];

    int GetNextConnectionId()
    {
        int id = 0;
        while (_idToConnection.ContainsKey(id))
        {
            id++;
        }
        
        return id;
    }
    
    int GetIdFromSteamConnection(Connection steamConnection)
    {
        foreach (var keyValuePair in _idToConnection)
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
            if ((uint)(_buffer.Length * 2) > Array.MaxLength)
            {
                newCapacity = Math.Max(size, Array.MaxLength);
            }

            byte[] newBuffer = new byte[newCapacity];
           
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _buffer.Length);
            
            _buffer = newBuffer;
        } 
    }

    public void Receive()
    {
        server?.Receive();
        client?.Receive();
    }
    //Transport
    public override void ConnectClient(string address)
    {
        _idToConnection.Clear();
        if (ulong.TryParse(address, out var id))
        {
            client = SteamNetworkingSockets.ConnectRelay(id, 0, this);
            return;
        }
        
        throw new ArgumentException($"{address} is not a valid SteamID");
    }

    public override void StopClient()
    {
        IsActive = false;
        client?.Close();
    }

    public override void StartServer()
    {
        _idToConnection.Clear();
        server = SteamNetworkingSockets.CreateRelaySocket(0, this);
        IsActive = true;
    }

    public override void StopServer()
    {
        IsActive = false;
        server?.Close();
    }

    public override void KickConnection(int connectionId)
    {
        if (_idToConnection.TryGetValue(connectionId, out var connection))
        {
            connection.Close();
        }
    }

    public override void SendMessageToServer(ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
        var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable : Steamworks.Data.SendType.Unreliable;
        
        client?.Connection.SendMessage(data.Array, data.Offset, data.Count, steamSendType);
    }

    public override void SendMessageToClient(int connectionId, ArraySegment<byte> data, SendType sendType = SendType.Reliable)
    {
        if (_idToConnection.TryGetValue(connectionId, out var connection))
        {
            var steamSendType = sendType == SendType.Reliable ? Steamworks.Data.SendType.Reliable : Steamworks.Data.SendType.Unreliable;
            
            connection.SendMessage(data.Array, data.Offset, data.Count, steamSendType);
        }
    }

    public override void Shutdown()
    {
        server?.Close();
        client?.Close();
        _idToConnection.Clear();
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
        
        _idToConnection.Add(id, connection);
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
        
        _idToConnection.Remove(id);
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

    public void OnConnecting(ConnectionInfo info) { }

    public void OnConnected(ConnectionInfo info)
    {
        IsActive = true;
        OnClientConnected?.Invoke(new ServerNetworkConnection(info.Identity.SteamId.ToString()));
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
