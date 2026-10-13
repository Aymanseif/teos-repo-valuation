namespace Teos.RepoValuation.Tests;

using System.IO;
using Xunit;

public class ProgramSmokeTests
{
    [Fact]
    public void Smoke_writes_github_outputs_with_released_values()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"github_output_{Guid.NewGuid():N}.txt");
        try
        {
            var previous = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            Environment.SetEnvironmentVariable("GITHUB_OUTPUT", outFile);
            try
            {
                var exitCode = Program.RunSmoke();
                Assert.Equal(0, exitCode);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GITHUB_OUTPUT", previous);
            }

            var lines = File.ReadAllLines(outFile);
            Assert.Contains("score=90", lines);
            Assert.Contains("valuation=$193-$442", lines);
            Assert.Contains("decision=ALERT", lines);
        }
        finally
        {
            if (File.Exists(outFile))
                File.Delete(outFile);
        }
    }

    [Fact]
    public void Smoke_is_stable_across_runs()
    {
        using var captured = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        try
        {
            Console.SetOut(captured);
            var first = Program.RunSmoke();
            var firstText = captured.ToString();

            captured.GetStringBuilder().Clear();
            var second = Program.RunSmoke();
            var secondText = captured.ToString();

            Assert.Equal(0, first);
            Assert.Equal(0, second);
            Assert.Equal(firstText, secondText);
            Assert.Contains("Score=90/100", firstText);
            Assert.Contains("ALERT/APPLY_NOW", firstText);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}