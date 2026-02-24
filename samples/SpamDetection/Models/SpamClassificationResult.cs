namespace SpamDetection.Models;

/// <summary>
/// Represents the result of spam classification.
/// </summary>
public sealed record SpamClassificationResult(string Classification, double Confidence, string Reason);
