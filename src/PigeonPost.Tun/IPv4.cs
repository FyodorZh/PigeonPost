using System;

namespace PigeonPost.Tun;

public readonly struct IPv4 : IEquatable<IPv4>
{
    private readonly uint _value;

    public uint Value => _value;

    public byte A => (byte)(_value >> 24);
    public byte B => (byte)(_value >> 16);
    public byte C => (byte)(_value >> 8);
    public byte D => (byte)_value;

    public IPv4(byte a, byte b, byte c, byte d)
    {
        _value = ((uint)a << 24) | ((uint)b << 16) | ((uint)c << 8) | d;
    }

    public IPv4(uint networkByteOrderValue)
    {
        _value = networkByteOrderValue;
    }

    public IPv4(byte[] bytes, int offset = 0)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _value = ((uint)bytes[offset] << 24)
               | ((uint)bytes[offset + 1] << 16)
               | ((uint)bytes[offset + 2] << 8)
               | bytes[offset + 3];
    }

    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 4)
            throw new ArgumentException("Destination span must be at least 4 bytes.", nameof(destination));

        destination[0] = A;
        destination[1] = B;
        destination[2] = C;
        destination[3] = D;
    }

    public byte[] ToBytes()
    {
        return [A, B, C, D];
    }

    public static IPv4 Parse(string s)
    {
        if (s == null) throw new ArgumentNullException(nameof(s));

        var parts = s.Split('.');
        if (parts.Length != 4)
            throw new FormatException($"Invalid IPv4 string: '{s}'.");

        return new IPv4(
            byte.Parse(parts[0]),
            byte.Parse(parts[1]),
            byte.Parse(parts[2]),
            byte.Parse(parts[3]));
    }

    public static bool TryParse(string s, out IPv4 result)
    {
        result = default;
        if (s == null!) return false;

        var parts = s.Split('.');
        if (parts.Length != 4) return false;

        if (!byte.TryParse(parts[0], out var a)) return false;
        if (!byte.TryParse(parts[1], out var b)) return false;
        if (!byte.TryParse(parts[2], out var c)) return false;
        if (!byte.TryParse(parts[3], out var d)) return false;

        result = new IPv4(a, b, c, d);
        return true;
    }

    public override string ToString() => $"{A}.{B}.{C}.{D}";

    public bool Equals(IPv4 other) => _value == other._value;

    public override bool Equals(object? obj) => obj is IPv4 other && Equals(other);

    public override int GetHashCode() => (int)_value;

    public static bool operator ==(IPv4 left, IPv4 right) => left.Equals(right);

    public static bool operator !=(IPv4 left, IPv4 right) => !left.Equals(right);

    public static implicit operator uint(IPv4 ip) => ip._value;

    public static explicit operator IPv4(uint value) => new IPv4(value);

    public static readonly IPv4 Any = new IPv4(0, 0, 0, 0);
    public static readonly IPv4 Loopback = new IPv4(127, 0, 0, 1);
    public static readonly IPv4 Broadcast = new IPv4(255, 255, 255, 255);
}
