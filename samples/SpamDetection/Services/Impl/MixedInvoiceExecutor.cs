using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using Cyclotron.Maf.AgentSdk.VectorStore.Services;
using SpamDetection.Models;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Executor for processing mixed PDF invoices (containing both text and images).
/// Extracts images, converts text to markdown, creates vector store, and sends both to agent.
/// </summary>
public sealed class MixedInvoiceExecutor(ILogger<MixedInvoiceExecutor> logger)
{
    private readonly ILogger<MixedInvoiceExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes mixed invoice extraction.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExecuteAsync(
        Stream pdfContent,
        string fileName,
        IAgentFactory agentFactory,
        IPdfToMarkdownConverter pdfToMarkdownConverter,
        IPdfImageExtractor pdfImageExtractor,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing MixedInvoiceExecutor for file: {FileName}", fileName);

        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "Mixed"
        };

        try
        {
            // Step 1a: Extract images from PDF
            _logger.LogInformation("Extracting images from mixed PDF...");
            var extractedImages = await pdfImageExtractor.ExtractImagesAsync(pdfContent, fileName, cancellationToken);
            _logger.LogInformation("Extracted {Count} images from PDF", extractedImages.Length);

            // Reset stream position for markdown conversion
            if (pdfContent.CanSeek)
            {
                pdfContent.Seek(0, SeekOrigin.Begin);
            }

            // Step 1b: Convert PDF to markdown
            _logger.LogInformation("Converting PDF to markdown...");
            var markdown = await pdfToMarkdownConverter.ConvertToMarkdownAsync(pdfContent, fileName, cancellationToken);
            _logger.LogInformation("PDF converted to markdown. Length: {Length} characters", markdown.Length);

            // Step 2: Create vector store
            var providerName = agentFactory.AgentDefinition.Provider;

            _logger.LogInformation("Creating vector store...");
            var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
                providerName,
                key: $"invoice-mixed-{fileName}",
                purpose: "Mixed invoice document (text + images) for extraction",
                name: $"Invoice_Mixed_{Path.GetFileNameWithoutExtension(fileName)}",
                cancellationToken);

            _logger.LogInformation("Vector store created with ID: {VectorStoreId}", vectorStoreId);
            result.MutableVectorStoreIds.Add(vectorStoreId);

            // Step 3: Upload markdown to vector store
            _logger.LogInformation("Uploading markdown file to vector store...");
            using var markdownStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
            var fileId = await vectorStoreManager.AddFileToVectorStoreAsync(
                providerName,
                vectorStoreId,
                markdownStream,
                $"{Path.GetFileNameWithoutExtension(fileName)}_text.md",
                SimpleChunkingAsync,
                cancellationToken);

            _logger.LogInformation("File uploaded with ID: {FileId}", fileId);
            result.MutableFileIds.Add(fileId);

            // Step 4: Create ChatMessage with both text prompt and images as DataContent
            _logger.LogInformation("Creating chat message with text prompt and {Count} image(s)...", extractedImages.Length);

            var contentItems = new List<AIContent>();

            // Generate and add text prompt
            var userPrompt = GenerateMixedAnalysisPrompt(fileName, extractedImages.Length);
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

            // Step 5: Create agent WITH vector store (so agent can use file_search)
            _logger.LogInformation("Creating invoice extraction agent with vector store access...");
            await agentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            result.MutableAgentIds.Add(agentFactory.Agent?.Id ?? "unknown");

            // Step 6: Create user message context (this won't be used since we already have chatMessage, but for completeness)
            var context = new
            {
                documentName = fileName,
                analysisMode = "Mixed",
                imageCount = extractedImages.Length
            };

            // Step 7: Run agent with message containing both images and prompt
            _logger.LogInformation("Running agent to extract invoice data from mixed content...");
            var response = await agentFactory.RunAgentWithPollingAsync(
                messages: [chatMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 8: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully from mixed content");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MixedInvoiceExecutor");
            result.Action = "error";
            throw;
        }
        finally
        {
            // Step 9: Cleanup
            _logger.LogInformation("Cleaning up agent resources...");
            await agentFactory.CleanupAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Generates the prompt for mixed invoice analysis (text + images).
    /// </summary>
    private static string GenerateMixedAnalysisPrompt(string fileName, int imageCount)
    {
        return $"""
        Please analyze the invoice document provided, which contains both text content and {imageCount} image(s).

        Document: {fileName}
        Analysis mode: Mixed (native PDF with embedded images and scanned pages)

        You have access to:
        1. Text content via document search tools (use file_search to retrieve text portions)
        2. {imageCount} invoice image(s) provided directly

        Extraction Instructions:
        1. First, examine the provided images to identify key invoice fields
        2. Use document search to retrieve supporting text information (line items, payment terms, etc.)
        3. Combine information from both sources for complete accuracy

        Extract all invoice information:
        - Invoice number, dates, and vendor information
        - Line items with quantities and prices
        - Totals, taxes, and payment information
        - Any special terms or notes

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

    /// <summary>
    /// Simple fixed-size chunking strategy for markdown documents.
    /// </summary>
    private static async IAsyncEnumerable<(string Text, string ChunkId)> SimpleChunkingAsync(
        Stream stream,
        string fileName)
    {
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        var chunkSize = 1000;
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var chunkText = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return (chunkText, $"{fileName}#{i / chunkSize}");
        }
    }
}
