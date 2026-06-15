using System;

namespace PigeonPost.Bridge.Protocol;

public sealed record ClientId
{
    public string Value { get; }

    public ClientId(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
    }

    public override string ToString() => Value;
}
