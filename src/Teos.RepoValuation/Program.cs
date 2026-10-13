namespace Teos.RepoValuation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// TEOS Sovereign Repo Valuation - deterministic repo scoring, no LLM.
///
/// Modes:
///   --smoke                             Deterministic demo valuation (identical output
///                                       to the released v1.1.1 action). Default with no args.
///   --repo-url &lt;url&gt;                    Live valuation via the GitHub REST API.
///   --json &lt;path&gt;                       (with --repo-url) write JSON result to a file.
///   --token &lt;token&gt;                     Optional token (also read from GITHUB_TOKEN / TEOS_GITHUB_TOKEN).
///
/// When run inside GitHub Actions ($GITHUB_OUTPUT is set) the score, valuation and
/// decision are written to that file so the composite action can expose them as outputs.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // "smoke" (bare) is the verb the composite action passes; "--smoke" is accepted too.
        if (args.Length == 0 || Contains(args, "--smoke")
            || string.Equals(args[0], "smoke", StringComparison.OrdinalIgnoreCase))
            return RunSmoke();

        if (Contains(args, "--help") || Contains(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var repoUrl = NextValue(args, "--repo-url");
        if (repoUrl is null)
        {
            Console.Error.WriteLine("error: expected --smoke or --repo-url <url>");
            PrintHelp();
            return 2;
        }

        var token = NextValue(args, "--token")
                 ?? Environment.GetEnvironmentVariable("TEOS_GITHUB_TOKEN")
                 ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        var jsonPath = NextValue(args, "--json");

        return await RunLiveAsync(repoUrl, token, jsonPath).ConfigureAwait(false);
    }

    /// <summary>
    /// Deterministic smoke valuation. Output must stay byte-stable - the released
    /// marketplace action and CI depend on the exact lines.
    /// </summary>
    public static int RunSmoke()
    {
        Console.WriteLine("=== TEOS Repo Valuation Smoke Test ===");

        var rawData = new RawRepoData
        {
            Url = "https://github.com/Elmahrosa/teos-opportunity-network",
            FullName = "Elmahrosa/teos-opportunity-network",
            Name = "teos-opportunity-network",
            Stars = 12,
            SizeKb = 2500,
            LanguageCount = 2,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        Console.WriteLine($"Collected: {rawData.FullName} | Stars={rawData.Stars} | Size={rawData.SizeKb}KB | Langs={rawData.LanguageCount}");

        var result = RepoValuationEngine.Evaluate(rawData);

        Console.WriteLine($"Valuation: Score={result.Score}/100 | ${result.EstimatedLow:F0}-${result.EstimatedHigh:F0} | ${result.EstimatedAvg:F0} | {result.Decision}/{result.Action}");
        Console.WriteLine($"Metrics: LOC={result.Metrics.EstimatedLoc} | Activity={result.Metrics.ActivityFactor} | Rep={result.Metrics.ReputationFactor}");

        WriteGithubOutputs(result);

        Console.WriteLine("=== Smoke Test PASSED ===");
        return 0;
    }

    /// <summary>Live valuation against real GitHub data (no fake "live" output).</summary>
    public static async Task<int> RunLiveAsync(string repoUrl, string? token, string? jsonPath, CancellationToken ct = default)
    {
        Console.WriteLine("=== TEOS Repo Valuation ===");

        var client = new GitHubApiClient(new HttpClient(), token);
        RawRepoData raw;
        try
        {
            raw = await client.CollectAsync(repoUrl, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"::error::Failed to collect repo data: {ex.Message}");
            return 1;
        }

        var display = string.IsNullOrEmpty(raw.FullName) ? raw.ExternalId : raw.FullName;
        Console.WriteLine($"Collected: {display} | Stars={raw.Stars} | Size={raw.SizeKb}KB | Langs={raw.Languages.Count}");

        var result = RepoValuationEngine.Evaluate(raw);

        Console.WriteLine($"Valuation: Score={result.Score}/100 | ${result.EstimatedLow:F0}-${result.EstimatedHigh:F0} | ${result.EstimatedAvg:F0} | {result.Decision}/{result.Action}");
        Console.WriteLine($"Metrics: LOC={result.Metrics.EstimatedLoc} | Activity={result.Metrics.ActivityFactor} | Rep={result.Metrics.ReputationFactor}");

        if (!string.IsNullOrEmpty(jsonPath))
        {
            var payload = new
            {
                score = result.Score,
                value_low = result.EstimatedLow,
                value_high = result.EstimatedHigh,
                decision = result.Decision,
                action = result.Action
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath!, json, ct).ConfigureAwait(false);
            Console.WriteLine(json);
        }

        WriteGithubOutputs(result);

        Console.WriteLine("=== Valuation Complete ===");
        return 0;
    }

    /// <summary>Appends score/valuation/decision to $GITHUB_OUTPUT when present (GitHub Actions).</summary>
    private static void WriteGithubOutputs(RepoValuationEngine.RepoValuationResult result)
    {
        var path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (string.IsNullOrEmpty(path))
            return;

        var valuation = $"${result.EstimatedLow:F0}-${result.EstimatedHigh:F0}";
        var lines = new[]
        {
            $"score={result.Score}",
            $"valuation={valuation}",
            $"decision={result.Decision}"
        };
        File.AppendAllLines(path, lines);
    }

    private static bool Contains(string[] args, string value)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, value, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string? NextValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.Ordinal))
                return args[i + 1];
        }
        return null;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("TEOS Sovereign Repo Valuation");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  --smoke                 Deterministic demo valuation (default).");
        Console.WriteLine("  --repo-url <url>        Live valuation for a GitHub repo.");
        Console.WriteLine("  --json <path>           With --repo-url: also write JSON result to a file.");
        Console.WriteLine("  --token <token>         GitHub token (or GITHUB_TOKEN / TEOS_GITHUB_TOKEN env).");
        Console.WriteLine("  --help                  Show this help.");
    }
}