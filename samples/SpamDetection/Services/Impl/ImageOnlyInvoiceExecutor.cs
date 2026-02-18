using System.Text.Json;
using Microsoft.Extensions.AI;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using SpamDetection.Models;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Executor for processing image-only PDF invoices.
/// Extracts images and sends them directly to the vision model via DataContent.
/// </summary>
public sealed class ImageOnlyInvoiceExecutor(ILogger<ImageOnlyInvoiceExecutor> logger)
{
    private readonly ILogger<ImageOnlyInvoiceExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes image-only invoice extraction.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExecuteAsync(
        Stream pdfContent,
        string fileName,
        IAgentFactory agentFactory,
        IPdfImageExtractor pdfImageExtractor,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing ImageOnlyInvoiceExecutor for file: {FileName}", fileName);

        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "ImageOnly"
        };

        try
        {
            // Step 1: Extract images from PDF
            _logger.LogInformation("Extracting images from PDF...");
            var extractedImages = await pdfImageExtractor.ExtractImagesAsync(pdfContent, fileName, cancellationToken);

            _logger.LogInformation("Extracted {Count} images from PDF", extractedImages.Length);

            if (extractedImages.Length == 0)
            {
                _logger.LogWarning("No images found in PDF");
                result.InvoiceData.ExtractionNotes = "No images found in PDF document";
                return result;
            }

            // Create a vector store to satisfy file_search tool configuration even for image-only PDFs
            var providerName = agentFactory.AgentDefinition.Provider;
            _logger.LogInformation("Creating vector store for image-only invoice...");

            var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
                providerName,
                key: $"invoice-image-{fileName}",
                purpose: "Image-only invoice document for extraction",
                name: $"Invoice_Image_{Path.GetFileNameWithoutExtension(fileName)}",
                cancellationToken);

            result.MutableVectorStoreIds.Add(vectorStoreId);

            // Step 2: Create ChatMessage with image content
            _logger.LogInformation("Creating chat message with {Count} image(s)...", extractedImages.Length);

            var contentItems = new List<AIContent>();

            // Add text prompt
            var userPrompt = GenerateImageAnalysisPrompt(fileName);
            contentItems.Add(new TextContent(userPrompt));

            // Add each image as DataContent
            foreach (var image in extractedImages)
            {
                _logger.LogDebug(
                    "Adding image to message: {ImageName}, Size: {Size} bytes, Format: {MimeType}",
                    image.ImageName,
                    image.ImageBytes.Length,
                    image.MimeType);

                contentItems.Add(new DataContent(image.ImageBytes, image.MimeType));
            }

            var chatMessage = new ChatMessage(ChatRole.User, contentItems);

            // Step 3: Create agent with vector store (required for configured tools)
            _logger.LogInformation("Creating invoice extraction agent for image analysis...");
            await agentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            result.MutableAgentIds.Add(agentFactory.Agent?.Id ?? "unknown");

            // Step 4: Run agent with image-containing message
            _logger.LogInformation("Running agent to analyze invoice images...");
            var response = await agentFactory.RunAgentWithPollingAsync(
                messages: [chatMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 5: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully from images");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ImageOnlyInvoiceExecutor");
            result.Action = "error";
            throw;
        }
        finally
        {
            // Step 6: Cleanup
            _logger.LogInformation("Cleaning up agent resources...");
            await agentFactory.CleanupAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Generates the prompt for image-based invoice analysis.
    /// </summary>
    private static string GenerateImageAnalysisPrompt(string fileName)
    {
        return $"""
        Please analyze the invoice image(s) provided and extract all invoice information.

        Document: {fileName}
        Analysis mode: ImageOnly (scanned or image-based PDF)

        Carefully examine each image to identify:
        - Invoice number and dates
        - Vendor and customer information
        - Line items with quantities and prices
        - Totals and payment information

        Return ONLY valid JSON matching the required schema. Do not include any explanation or additional text.
        """;
    }

    /// <summary>
    /// Parses the JSON response from the agent into InvoiceData.
    /// </summary>
    private InvoiceData ParseInvoiceDataFromJson(string jsonResponse)
    {
        try
        {
            // Try to extract JSON from the response (in case there's additional text)
            var jsonStart = jsonResponse.IndexOf('{');
            var jsonEnd = jsonResponse.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                _logger.LogWarning("No JSON object found in response. Raw response: {Response}", jsonResponse);
                return new InvoiceData
                {
                    ExtractionNotes = "Failed to parse response: No JSON found"
                };
            }

            var jsonString = jsonResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };

            var invoiceData = JsonSerializer.Deserialize<InvoiceData>(jsonString, options)
                ?? new InvoiceData { ExtractionNotes = "Deserialization returned null" };

            return invoiceData;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error parsing JSON response");
            return new InvoiceData
            {
                ExtractionNotes = $"JSON parsing error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing invoice data");
            return new InvoiceData
            {
                ExtractionNotes = $"Parsing error: {ex.Message}"
            };
        }
    }
}
