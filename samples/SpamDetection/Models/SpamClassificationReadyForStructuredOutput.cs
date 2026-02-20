using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SpamDetection.Models;

/// <summary>
/// Structured output model for spam classification results.
/// This model defines the schema for agent responses when using structured output mode.
/// </summary>
[Description("Structured output model for spam classification")]
public class SpamClassificationReadyForStructuredOutput
{
    /// <summary>
    /// Gets or sets the classification result: true if spam, false if not spam.
    /// </summary>
    [JsonPropertyName("is_spam")]
    [Description("True if the message is classified as spam, false otherwise")]
    public bool IsSpam { get; set; }

    /// <summary>
    /// Gets or sets the confidence score (0.0 to 1.0) for the classification.
    /// </summary>
    [JsonPropertyName("confidence")]
    [Description("Confidence score between 0.0 (not confident) and 1.0 (very confident)")]
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the reason for the classification.
    /// </summary>
    [JsonPropertyName("reason")]
    [Description("Explanation of why the message was classified this way")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets any detected spam indicators (comma-separated list).
    /// Examples: "phishing", "suspicious_links", "impersonation", "financial_scam"
    /// </summary>
    [JsonPropertyName("spam_indicators")]
    [Description("Comma-separated list of detected spam indicators")]
    public string? SpamIndicators { get; set; }

    /// <summary>
    /// Gets or sets recommended actions to take on this message.
    /// </summary>
    [JsonPropertyName("recommendation")]
    [Description("Recommended action: 'delete', 'quarantine', or 'approve'")]
    public string? Recommendation { get; set; }
}
