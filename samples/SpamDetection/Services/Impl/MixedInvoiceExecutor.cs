using System.Text.Json;
using Microsoft.Extensions.AI;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Models;
using Cyclotron.Maf.AgentSdk.Services;
using SpamDetection.Models;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Executor for processing mixed PDF invoices (containing both text and images).
/// Supports both Azure (vector store + file_search) and Ollama (local retrieval + images).
/// </summary>
public sealed class MixedInvoiceExecutor(
    ILogger<MixedInvoiceExecutor> logger,
    IPromptRenderingService promptService)
{
    private readonly ILogger<MixedInvoiceExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IPromptRenderingService _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));

    /// <summary>
    /// Executes mixed invoice extraction using the specified provider strategy.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExecuteAsync(
        Stream pdfContent,
        string fileName,
        IInvoiceProviderStrategy strategy,
        IPdfToMarkdownConverter pdfToMarkdownConverter,
        IPdfImageExtractor pdfImageExtractor,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing MixedInvoiceExecutor for file: {FileName} using provider: {Provider}", fileName, strategy.ProviderKey);

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

            if (strategy.UsesVectorStore)
            {
                result = await ExecuteAzureAsync(
                    fileName,
                    markdown,
                    extractedImages,
                    strategy,
                    vectorStoreManager,
                    cancellationToken);
            }
            else
            {
                result = await ExecuteOllamaAsync(
                    fileName,
                    markdown,
                    extractedImages,
                    strategy,
                    vectorStoreManager,
                    cancellationToken);
            }

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
            await strategy.AgentFactory.CleanupAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Azure execution path: upload markdown to vector store and combine with images.
    /// </summary>
    private async Task<InvoiceExtractionResult> ExecuteAzureAsync(
        string fileName,
        string markdown,
        ExtractedPdfImage[] extractedImages,
        IInvoiceProviderStrategy strategy,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "Mixed"
        };

        try
        {
            var providerName = strategy.AgentFactory.AgentDefinition.Provider;

            // Step 2: Create vector store
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

            // Generate and add text prompt using template
            var context = new
            {
                documentName = fileName,
                analysisMode = "Mixed"
            };
            var userPrompt = _promptService.RenderUserPrompt(strategy.AgentFactory.AgentKey, context);
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
            await strategy.AgentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            result.MutableAgentIds.Add(strategy.AgentFactory.Agent?.Id ?? "unknown");

            // Step 6: Run agent with message containing both images and prompt
            _logger.LogInformation("Running agent to extract invoice data from mixed content...");
            var response = await strategy.AgentFactory.RunAgentWithPollingAsync(
                messages: [chatMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 7: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully from mixed content");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Azure execution path");
            result.Action = "error";
            throw;
        }
    }

    /// <summary>
    /// Ollama execution path: index markdown locally, retrieve chunks, and combine with images.
    /// </summary>
    private async Task<InvoiceExtractionResult> ExecuteOllamaAsync(
        string fileName,
        string markdown,
        ExtractedPdfImage[] extractedImages,
        IInvoiceProviderStrategy strategy,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "Mixed"
        };

        try
        {
            var providerName = strategy.AgentFactory.AgentDefinition.Provider;

            // Step 2: Create local vector store for RAG
            _logger.LogInformation("Creating local vector store for Ollama RAG...");
            var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
                providerName,
                key: $"invoice-ollama-mixed-{fileName}",
                purpose: "Mixed invoice document for local RAG",
                name: $"Invoice_Ollama_Mixed_{Path.GetFileNameWithoutExtension(fileName)}",
                cancellationToken);

            _logger.LogInformation("Local vector store created with ID: {VectorStoreId}", vectorStoreId);
            result.MutableVectorStoreIds.Add(vectorStoreId);

            // Step 3: Index markdown in local vector store
            _logger.LogInformation("Indexing markdown in local vector store...");
            using var markdownStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
            await vectorStoreManager.AddFileToVectorStoreAsync(
                providerName,
                vectorStoreId,
                markdownStream,
                $"{Path.GetFileNameWithoutExtension(fileName)}_text.md",
                SimpleChunkingAsync,
                cancellationToken);

            _logger.LogInformation("Markdown indexed in local vector store");

            // Step 4: Create agent WITHOUT vector store
            _logger.LogInformation("Creating invoice extraction agent without vector store...");
            await strategy.AgentFactory.CreateAgentAsync(cancellationToken);
            result.MutableAgentIds.Add(strategy.AgentFactory.Agent?.Id ?? "unknown");

            // Step 5: Create ChatMessage with retrieved text context and images
            _logger.LogInformation("Creating chat message with context and {Count} image(s)...", extractedImages.Length);

            var contentItems = new List<AIContent>();

            // Generate and add text prompt with retrieved context using template
            var retrievedContext = await RetrieveContextAsync(
                vectorStoreId,
                providerName,
                vectorStoreManager,
                cancellationToken);

            var context = new
            {
                documentName = fileName,
                analysisMode = "Mixed",
                retrievedContent = retrievedContext
            };
            var userPrompt = _promptService.RenderUserPrompt(strategy.AgentFactory.AgentKey, context);
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

            // Step 6: Run agent with message containing context and images
            _logger.LogInformation("Running agent to extract invoice data from mixed content with Ollama...");
            var response = await strategy.AgentFactory.RunAgentWithPollingAsync(
                messages: [chatMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 7: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully from mixed content with Ollama");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Ollama execution path");
            result.Action = "error";
            throw;
        }
    }

    /// <summary>
    /// Retrieves context from the local vector store for embedding in the prompt.
    /// </summary>
    private async Task<string> RetrieveContextAsync(
        string vectorStoreId,
        string providerName,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = "invoice information including number dates amounts vendor customer line items";
            var topK = 5;

            _logger.LogDebug(
                "Retrieving top {TopK} chunks for invoice context from vector store {VectorStoreId}",
                topK,
                vectorStoreId);

            // Query the vector store for similar chunks
            var chunks = await vectorStoreManager.QuerySimilarChunksAsync(
                providerName,
                vectorStoreId,
                query,
                topK,
                cancellationToken);

            if (chunks.Count == 0)
            {
                _logger.LogWarning("No chunks retrieved from vector store");
                return "No document context available.";
            }

            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine("Retrieved document context:");
            contextBuilder.AppendLine("---");
            foreach (var (chunkId, text) in chunks)
            {
                contextBuilder.AppendLine($"[{chunkId}] {text}");
                contextBuilder.AppendLine();
            }
            contextBuilder.AppendLine("---");

            return contextBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve context from vector store; will continue without context");
            return "Document context retrieval failed.";
        }
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
        const int chunkSize = 1000;
        var buffer = new char[chunkSize];
        var chunkIndex = 0;

        while (true)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, chunkSize).ConfigureAwait(false);
            if (charsRead == 0)
            {
                yield break;
            }

            var chunkText = new string(buffer, 0, charsRead);
            yield return (chunkText, $"{fileName}#{chunkIndex}");
            chunkIndex++;
        }
    }
}
