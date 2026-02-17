using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering PDF processing services in the dependency injection container.
/// Provides PDF image extraction, content analysis, and markdown conversion capabilities.
/// </summary>
public static class PdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers all PDF processing services including options configuration, image extraction,
    /// content analysis, and markdown conversion.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfServices(this IServiceCollection services)
    {
        // Register PDF options
        services.AddPdfConversionOptions();
        services.AddPdfContentAnalysisOptions();
        services.AddPdfImageExtractionOptions();

        // Register PDF to Markdown converter
        services.AddSingleton<IPdfToMarkdownConverter, PdfPigMarkdownConverter>();

        // Register PDF image extractor as keyed service
        services.AddKeyedSingleton<IPdfImageExtractor, PdfPigImageExtractor>("pdfpig");

        // Register PDF content analyzer as keyed service
        services.AddKeyedSingleton<IPdfContentAnalyzer>(
            "pdfpig",
            (sp, _) => new PdfPigContentAnalyzer(
                sp.GetRequiredService<ILogger<PdfPigContentAnalyzer>>(),
                sp.GetRequiredService<IOptions<PdfContentAnalysisOptions>>()));

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="PdfConversionOptions"/> from the <c>PdfConversion:</c> section in configuration.
    /// Controls PDF to Markdown conversion behavior and debug output settings.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfConversionOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<PdfConversionOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var pdfSection = configuration.GetSection(PdfConversionOptions.SectionName);
                if (pdfSection.Exists())
                {
                    pdfSection.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="PdfContentAnalysisOptions"/> from the <c>PdfContentAnalysis:</c> section in configuration.
    /// Controls PDF content analysis behavior, analyzer selection, and failure handling strategies.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfContentAnalysisOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<PdfContentAnalysisOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var analysisSection = configuration.GetSection(PdfContentAnalysisOptions.SectionName);
                if (analysisSection.Exists())
                {
                    analysisSection.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers and configures <see cref="PdfImageExtractionOptions"/> from the <c>PdfImageExtraction:</c> section in configuration.
    /// Controls PDF image extraction behavior, format preferences, and performance characteristics.
    /// Supports named options for multiple configurations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="name">Optional name for the options instance. Defaults to the default options name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPdfImageExtractionOptions(
        this IServiceCollection services,
        string? name = null)
    {
        name ??= string.Empty;

        services.AddOptions<PdfImageExtractionOptions>(name)
            .Configure<IConfiguration>((options, configuration) =>
            {
                var extractionSection = configuration.GetSection(PdfImageExtractionOptions.SectionName);
                if (extractionSection.Exists())
                {
                    extractionSection.Bind(options);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
