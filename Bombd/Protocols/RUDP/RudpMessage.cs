﻿using Bombd.Extensions;
using Bombd.Helpers;

namespace Bombd.Protocols.RUDP;

public class RudpMessage
{
    private readonly byte[] _data = new byte[1040];
    private int _offset;
    internal PacketType Protocol;
    internal uint Sequence;
    internal int Timestamp;

    public void EncodeAck(PacketType protocol, uint sequence)
    {
        Protocol = PacketType.Ack;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.Ack);
        _offset += _data.WriteUint8(_offset, (byte)protocol);
        _offset += _data.WriteUint16BE(_offset, 0);
        _offset += _data.WriteUint32BE(_offset, sequence);
        _offset += _data.WriteInt32BE(_offset, TimeHelper.LocalTime);
        _offset += _data.WriteUint32BE(_offset, 0);
    }

    public void EncodeNetcode(ushort groupId, uint sequence, ArraySegment<byte> data, bool complete)
    {
        Protocol = PacketType.ReliableNetcodeData;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.ReliableNetcodeData);
        _offset += _data.WriteBoolean(_offset, complete);
        _offset += _data.WriteUint16BE(_offset, groupId);
        _offset += _data.WriteUint32BE(_offset, sequence);

        int checksumOffset = _offset;
        _offset += _data.WriteUint32BE(_offset, 0);

        _offset += _data.WriteUint32BE(_offset, 0);
        _offset += _data.Write(_offset, data);

        int checksum = CryptoHelper.GetMD532(GetArraySegment(), CryptoHelper.Salt);
        _data.WriteInt32BE(checksumOffset, checksum);
    }

    public void EncodeGamedata(ushort groupId, uint sequence, ushort groupSize, ArraySegment<byte> data, bool complete)
    {
        Protocol = PacketType.ReliableGameData;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.ReliableGameData);
        _offset += _data.WriteUint8(_offset, 0xFE);
        _offset += _data.WriteUint8(_offset, 0x2);
        _offset += _data.WriteBoolean(_offset, complete);
        _offset += _data.WriteUint32BE(_offset, sequence);
        _offset += _data.WriteUint16BE(_offset, groupId);
        _offset += _data.WriteUint16BE(_offset, groupSize);

        int checksumOffset = _offset;
        _offset += _data.WriteUint16BE(_offset, 0);

        _offset += _data.WriteUint16BE(_offset, (ushort)data.Count);
        _offset += _data.Write(_offset, data);

        ushort checksum = CryptoHelper.GetMD516(GetArraySegment(), CryptoHelper.Salt);
        _data.WriteUint16BE(checksumOffset, checksum);
    }

    public void EncodeUnreliableGamedata(ArraySegment<byte> data)
    {
        Protocol = PacketType.UnreliableGameData;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.UnreliableGameData);
        _offset += _data.WriteUint8(_offset, 0xfe);
        _offset += _data.WriteUint16BE(_offset, 0xffff);
        _offset += _data.WriteUint16BE(_offset, (ushort)data.Count);

        int checksumOffset = _offset;
        _offset += _data.WriteUint16BE(_offset, 0);

        _offset += _data.Write(_offset, data);

        ushort checksum = CryptoHelper.GetMD516(GetArraySegment(), CryptoHelper.Salt);
        _data.WriteUint16BE(checksumOffset, checksum);
    }

    public void EncodeVoipData(uint sequence, ArraySegment<byte> data)
    {
        Protocol = PacketType.VoipData;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.VoipData);
        _offset += _data.WriteUint8(_offset, 0xfe);
        _offset += _data.WriteUint16BE(_offset, 0xffff);
        _offset += _data.WriteUint32BE(_offset, sequence);
        _offset += _data.WriteUint32BE(_offset, 0);
        _offset += _data.WriteUint32BE(_offset, 0);
        _offset += _data.Write(_offset, data);
    }

    public void EncodeHandshake(int sessionId, int secret)
    {
        Protocol = PacketType.Handshake;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.Handshake);
        _offset += _data.WriteUint8(_offset, 0);

        int checksumOffset = _offset;
        _offset += _data.WriteUint16BE(_offset, 0);

        _offset += _data.WriteUint32BE(_offset, 0);
        _offset += _data.WriteInt32BE(_offset, sessionId);
        _offset += _data.WriteInt32BE(_offset, secret);
        _offset += _data.WriteUint32BE(_offset, 0);

        ushort checksum = CryptoHelper.GetMD516(GetArraySegment(), CryptoHelper.Salt);
        _data.WriteUint16BE(checksumOffset, checksum);
    }

    public void EncodeKeepAlive(uint sequence)
    {
        Protocol = PacketType.KeepAlive;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.KeepAlive);
        _offset += _data.WriteUint8(_offset, 0);
        _offset += _data.WriteUint16BE(_offset, 0);
        _offset += _data.WriteUint32BE(_offset, sequence);
        _offset += _data.WriteInt32BE(_offset, TimeHelper.LocalTime);
        _offset += _data.WriteUint32BE(_offset, 0);
    }

    public void EncodeReset(uint sequence, int secret)
    {
        Protocol = PacketType.Reset;
        Sequence = sequence;

        _offset += _data.WriteUint8(_offset, (byte)PacketType.KeepAlive);
        _offset += _data.WriteUint8(_offset, 0);
        _offset += _data.WriteUint16BE(_offset, 0);
        _offset += _data.WriteUint32BE(_offset, sequence);
        _offset += _data.WriteUint32BE(_offset, 0);
        _offset += _data.WriteInt32BE(_offset, secret);
        _offset += _data.WriteUint32BE(_offset, 0);
    }

    public ArraySegment<byte> GetArraySegment() => new(_data, 0, _offset);

    public void Reset()
    {
        _offset = 0;
        Timestamp = 0;
        Sequence = 0;
    }
}