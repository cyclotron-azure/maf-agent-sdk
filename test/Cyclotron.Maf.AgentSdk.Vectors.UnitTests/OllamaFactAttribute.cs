using System;
using Xunit;

namespace Cyclotron.Maf.AgentSdk.Vectors.UnitTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OllamaFactAttribute : FactAttribute
{
    public OllamaFactAttribute()
    {
        var isGitHubActions = string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (isGitHubActions && string.IsNullOrWhiteSpace(Skip))
        {
            Skip = "Ollama tests are disabled on GitHub Actions.";
        }
    }
}
