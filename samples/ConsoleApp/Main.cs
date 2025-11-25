public class Main : IMain
{
    private readonly ILogger<Main> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public Main(
        IHostApplicationLifetime applicationLifetime,
        IConfiguration configuration,
        ILogger<Main> logger)
    {
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IConfiguration Configuration { get; set; }

    public Task<int> RunAsync()
    {
        _logger.LogInformation("Main executed");

        // use this token for stopping the services
        _applicationLifetime.ApplicationStopping.ThrowIfCancellationRequested();

        return Task.FromResult(0);
    }
}
