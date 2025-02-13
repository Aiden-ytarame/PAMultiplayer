using System.IO;
using System;
using UnityEngine;

namespace PAMultiplayer.Packet;

public class NewPacket
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public NewPacket(PacketType packetType)
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
        
        _writer.Write((ushort)packetType);
    }

    ~NewPacket()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
    
    public unsafe IntPtr GetData(out int length)
    {
        length = (int)_stream.Length;
        fixed (byte* ptr = _stream.GetBuffer())
        {
            return (IntPtr)ptr;
        }
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
}