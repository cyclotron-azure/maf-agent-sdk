namespace Cyclotron.Maf.AgentSdk.Options;

/// <summary>
/// Configuration options for vector store file indexing behavior.
/// Controls how the system waits for files to be indexed before agents use them.
/// </summary>
public class VectorStoreIndexingOptions
{
    /// <summary>
    /// Maximum number of attempts to check if a file has been indexed.
    /// Default: 60 (allows up to 2 minutes with 2-second delays, or 16+ minutes with exponential backoff).
    /// </summary>
    public int MaxWaitAttempts { get; set; } = 60;

    /// <summary>
    /// Initial delay in milliseconds between index readiness checks.
    /// Default: 2000ms (2 seconds).
    /// </summary>
    public int InitialWaitDelayMs { get; set; } = 2000;

    /// <summary>
    /// Whether to use exponential backoff for wait delays.
    /// When enabled, delay doubles after each attempt up to MaxWaitDelayMs.
    /// Default: true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Maximum delay in milliseconds between checks when using exponential backoff.
    /// Default: 30000ms (30 seconds).
    /// </summary>
    public int MaxWaitDelayMs { get; set; } = 30000;

    /// <summary>
    /// Total timeout in milliseconds for all indexing operations.
    /// If zero or negative, no total timeout is enforced (only MaxWaitAttempts).
    /// Default: 0 (disabled).
    /// </summary>
    public int TotalTimeoutMs { get; set; } = 0;
}
