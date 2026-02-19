using System.Text.Json;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using SpamDetection.Models;
using IVectorStoreManager = Cyclotron.Maf.AgentSdk.VectorStore.Services.IVectorStoreManager;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Executor for processing text-based PDF invoices.
/// Supports both Azure (vector store + file_search) and Ollama (local embedding retrieval).
/// </summary>
public sealed class TextBasedInvoiceExecutor(ILogger<TextBasedInvoiceExecutor> logger)
{
    private readonly ILogger<TextBasedInvoiceExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes text-based invoice extraction using the specified provider strategy.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExecuteAsync(
        Stream pdfContent,
        string fileName,
        IInvoiceProviderStrategy strategy,
        IPdfToMarkdownConverter pdfToMarkdownConverter,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing TextBasedInvoiceExecutor for file: {FileName} using provider: {Provider}", fileName, strategy.ProviderKey);

        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "TextBased"
        };

        try
        {
            // Step 1: Convert PDF to markdown
            _logger.LogInformation("Converting PDF to markdown...");
            var markdown = await pdfToMarkdownConverter.ConvertToMarkdownAsync(pdfContent, fileName, cancellationToken);

            _logger.LogInformation("PDF converted to markdown. Length: {Length} characters", markdown.Length);

            if (strategy.UsesVectorStore)
            {
                result = await ExecuteAzureAsync(
                    fileName,
                    markdown,
                    strategy,
                    vectorStoreManager,
                    cancellationToken);
            }
            else
            {
                result = await ExecuteOllamaAsync(
                    fileName,
                    markdown,
                    strategy,
                    vectorStoreManager,
                    cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TextBasedInvoiceExecutor");
            result.Action = "error";
            throw;
        }
        finally
        {
            // Step 8: Cleanup
            _logger.LogInformation("Cleaning up agent resources...");
            await strategy.AgentFactory.CleanupAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Azure execution path: upload markdown to vector store and use file_search.
    /// </summary>
    private async Task<InvoiceExtractionResult> ExecuteAzureAsync(
        string fileName,
        string markdown,
        IInvoiceProviderStrategy strategy,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "TextBased"
        };

        try
        {
            var providerName = strategy.AgentFactory.AgentDefinition.Provider;

            // Step 2: Create vector store
            _logger.LogInformation("Creating vector store for Azure...");
            var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
                providerName,
                key: $"invoice-{fileName}",
                purpose: "Invoice document for extraction",
                name: $"Invoice_{Path.GetFileNameWithoutExtension(fileName)}",
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
                $"{Path.GetFileNameWithoutExtension(fileName)}.md",
                SimpleChunkingAsync,
                cancellationToken);

            _logger.LogInformation("File uploaded with ID: {FileId}", fileId);
            result.MutableFileIds.Add(fileId);

            // Step 4: Create agent with vector store
            _logger.LogInformation("Creating invoice extraction agent with vector store...");
            await strategy.AgentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            result.MutableAgentIds.Add(strategy.AgentFactory.Agent?.Id ?? "unknown");

            // Step 5: Create user message with context
            var context = new
            {
                documentName = fileName,
                analysisMode = "TextBased"
            };

            var userMessage = strategy.AgentFactory.CreateUserMessage(context);

            _logger.LogInformation("Running agent to extract invoice data...");

            // Step 6: Run agent with polling
            var response = await strategy.AgentFactory.RunAgentWithPollingAsync(
                messages: [userMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 7: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully");

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
    /// Ollama execution path: index markdown locally and retrieve chunks for context.
    /// </summary>
    private async Task<InvoiceExtractionResult> ExecuteOllamaAsync(
        string fileName,
        string markdown,
        IInvoiceProviderStrategy strategy,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceExtractionResult
        {
            InvoiceData = new InvoiceData(),
            ContentType = "TextBased"
        };

        try
        {
            var providerName = strategy.AgentFactory.AgentDefinition.Provider;

            // Step 2: Create local vector store for RAG
            _logger.LogInformation("Creating local vector store for Ollama RAG...");
            var vectorStoreId = await vectorStoreManager.GetOrCreateSharedVectorStoreAsync(
                providerName,
                key: $"invoice-ollama-{fileName}",
                purpose: "Invoice document for local RAG",
                name: $"Invoice_Ollama_{Path.GetFileNameWithoutExtension(fileName)}",
                cancellationToken);

            _logger.LogInformation("Local vector store created with ID: {VectorStoreId}", vectorStoreId);
            result.MutableVectorStoreIds.Add(vectorStoreId);

            // Step 3: Index markdown in local vector store (Ollama generates embeddings)
            _logger.LogInformation("Indexing markdown in local vector store...");
            using var markdownStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markdown));
            await vectorStoreManager.AddFileToVectorStoreAsync(
                providerName,
                vectorStoreId,
                markdownStream,
                $"{Path.GetFileNameWithoutExtension(fileName)}.md",
                SimpleChunkingAsync,
                cancellationToken);

            _logger.LogInformation("Markdown indexed in local vector store");

            // Step 4: Create agent WITHOUT vector store (Ollama agents don't support Azure file_search)
            _logger.LogInformation("Creating invoice extraction agent without vector store...");
            await strategy.AgentFactory.CreateAgentAsync(cancellationToken);
            result.MutableAgentIds.Add(strategy.AgentFactory.Agent?.Id ?? "unknown");

            // Step 5: Create context with retrieved chunks and user message
            var context = new
            {
                documentName = fileName,
                analysisMode = "TextBased_Ollama",
                retrievedContent = await RetrieveContextAsync(
                    vectorStoreId,
                    providerName,
                    vectorStoreManager,
                    cancellationToken)
            };

            var userMessage = strategy.AgentFactory.CreateUserMessage(context);

            _logger.LogInformation("Running agent to extract invoice data using local context...");

            // Step 6: Run agent with polling
            var response = await strategy.AgentFactory.RunAgentWithPollingAsync(
                messages: [userMessage],
                cancellationToken: cancellationToken);

            var responseText = response.Messages?.LastOrDefault()?.Text ?? string.Empty;
            result.AgentResponse = responseText;

            _logger.LogInformation("Agent response received. Response length: {Length} characters", responseText.Length);

            // Step 7: Parse JSON response into InvoiceData
            result.InvoiceData = ParseInvoiceDataFromJson(responseText);

            _logger.LogInformation("Invoice data extracted successfully from Ollama");

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
            // Query for top chunks that help with invoice extraction
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
        var text = await reader.ReadToEndAsync();
        var chunkSize = 1000;
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            var chunkText = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return (chunkText, $"{fileName}#{i / chunkSize}");
        }
    }
}

