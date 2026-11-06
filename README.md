# AI Quality Gate

An offline, deterministic change-impact analyzer for unified Git diffs.

It classifies source changes, follows service dependencies, calculates technical and
business-facing risk, and produces a Markdown review report. The public core performs
no network requests and requires no external credentials or runtime configuration.

## Features

- Unified diff parsing
- Change classification for C#, Java, Go, JavaScript, and TypeScript
- Direct and transitive service dependency analysis
- Deterministic technical and business risk scoring
- Markdown reports with review recommendations
- Unit tests and a minimal command-line interface

## Requirements

- .NET SDK 10

## Build and test

```bash
dotnet restore AnalysisEngine.slnx
dotnet test AnalysisEngine.slnx --configuration Release
```

## Run

```bash
dotnet run --project AnalysisEngine.Cli -- \
  --diff examples/sample.diff \
  --service order-service \
  --registry examples/service-registry.json \
  --output reports/impact.md
```

The service registry is optional. Without it, the analyzer still classifies changes and
calculates risk, but it does not report downstream services.

## Repository layout

```text
AnalysisEngine.Core/   Diff analysis, dependency resolution, risk rules, reporting
AnalysisEngine.Cli/    Offline command-line application
AnalysisEngine.Tests/  Unit tests
examples/              Synthetic input examples
```

## Safety model

- No network clients
- No runtime configuration files
- No external service integrations
- Synthetic examples only
- Generated reports are ignored by Git
