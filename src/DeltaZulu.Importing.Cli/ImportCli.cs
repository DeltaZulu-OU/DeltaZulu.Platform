using System.Globalization;
using DeltaZulu.Importing.Core;

namespace DeltaZulu.Importing.Cli;

public static class ImportCli
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            WriteHelp(output);
            return 0;
        }

        try
        {
            var options = ParseOptions(args);
            if (args.Length == 0 || options.ContainsKey("interactive"))
            {
                options = PromptForMissingOptions(options, input, output);
            }

            if (options.TryGetValue("demo-seed", out var demoDirectory))
            {
                var target = string.IsNullOrWhiteSpace(demoDirectory) ? Path.Combine(Path.GetTempPath(), "deltazulu-demo-seed") : demoDirectory;
                DemoSeedImportCatalog.MaterializeBaseline(target);
                options["source"] = target;
                options["mode"] = ImportMode.DemoSeed.ToString();
                output.WriteLine($"Demo seed files materialized at {target}");
            }

            var request = BuildRequest(options);
            var result = new LocalFilesetImporter().Import(request);
            WriteSummary(result, output);
            return result.Manifest.Status == ImportJobStatus.Completed ? 0 : 2;
        }
        catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine(ex.Message);
            error.WriteLine();
            WriteHelp(error);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{token}'.");
            var name = token[2..];
            if (name is "interactive" or "no-silver")
            {
                options[name] = "true";
                continue;
            }

            if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for option '{token}'.");
            options[name] = args[++index];
        }

        return options;
    }

    private static Dictionary<string, string> PromptForMissingOptions(Dictionary<string, string> options, TextReader input, TextWriter output)
    {
        var prompted = new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase);
        Prompt("source", "Source file or directory", prompted, input, output);
        Prompt("job-id", "Job id", prompted, input, output, $"import-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
        Prompt("mode", "Mode (Migration, Onboarding, DemoSeed)", prompted, input, output, ImportMode.Onboarding.ToString());
        Prompt("parser-target", "Parser target", prompted, input, output, "generic");
        Prompt("preprocessor", "Preprocessor version", prompted, input, output, "pre-v1");
        return prompted;
    }

    private static void Prompt(string key, string label, Dictionary<string, string> options, TextReader input, TextWriter output, string? defaultValue = null)
    {
        if (options.ContainsKey(key)) return;
        output.Write(defaultValue is null ? $"{label}: " : $"{label} [{defaultValue}]: ");
        var value = input.ReadLine();
        options[key] = string.IsNullOrWhiteSpace(value) ? defaultValue ?? string.Empty : value.Trim();
    }

    private static ImportJobRequest BuildRequest(IReadOnlyDictionary<string, string> options)
    {
        var jobId = Required(options, "job-id");
        var source = Required(options, "source");
        var mode = options.TryGetValue("mode", out var modeValue) && Enum.TryParse<ImportMode>(modeValue, true, out var parsedMode) ? parsedMode : ImportMode.Onboarding;
        var parser = options.TryGetValue("parser-target", out var parserValue) ? parserValue : "generic";
        var preprocessor = options.TryGetValue("preprocessor", out var preprocessorValue) ? preprocessorValue : "pre-v1";
        var maxChunkBytes = options.TryGetValue("max-chunk-bytes", out var chunkValue) ? int.Parse(chunkValue, CultureInfo.InvariantCulture) : 1024 * 1024;
        return new ImportJobRequest(jobId, mode, source, parser, preprocessor, maxChunkBytes, !options.ContainsKey("no-silver"));
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"Missing required option '--{name}'.");

    private static void WriteSummary(ImportResult result, TextWriter output)
    {
        var manifest = result.Manifest;
        output.WriteLine($"Import job {manifest.JobId} finished with status {manifest.Status}.");
        output.WriteLine($"Sources: {manifest.Summary.SourceFileCount}; chunks: {manifest.Summary.ChunkCount}; bronze rows: {manifest.Summary.BronzeRowCount}; malformed records: {manifest.Summary.MalformedRecordCount}.");
        foreach (var source in manifest.SourceObjects)
        {
            output.WriteLine($"- {source.RelativePath} ({source.Format}, {source.Status}) => {result.BronzeRows.Count(row => row.SourceObjectId == source.SourceObjectId)} rows");
        }

        foreach (var diagnostic in manifest.Diagnostics.Where(item => item.Severity != ImportDiagnosticSeverity.Information))
        {
            output.WriteLine($"  {diagnostic.Severity}: {diagnostic.Code} record={diagnostic.RecordNumber?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} {diagnostic.Message}");
        }
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("DeltaZulu bulk log importer");
        output.WriteLine("Usage: deltazulu-import --source <path> --job-id <id> [--mode Migration|Onboarding|DemoSeed] [--parser-target <name>] [--preprocessor <version>] [--max-chunk-bytes <bytes>] [--no-silver]");
        output.WriteLine("       deltazulu-import --interactive");
        output.WriteLine("       deltazulu-import --demo-seed <directory> --job-id demo");
    }
}
