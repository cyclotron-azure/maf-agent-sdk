using System.Text.Json;
using Cyclotron.Maf.AgentSdk.Agents;
using Cyclotron.Maf.AgentSdk.Services;
using SpamDetection.Models;

namespace SpamDetection.Services.Impl;

/// <summary>
/// Executor for processing text-based PDF invoices.
/// Converts PDF to markdown, creates vector store, and retrieves invoice data via file_search tool.
/// </summary>
public sealed class TextBasedInvoiceExecutor(ILogger<TextBasedInvoiceExecutor> logger)
{
    private readonly ILogger<TextBasedInvoiceExecutor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Executes text-based invoice extraction.
    /// </summary>
    public async Task<InvoiceExtractionResult> ExecuteAsync(
        Stream pdfContent,
        string fileName,
        IAgentFactory agentFactory,
        IPdfToMarkdownConverter pdfToMarkdownConverter,
        IVectorStoreManager vectorStoreManager,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing TextBasedInvoiceExecutor for file: {FileName}", fileName);

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

            // Step 2: Create vector store
            var providerName = agentFactory.AgentDefinition.AIFrameworkOptions.Provider;

            _logger.LogInformation("Creating vector store...");
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
                cancellationToken);

            _logger.LogInformation("File uploaded with ID: {FileId}", fileId);
            result.MutableFileIds.Add(fileId);

            // Step 4: Create agent with vector store
            _logger.LogInformation("Creating invoice extraction agent...");
            await agentFactory.CreateAgentAsync(vectorStoreId, cancellationToken);
            result.MutableAgentIds.Add(agentFactory.Agent?.Id ?? "unknown");

            // Step 5: Create user message with context
            var context = new
            {
                documentName = fileName,
                analysisMode = "TextBased"
            };

            var userMessage = agentFactory.CreateUserMessage(context);

            _logger.LogInformation("Running agent to extract invoice data...");

            // Step 6: Run agent with polling
            var response = await agentFactory.RunAgentWithPollingAsync(
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
            _logger.LogError(ex, "Error in TextBasedInvoiceExecutor");
            result.Action = "error";
            throw;
        }
        finally
        {
            // Step 8: Cleanup
            _logger.LogInformation("Cleaning up agent resources...");
            await agentFactory.CleanupAsync(cancellationToken);
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
}
