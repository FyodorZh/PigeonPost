using System;
using System.Globalization;
using System.IO;
using System.Text;
using Pontifex;
using Pontifex.StopReasons;

namespace PigeonPost.Ping;

internal sealed class PingLog
{
    private readonly string _role;
    private readonly object _lock = new();

    public PingLog(string role, bool verbose)
    {
        _role = role;
        Verbose = verbose;
    }

    public bool Verbose { get; }

    public void Event(string name, params (string Key, object? Value)[] fields)
    {
        Write(Console.Out, null, name, fields);
    }

    public void Warn(string name, params (string Key, object? Value)[] fields)
    {
        Write(Console.Error, "warn", name, fields);
    }

    public void Error(string name, params (string Key, object? Value)[] fields)
    {
        Write(Console.Error, "error", name, fields);
    }

    public void Raw(string text)
    {
        lock (_lock)
        {
            Console.Out.WriteLine(text);
        }
    }

    public static (string Key, object? Value)[] ReasonFields(StopReason reason)
    {
        StopReason cause = Unwrap(reason);
        return
        [
            ("reason_type", reason.Type),
            ("reason_source", reason.Source),
            ("cause_type", cause.Type),
            ("cause_source", cause.Source),
            ("reason_json", reason.ToString())
        ];
    }

    internal static StopReason Unwrap(StopReason reason)
    {
        while (true)
        {
            if (reason is Induced induced)
            {
                reason = induced.Cause;
                continue;
            }

            if (reason is ChainFail chain)
            {
                reason = chain.Reason;
                continue;
            }

            return reason;
        }
    }

    public static void AppendValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append('-');
                break;
            case bool boolean:
                builder.Append(boolean ? "true" : "false");
                break;
            case double number:
                builder.Append(number.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case float number:
                builder.Append(number.ToString("0.###", CultureInfo.InvariantCulture));
                break;
            case IFormattable formattable:
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                AppendQuoted(builder, value.ToString() ?? string.Empty);
                break;
        }
    }

    public static void AppendQuoted(StringBuilder builder, string value)
    {
        bool quote = value.Length == 0;
        if (!quote)
        {
            foreach (char ch in value)
            {
                if (char.IsWhiteSpace(ch) || ch == '=' || ch == '"' || ch == '\\' || ch == '{' || ch == '}')
                {
                    quote = true;
                    break;
                }
            }
        }

        if (!quote)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }
        builder.Append('"');
    }

    private void Write(TextWriter writer, string? level, string name, (string Key, object? Value)[] fields)
    {
        var builder = new StringBuilder(160);
        builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" role=").Append(_role);
        builder.Append(" event=").Append(name);

        if (level != null)
        {
            builder.Append(" level=").Append(level);
        }

        foreach ((string key, object? value) in fields)
        {
            builder.Append(' ').Append(key).Append('=');
            AppendValue(builder, value);
        }

        lock (_lock)
        {
            writer.WriteLine(builder.ToString());
        }
    }
}
