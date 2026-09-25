using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AttributeNetworkWrapperV2;
using PAMultiplayer;
using PAMultiplayer.Managers;
using Steamworks;
using UnityEngine;

namespace PAMultiplayer.AttributeNetworkWrapperOverrides;

public static class AttrWrapperExtension
{
    public static void WriteSteamId(this NetworkWriter writer, SteamId steamId) => writer.Write(steamId.Value);
    public static SteamId ReadSteamId(this NetworkReader reader) => reader.ReadUInt64();

    public static void WriteVector2(this NetworkWriter writer, Vector2 vector2)
    {
        writer.Write(vector2.x);
        writer.Write(vector2.y);
    }

    public static Vector2 ReadVector2(this NetworkReader reader)
    {
        return new Vector2(reader.ReadSingle(), reader.ReadSingle());
    }

    public static void WriteUlongs(this NetworkWriter writer, List<ulong> ulongs)
    {
        writer.Write(ulongs.Count);
        foreach (var @ulong in ulongs)
        {
            writer.Write(@ulong);
        }
    }

    public static List<ulong> ReadUlongs(this NetworkReader reader)
    {
        int count = reader.ReadInt32();
        List<ulong> ulongs = new List<ulong>(count);
        
        for (int i = 0; i < count; i++)
        {
            ulongs.Add(reader.ReadUInt64());
        }
        
        return ulongs;
    }

    public static void WriteSongData(this NetworkWriter writer, ReadOnlySpan<short> songData)
    {
        unsafe
        {
            fixed (short* ptr = songData)
            {
                byte* data = (byte*)ptr;
                int size = sizeof(short) * songData.Length;
                writer.Write(size);
                writer.WriteBytes(data, size);
            }
        }
    }

    public static ReadOnlySpan<short> ReadSongData(this NetworkReader reader)
    {
        int count = reader.ReadInt32();
        unsafe
        {
            ReadOnlySpan<byte> bytes = reader.ReadBytes(count);
            if (bytes != null && bytes.Length >= 2)
            {
                fixed (byte* ptr = bytes)
                {
                    return new Span<short>(ptr, Mathf.FloorToInt((float)count / sizeof(short)));
                }
            }
           
        }

        return new Span<short>();
    }
    
    public static void WriteVersion(this NetworkWriter writer, Version version)
    {
        writer.Write(version.Major);
        writer.Write(version.Minor);
        writer.Write(version.Build);
        writer.Write(version.Revision);
    }

    public static Version ReadVersion(this NetworkReader reader)
    {
        return new Version(Math.Max(reader.ReadInt32(), 0), Math.Max(reader.ReadInt32(), 0), Math.Max(reader.ReadInt32(), 0), Math.Max(reader.ReadInt32(), 0));
    }
    
    public static bool TryGetSteamId(this ClientNetworkConnection conn, out SteamId steamId) => GlobalsManager.ConnIdToSteamId.TryGetValue(conn.ConnectionId, out steamId);
}