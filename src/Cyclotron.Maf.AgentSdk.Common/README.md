# Cyclotron.Maf.AgentSdk.Common

Shared common types and configuration options for the AgentSdk ecosystem.

This package contains model provider configuration definitions that are used across multiple AgentSdk packages (main SDK, Vectors, PDF, etc.) to avoid circular dependencies.

## Contents

- **ModelProviderOptions**: Configuration container for all model providers
- **ModelProviderDefinitionOptions**: Individual provider configuration with validation and utility methods

## Usage

```csharp
using Cyclotron.Maf.AgentSdk.Common.Options;

var providers = options.Value.Providers;
var providerConfig = providers["azure_foundry"];
```
