using AnalysisEngine.Core.DependencyGraph;
using AnalysisEngine.Core.DiffAnalysis;
using AnalysisEngine.Core.Models;
using AnalysisEngine.Core.Report;
using AnalysisEngine.Core.Rules.Business;
using AnalysisEngine.Core.Rules.Technical;

var arguments = ParseArguments(args);
if (arguments.ShowHelp || string.IsNullOrWhiteSpace(arguments.DiffPath))
{
    PrintUsage();
    return arguments.ShowHelp ? 0 : 1;
}

if (!File.Exists(arguments.DiffPath))
{
    Console.Error.WriteLine($"Diff file not found: {arguments.DiffPath}");
    return 2;
}

var diffText = await File.ReadAllTextAsync(arguments.DiffPath);
var parser = new DiffParser();
var diffFiles = parser.Parse(diffText);
var changes = new ChangeClassifier().Classify(diffFiles);

var affectedServices = new List<AffectedService>();
if (!string.IsNullOrWhiteSpace(arguments.RegistryPath))
{
    if (!File.Exists(arguments.RegistryPath))
    {
        Console.Error.WriteLine($"Registry file not found: {arguments.RegistryPath}");
        return 3;
    }

    affectedServices = new DependencyResolver(arguments.RegistryPath)
        .Resolve(arguments.ServiceName);
}

var (technicalScore, technicalRules) = new RiskCalculator()
    .Calculate(changes, affectedServices, diffFiles);
var (businessScore, businessRules) = new BusinessRiskCalculator()
    .Calculate(changes);
var finalScore = Math.Clamp(
    (int)Math.Ceiling((technicalScore * 0.65) + (businessScore * 0.35)),
    1,
    10);

var risk = new RiskScore(
    technicalScore,
    businessScore,
    finalScore,
    RiskScore.CalculateLevel(finalScore),
    [.. technicalRules, .. businessRules]);

var report = new AnalysisReport(
    arguments.ServiceName,
    changes,
    affectedServices,
    risk,
    ReportGenerator.GenerateRecommendations(changes, affectedServices, risk));
var markdown = new ReportGenerator().Generate(report);

if (string.IsNullOrWhiteSpace(arguments.OutputPath))
{
    Console.Write(markdown);
}
else
{
    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(arguments.OutputPath));
    if (!string.IsNullOrWhiteSpace(outputDirectory))
        Directory.CreateDirectory(outputDirectory);

    await File.WriteAllTextAsync(arguments.OutputPath, markdown);
    Console.WriteLine($"Report written to {arguments.OutputPath}");
}

return 0;

static CliArguments ParseArguments(string[] args)
{
    string? diffPath = null;
    string? registryPath = null;
    string? outputPath = null;
    var serviceName = "changed-service";
    var showHelp = false;

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--diff" when index + 1 < args.Length:
                diffPath = args[++index];
                break;
            case "--registry" when index + 1 < args.Length:
                registryPath = args[++index];
                break;
            case "--service" when index + 1 < args.Length:
                serviceName = args[++index];
                break;
            case "--output" when index + 1 < args.Length:
                outputPath = args[++index];
                break;
            case "-h" or "--help":
                showHelp = true;
                break;
            default:
                throw new ArgumentException($"Unknown or incomplete argument: {args[index]}");
        }
    }

    return new CliArguments(diffPath, registryPath, outputPath, serviceName, showHelp);
}

static void PrintUsage()
{
    Console.WriteLine("AI Quality Gate");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project AnalysisEngine.Cli -- --diff <file> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --service <name>    Changed service name");
    Console.WriteLine("  --registry <file>   Optional service registry JSON");
    Console.WriteLine("  --output <file>     Write Markdown report to a file");
    Console.WriteLine("  -h, --help          Show help");
}

internal sealed record CliArguments(
    string? DiffPath,
    string? RegistryPath,
    string? OutputPath,
    string ServiceName,
    bool ShowHelp);
