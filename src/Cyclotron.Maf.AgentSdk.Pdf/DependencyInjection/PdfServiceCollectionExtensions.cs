using Cyclotron.Maf.AgentSdk.Options;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.Services.Impl;
using UglyToad.PdfPig.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Register the JPX-capable filter provider for PDF image extraction, which uses OpenJpeg for JPEG 2000 decoding
        services.AddSingleton<IFilterProvider, JpxFilterProvider>();

        // Register PDF image extractor as keyed service
        services.AddKeyedSingleton<IPdfImageExtractor, PdfPigImageExtractor>("pdfpig");

        // Register DefaultPdfContentClassifier as the implementation for IPdfContentClassifier
        services.TryAddSingleton<IPdfContentClassifier, DefaultPdfContentClassifier>();

        // Register PDF content analyzer as keyed service
        services.AddKeyedSingleton<IPdfContentAnalyzer, PdfPigContentAnalyzer>("pdfpig");

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
