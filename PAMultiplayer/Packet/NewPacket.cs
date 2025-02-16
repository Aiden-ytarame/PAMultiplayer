using System.IO;
using System;
using UnityEngine;

namespace PAMultiplayer.Packet;

public class NewPacket : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public NewPacket(PacketType packetType)
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
        
        _writer.Write((ushort)packetType);
    }

  
    public byte[] GetData(out int length)
    {
        length = (int)_stream.Length;
        return _stream.GetBuffer();
    }
    
 
    public void Write(ulong value) => _writer.Write(value);
    public void Write(uint value) => _writer.Write(value);
    public void Write(int value) => _writer.Write(value);
    public void Write(float value) => _writer.Write(value);

    public void Write(Vector2 value)
    {
        _writer.Write(value.x);
        _writer.Write(value.y);
    }
    
    public void Dispose()
    {
        _writer?.Dispose();
        _stream?.Dispose();
        GC.SuppressFinalize(this);
    }
}