using System.Reflection;

using Serilog;
using Serilog.Events;
using Serilog.Sinks.AzureAnalytics;

// Start our smart AppHost
AppHost.Start(args, Assembly.GetEntryAssembly()?.GetName().Name);

// Create a Serilog Logger
Log.Logger = AppHost.CreateSerilogLogger(
    (logger, configuration, env) =>
    {
        if (!string.IsNullOrEmpty(configuration["AzureLogAnalytics:WorkspaceId"])
        && !string.IsNullOrEmpty(configuration["AzureLogAnalytics:AuthenticationId"]))
        {
            logger.WriteTo.AzureAnalytics(
                configuration["AzureLogAnalytics:WorkspaceId"],
                configuration["AzureLogAnalytics:AuthenticationId"],
                new ConfigurationSettings
                {
                    Flatten = false,
                    LogName = $"{env.ApplicationName}{env.EnvironmentName}",
                    BufferSize = 1,
                    BatchSize = 1
                },
                restrictedToMinimumLevel: LogEventLevel.Information);
        }
    });

try
{
    Log.Information("Starting Spam Detection Sample");

    using var host = AppHost
                    .CreateHostBuilder()
                    .UseAgentSdk()
                    .ConfigureServices(SpamDetectionServiceCollectionExtensions.ConfigureServices)
                    .Build();

    await host.StartAsync();

    var result = await host.ExecuteAsync(async m => await m.RunAsync());

    await host.StopAsync();

    Log.Information("Spam Detection Sample Stopped");

    return result;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Spam Detection Sample terminated unexpectedly");

    return 1;
}
finally
{
    Log.CloseAndFlush();
}
