using Microsoft.Extensions.AI;
using SpamDetection.Services;

namespace SpamDetection;

/// <summary>
/// Main entry point for the spam detection sample.
/// Demonstrates using the AgentSdk to classify messages as spam or not spam.
/// </summary>
public class Main(
    IHostApplicationLifetime applicationLifetime,
    IConfiguration configuration,
    ILogger<Main> logger,
    ISpamWorkflow spamWorkflow) : IMain
{
    private readonly ILogger<Main> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    private readonly ISpamWorkflow _spamWorkflow = spamWorkflow ?? throw new ArgumentNullException(nameof(spamWorkflow));

    public IConfiguration Configuration { get; set; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<int> RunAsync()
    {
        var cancellationToken = _applicationLifetime.ApplicationStopping;
        return await _spamWorkflow.RunAsync(cancellationToken);
    }
}
