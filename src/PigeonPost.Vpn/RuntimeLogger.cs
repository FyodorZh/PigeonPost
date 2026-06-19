using System;
using System.IO;
using Scriba;
using Scriba.JsonFactory;

namespace PigeonPost.Vpn;

internal sealed class RuntimeLogger : ILoggerExt
{
    private readonly Action<VpnLogEntry> _emit;
    private Severity _logFor = Severity.INFO;
    private Severity _ignoreStackFor = Severity.INFO;
    private string? _appId;
    private string? _machineName;
    private bool _logTime;

    public RuntimeLogger(Action<VpnLogEntry> emit)
    {
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
    }

    public Severity LogFor
    {
        get => _logFor;
        set => _logFor = value;
    }

    public Severity IgnoreStackFor
    {
        get => _ignoreStackFor;
        set => _ignoreStackFor = value;
    }

    public string? AppId
    {
        get => _appId;
        set => _appId = value;
    }

    public string? MachineName
    {
        get => _machineName;
        set => _machineName = value;
    }

    public bool LogTime
    {
        get => _logTime;
        set => _logTime = value;
    }

    public ITagList Tags => Scriba.VoidTagList.Instance;

    public void AddConsumer(ILogConsumer logConsumer) { }
    public void RemoveConsumer(ILogConsumer logConsumer) { }
    public void RemoveConsumerByType(Type type) { }

    public void d(string format, params object[] args)
    {
        var msg = args.Length > 0 ? string.Format(format, args) : format;
        _emit(new VpnLogEntry(DateTime.UtcNow, msg, VpnLogLevel.Info));
    }

    public void i(string format, params object[] args)
    {
        var msg = args.Length > 0 ? string.Format(format, args) : format;
        _emit(new VpnLogEntry(DateTime.UtcNow, msg, VpnLogLevel.Info));
    }

    public void w(string format, params object[] args)
    {
        var msg = args.Length > 0 ? string.Format(format, args) : format;
        _emit(new VpnLogEntry(DateTime.UtcNow, msg, VpnLogLevel.Warning));
    }

    public void e(string format, params object[] args)
    {
        var msg = args.Length > 0 ? string.Format(format, args) : format;
        _emit(new VpnLogEntry(DateTime.UtcNow, msg, VpnLogLevel.Error));
    }

    public void wtf(string message, Exception exception)
    {
        _emit(new VpnLogEntry(DateTime.UtcNow, $"{message}: {exception.Message}", VpnLogLevel.Error));
    }

    public void wtf(Exception exception)
    {
        _emit(new VpnLogEntry(DateTime.UtcNow, exception.Message, VpnLogLevel.Error));
    }

    public void json(IJsonObject message)
    {
        if (JsonUtils.Serialize(message, out var text))
            _emit(new VpnLogEntry(DateTime.UtcNow, text, VpnLogLevel.Info));
        else
            _emit(new VpnLogEntry(DateTime.UtcNow, "<json-serialize-failed>", VpnLogLevel.Warning));
    }

    public void Publish(MessageData message)
    {
        using var sw = new StringWriter();
        message.WriteMessageTo(sw);
        _emit(new VpnLogEntry(DateTime.UtcNow, sw.ToString(), VpnLogLevel.Info));
    }

    public void Dispose()
    {
    }
}
