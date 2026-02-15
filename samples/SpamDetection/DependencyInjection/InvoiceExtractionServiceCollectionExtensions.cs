using SpamDetection.Services;
using SpamDetection.Services.Impl;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring invoice extraction services.
/// </summary>
public static class InvoiceExtractionServiceCollectionExtensions
{
    /// <summary>
    /// Configures all services required for the invoice extraction workflow.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInvoiceExtractionServices(this IServiceCollection services)
    {
        // Register keyed agent factory for invoice_extractor
        services.AddKeyedAgentFactories("invoice_extractor");

        // Register the three executors as transient services
        services.AddTransient<TextBasedInvoiceExecutor>();
        services.AddTransient<ImageOnlyInvoiceExecutor>();
        services.AddTransient<MixedInvoiceExecutor>();

        // Register the invoice extraction workflow service
        services.AddScoped<IInvoiceExtractionWorkflow, InvoiceExtractionWorkflow>();

        return services;
    }
}
