using Cyclotron.Maf.AgentSdk.VectorStore.Options;
using Cyclotron.Maf.AgentSdk.VectorStore.Models;
using Cyclotron.Maf.AgentSdk.VectorStore.Telemetry;
using Azure.AI.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;

namespace Cyclotron.Maf.AgentSdk.VectorStore.Services.Impl;

/// <summary>
/// Deprecated: Use <see cref="AzureVectorStoreManager"/> instead.
/// This class is maintained for backward compatibility only.
/// </summary>
[Obsolete("Use AzureVectorStoreManager instead. This class will be removed in a future version.", false)]
public class VectorStoreManager : AzureVectorStoreManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorStoreManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="clientFactory">Factory for creating AI project clients.</param>
    /// <param name="indexingOptions">Vector store indexing options.</param>
    public VectorStoreManager(
        ILogger<VectorStoreManager> logger,
        Func<string, AIProjectClient> clientFactory,
        IOptions<VectorStoreIndexingOptions> indexingOptions)
        : base(
            ConvertLogger<AzureVectorStoreManager>(logger),
            indexingOptions,
            CreateDefaultTelemetry(logger),
            clientFactory ?? throw new ArgumentNullException(nameof(clientFactory)),
            CreateDefaultConfigFactory())
    {
    }

    /// <summary>
    /// Creates a default configuration factory for backward compatibility with legacy code.
    /// This factory throws InvalidOperationException as it should not be called in practice.
    /// The clientFactory parameter should be used directly in new code.
    /// </summary>
    private static Func<string, VectorStoreProviderConfig> CreateDefaultConfigFactory()
    {
        return providerName => throw new InvalidOperationException(
            "VectorStoreManager (legacy) does not support provider configuration. " +
            "Use AzureVectorStoreManager directly instead.");
    }

    /// <summary>
    /// Converts an ILogger of one type to another (unsafe cast for backward compatibility).
    /// </summary>
    private static ILogger<T> ConvertLogger<T>(ILogger logger)
    {
        // This is a workaround for backward compatibility. In practice, this should be handled
        // by the DI container, but for direct instantiation we need this adapter.
        return new LoggerAdapter<T>(logger);
    }

    /// <summary>
    /// Creates a default telemetry instance for backward compatibility.
    /// </summary>
    private static VectorStoreTelemetry CreateDefaultTelemetry(ILogger logger)
    {
        // Create a minimal meter factory that provides a no-op meter
        var meterFactory = new NoOpMeterFactory();
        var telemetryLogger = new LoggerAdapter<VectorStoreTelemetry>(logger);
        return new VectorStoreTelemetry(meterFactory, telemetryLogger);
    }
}

/// <summary>
/// Minimal logger adapter for type conversion (backward compatibility).
/// </summary>
internal class LoggerAdapter<T> : ILogger<T>
{
    private readonly ILogger _logger;

    public LoggerAdapter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _logger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _logger.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _logger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// No-op meter factory for creating meters that do nothing (backward compatibility).
/// </summary>
internal class NoOpMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options) => new Meter(options.Name, options.Version);
    public void Dispose() { }
}

