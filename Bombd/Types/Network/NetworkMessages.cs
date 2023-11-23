﻿using Bombd.Helpers;
using Bombd.Serialization;
using Bombd.Types.Network.Messages;

namespace Bombd.Types.Network;

public class NetworkMessages
{
    public const string SimServerName = "SimServer";
    private const int HeaderSize = 0x8;
    public static readonly int SimServerUID = CryptoHelper.StringHash32(SimServerName);

    public static ArraySegment<byte> Pack(NetworkWriter writer, INetworkMessage message)
    {
        writer.Reset();

        writer.Write((byte)message.Type);
        writer.Offset += 3;
        writer.Write(SimServerUID);
        writer.Write(message);

        ArraySegment<byte> payload = writer.ToArraySegment();
        int size = payload.Count;
        payload[2] = (byte)(size >> 8);
        payload[3] = (byte)(size >> 0);

        return payload;
    }

    public static ArraySegment<byte> PackData(NetworkWriter writer, ArraySegment<byte> message, NetMessageType type)
    {
        writer.Reset();

        writer.Write((byte)type);
        writer.Offset += 3;
        writer.Write(SimServerUID);
        writer.Write(message);

        ArraySegment<byte> payload = writer.ToArraySegment();
        int size = payload.Count;
        payload[2] = (byte)(size >> 8);
        payload[3] = (byte)(size >> 0);

        return payload;
    }

    public static ArraySegment<byte> PackInt(NetworkWriter writer, int value, NetMessageType type)
    {
        writer.Reset();

        writer.Write((byte)type);
        writer.Offset += 3;
        writer.Write(SimServerUID);
        writer.Write(value);
        
        ArraySegment<byte> payload = writer.ToArraySegment();
        int size = payload.Count;
        payload[2] = (byte)(size >> 8);
        payload[3] = (byte)(size >> 0);

        return payload;
    }
    
    public static bool Unpack(NetworkReader reader, Platform platform, out NetMessageType type, out int sender, out ArraySegment<byte> message)
    {
        message = default;
        type = 0;
        sender = 0;
        
        if (reader.Capacity < HeaderSize) return false;
        
        type = (NetMessageType)reader.ReadInt8();
        
        // The extra size in ModNation is just random bytes, probably unused
        // in Karting it is actually used.
        byte extraSize = reader.ReadInt8();
        int size = reader.ReadUInt16();
        if (platform == Platform.Karting)
            size |= (extraSize << 0x10);
        
        sender = reader.ReadInt32();

        // Size includes the header bytes
        size -= HeaderSize;
        
        // Messages should contain exactly as much data as their header dictates
        if (size != reader.Remaining)
            return false;
        
        message = reader.ReadSegment(size);
        
        return true;
    }
}