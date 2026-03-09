using Serilog.Events;

namespace OmniFlow.Observability;

/// <summary>
/// Configuration options for OmniFlow logging with Serilog
/// </summary>
public class OmniFlowLoggingOptions
{
    /// <summary>
    /// Minimum log level. Default is Information.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Enable console logging. Default is true.
    /// </summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>
    /// Enable file logging. Default is false.
    /// </summary>
    public bool EnableFile { get; set; } = false;

    /// <summary>
    /// File log path. Default is "logs/omniflow-.log".
    /// </summary>
    public string FilePath { get; set; } = "logs/omniflow-.log";

    /// <summary>
    /// File rolling interval. Default is Day.
    /// </summary>
    public Serilog.RollingInterval RollingInterval { get; set; } = Serilog.RollingInterval.Day;

    /// <summary>
    /// Number of log files to retain. Default is 7.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;

    /// <summary>
    /// Console output template.
    /// </summary>
    public string ConsoleTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// File output template.
    /// </summary>
    public string FileTemplate { get; set; } =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Enable JSON formatting for console output. Default is false.
    /// </summary>
    public bool UseJsonConsole { get; set; } = false;

    /// <summary>
    /// Enable JSON formatting for file output. Default is false.
    /// </summary>
    public bool UseJsonFile { get; set; } = false;

    /// <summary>
    /// Enable correlation ID enrichment. Default is true.
    /// </summary>
    public bool EnableCorrelationId { get; set; } = true;

    /// <summary>
    /// Enable machine name enrichment. Default is true.
    /// </summary>
    public bool EnableMachineName { get; set; } = true;

    /// <summary>
    /// Enable environment name enrichment. Default is true.
    /// </summary>
    public bool EnableEnvironmentName { get; set; } = true;

    /// <summary>
    /// Enable thread ID enrichment. Default is false.
    /// </summary>
    public bool EnableThreadId { get; set; } = false;

    /// <summary>
    /// Override log levels for specific namespaces.
    /// </summary>
    public Dictionary<string, LogEventLevel> LogLevelOverrides { get; set; } = new()
    {
        ["Microsoft"] = LogEventLevel.Warning,
        ["System"] = LogEventLevel.Warning
    };
}
