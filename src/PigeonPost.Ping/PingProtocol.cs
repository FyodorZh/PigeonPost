using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace PigeonPost.Ping;

internal static class PingProtocol
{
    public const int HandshakeSize = 16;
    public const int HeaderSize = 12;
    public const int MaxPayloadSize = 1024 * 1024;
    public const byte Version = 1;

    private static ReadOnlySpan<byte> Magic => "PPNG"u8;

    public static byte[] CreateHandshake(ulong clientId)
    {
        return CreateToken(clientId);
    }

    public static bool TryDecodeHandshake(ReadOnlySpan<byte> data, out ulong clientId)
    {
        return TryDecodeToken(data, out clientId);
    }

    public static byte[] CreateHandshakeAck(ulong sessionId)
    {
        return CreateToken(sessionId);
    }

    public static bool TryDecodeHandshakeAck(ReadOnlySpan<byte> data, out ulong sessionId)
    {
        return TryDecodeToken(data, out sessionId);
    }

    public static string DescribeTokenFailure(ReadOnlySpan<byte> data)
    {
        if (data.Length != HandshakeSize)
        {
            return "bad_length";
        }

        if (!data[..4].SequenceEqual(Magic))
        {
            return "bad_magic";
        }

        if (data[4] != Version)
        {
            return "version_mismatch";
        }

        if (data[5] != 0 || data[6] != 0 || data[7] != 0)
        {
            return "bad_reserved";
        }

        return "ok";
    }

    public static int GetTokenVersion(ReadOnlySpan<byte> data)
    {
        return data.Length > 4 ? data[4] : -1;
    }

    public static ulong CreateId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        ulong id = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return id == 0 ? 1UL : id;
    }

    public static string FormatId(ulong id)
    {
        return id.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static byte[] CreateToken(ulong id)
    {
        byte[] token = new byte[HandshakeSize];
        Magic.CopyTo(token);
        token[4] = Version;
        BinaryPrimitives.WriteUInt64BigEndian(token.AsSpan(8, 8), id);
        return token;
    }

    private static bool TryDecodeToken(ReadOnlySpan<byte> data, out ulong id)
    {
        id = 0;

        if (data.Length != HandshakeSize)
        {
            return false;
        }

        if (!data[..4].SequenceEqual(Magic) || data[4] != Version)
        {
            return false;
        }

        if (data[5] != 0 || data[6] != 0 || data[7] != 0)
        {
            return false;
        }

        id = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(8, 8));
        return true;
    }

    public static byte[] EncodeFrame(uint sequence, long timestamp, ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[HeaderSize + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(0, 4), sequence);
        BinaryPrimitives.WriteInt64BigEndian(frame.AsSpan(4, 8), timestamp);
        payload.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    public static bool TryDecodeFrame(ReadOnlySpan<byte> frame, out uint sequence, out long timestamp,
        out ReadOnlySpan<byte> payload)
    {
        sequence = 0;
        timestamp = 0;
        payload = default;

        if (frame.Length < HeaderSize)
        {
            return false;
        }

        int payloadLength = frame.Length - HeaderSize;
        if (payloadLength > MaxPayloadSize)
        {
            return false;
        }

        sequence = BinaryPrimitives.ReadUInt32BigEndian(frame[..4]);
        timestamp = BinaryPrimitives.ReadInt64BigEndian(frame.Slice(4, 8));
        payload = frame[HeaderSize..];
        return true;
    }

    public static byte[] CreateReversedReply(ReadOnlySpan<byte> frame)
    {
        byte[] reply = new byte[frame.Length];
        frame[..HeaderSize].CopyTo(reply);

        int payloadLength = frame.Length - HeaderSize;
        for (int i = 0; i < payloadLength; i++)
        {
            reply[HeaderSize + i] = frame[HeaderSize + payloadLength - 1 - i];
        }

        return reply;
    }

    public static bool IsReversedCopy(ReadOnlySpan<byte> original, ReadOnlySpan<byte> candidate)
    {
        if (original.Length != candidate.Length)
        {
            return false;
        }

        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] != candidate[original.Length - 1 - i])
            {
                return false;
            }
        }

        return true;
    }
}
