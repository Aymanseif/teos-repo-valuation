namespace Teos.RepoValuation.Tests;

using System;
using System.Collections.Generic;
using Xunit;

public class RepoValuationEngineTests
{
    private static RawRepoData Repo(
        int stars,
        long sizeKb,
        Dictionary<string, int>? languages = null,
        TimeSpan? age = null,
        string? license = "MIT") => new()
    {
        Stars = stars,
        SizeKb = sizeKb,
        Languages = languages ?? new Dictionary<string, int>(),
        UpdatedAt = DateTime.UtcNow.Add(-(age ?? TimeSpan.FromDays(1))),
        License = license,
        Url = "https://github.com/a/b",
        FullName = "a/b",
        Name = "b",
        ExternalId = "a/b"
    };

    [Fact]
    public void Smoke_fixture_matches_the_released_action_output()
    {
        // The action runs `-- smoke` on exactly this fixture (Stars=12, SizeKb=2500,
        // 1 day old, no explicit license). The values below are the hand-computed,
        // released-action outputs and must never drift.
        var raw = new RawRepoData
        {
            Url = "https://github.com/actions/checkout",
            FullName = "actions/checkout",
            Name = "checkout",
            Stars = 12,
            SizeKb = 2500,
            LanguageCount = 2,
            License = null,
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var result = RepoValuationEngine.Evaluate(raw);

        Assert.Equal(90, result.Score);
        Assert.Equal(193.2, result.EstimatedLow, 2);
        Assert.Equal(441.6, result.EstimatedHigh, 2);
        Assert.Equal(317.4, result.EstimatedAvg, 2);
        Assert.Equal("ALERT", result.Decision);
        Assert.Equal("APPLY_NOW", result.Action);
        Assert.Equal(12500, result.Metrics.EstimatedLoc);
        Assert.Equal(1.5, result.Metrics.ActivityFactor, 3);
        Assert.Equal(0.92, result.Metrics.ReputationFactor, 3);
    }

    [Fact]
    public void Inactive_repo_with_assertion_license_scores_ignore_pass()
    {
        var raw = Repo(stars: 0, sizeKb: 200, languages: new Dictionary<string, int> { ["C#"] = 50, ["TS"] = 50 },
            age: TimeSpan.FromDays(365), license: "NOASSERTION");

        var result = RepoValuationEngine.Evaluate(raw);

        Assert.Equal(35, result.Score);
        Assert.Equal("IGNORE", result.Decision);
        Assert.Equal("PASS", result.Action);
        Assert.Equal(11.2, result.EstimatedLow, 2);
        Assert.Equal(25.6, result.EstimatedHigh, 2);
    }

    [Fact]
    public void Mid_repo_scores_log_review()
    {
        var raw = Repo(stars: 5, sizeKb: 2000, languages: new Dictionary<string, int> { ["CSharp"] = 1 },
            age: TimeSpan.FromDays(10), license: "MIT");

        var result = RepoValuationEngine.Evaluate(raw);

        Assert.Equal(78, result.Score);
        Assert.Equal("LOG", result.Decision);
        Assert.Equal("REVIEW", result.Action);
    }

    [Fact]
    public void Score_is_capped_at_95()
    {
        var languages = new Dictionary<string, int>();
        for (int i = 0; i < 12; i++)
            languages["L" + i] = 100;

        var raw = Repo(stars: 5000, sizeKb: 1_000_000, languages: languages,
            age: TimeSpan.FromDays(1), license: "MIT");

        var result = RepoValuationEngine.Evaluate(raw);

        Assert.Equal(95, result.Score);
        Assert.Equal("ALERT", result.Decision);
        Assert.Equal("APPLY_NOW", result.Action);
    }

    [Fact]
    public void Missing_license_penalty_is_exactly_the_0_8_factor()
    {
        var withLicense = Evaluate(Repo(stars: 100, sizeKb: 10_000, license: "MIT"));
        var withoutLicense = Evaluate(Repo(stars: 100, sizeKb: 10_000, license: null));

        Assert.True(withoutLicense.EstimatedLow > 0);
        Assert.Equal(withLicense.EstimatedLow * 0.8, withoutLicense.EstimatedLow, 6);
        // Score is unaffected by the license penalty (it never enters scoreRaw).
        Assert.Equal(withLicense.Score, withoutLicense.Score);
    }

    [Fact]
    public void Evaluation_is_deterministic_for_identical_input()
    {
        var raw = Repo(stars: 42, sizeKb: 8000, languages: new Dictionary<string, int> { ["CSharp"] = 1 },
            age: TimeSpan.FromDays(20), license: "MIT");

        var a = RepoValuationEngine.Evaluate(raw);
        var b = RepoValuationEngine.Evaluate(raw);

        Assert.Equal(a.Score, b.Score);
        Assert.Equal(a.EstimatedLow, b.EstimatedLow);
        Assert.Equal(a.EstimatedHigh, b.EstimatedHigh);
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Action, b.Action);
    }

    private static RepoValuationEngine.RepoValuationResult Evaluate(RawRepoData raw) =>
        RepoValuationEngine.Evaluate(raw);
}