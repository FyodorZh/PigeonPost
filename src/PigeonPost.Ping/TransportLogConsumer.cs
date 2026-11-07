using System;
using System.Globalization;
using System.IO;
using System.Text;
using Scriba;

namespace PigeonPost.Ping;

internal sealed class TransportLogConsumer : MultiRefLogConsumer
{
    private readonly string _role;
    private readonly bool _includeTags;
    private readonly object _lock = new();

    public TransportLogConsumer(string role, bool includeTags)
    {
        _role = role;
        _includeTags = includeTags;
    }

    public override void Message(MessageData logMessage)
    {
        var builder = new StringBuilder(192);
        builder.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(" role=").Append(_role);
        builder.Append(" event=transport level=").Append(logMessage.Severity.ToLowerInvariant());
        builder.Append(" message=");
        PingLog.AppendQuoted(builder, ReadMessage(logMessage));

        if (_includeTags)
        {
            string tags = ReadTags(logMessage);
            if (tags.Length > 0)
            {
                builder.Append(" tags=");
                PingLog.AppendQuoted(builder, tags);
            }
        }

        if (logMessage.StackTraceDepth > 0)
        {
            var stackWriter = new StringWriter(CultureInfo.InvariantCulture);
            logMessage.WriteStackTrace("\t", stackWriter);
            builder.Append(" stack=");
            PingLog.AppendQuoted(builder, stackWriter.ToString());
        }

        lock (_lock)
        {
            Console.Error.WriteLine(builder.ToString());
        }
    }

    private static string ReadMessage(MessageData logMessage)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        logMessage.WriteMessageTo(writer);
        string message = writer.ToString().Trim();
        if (message.Length >= 2 && message[0] == '"' && message[^1] == '"')
        {
            message = message[1..^1];
        }
        return message;
    }

    private static string ReadTags(MessageData logMessage)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        logMessage.WriteTagsTo(writer);
        return writer.ToString().TrimEnd(' ', ';');
    }
}
