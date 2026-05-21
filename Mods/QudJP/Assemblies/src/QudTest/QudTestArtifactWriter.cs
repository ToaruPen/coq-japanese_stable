using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace QudJP.QudTest;

public static class QudTestArtifactWriter
{
    public static string Write(string outputDirectory, QudTestRunResult result)
    {
        Directory.CreateDirectory(outputDirectory);
        var runDirectory = Path.Combine(outputDirectory, "runs", FormatRunId(result.EndedAtUtc));
        Directory.CreateDirectory(runDirectory);

        var latestResults = Path.Combine(outputDirectory, "results.json");
        var runResults = Path.Combine(runDirectory, "results.json");
        WriteJson(latestResults, result);
        WriteJson(runResults, result);
        WriteSummary(Path.Combine(outputDirectory, "summary.txt"), result);
        WriteSummary(Path.Combine(runDirectory, "summary.txt"), result);
        return latestResults;
    }

    private static void WriteJson(string path, QudTestRunResult result)
    {
        using var writer = File.CreateText(path);
        var serializer = new JsonSerializer { Formatting = Formatting.Indented };
        serializer.Serialize(writer, result);
        writer.WriteLine();
    }

    private static void WriteSummary(string path, QudTestRunResult result)
    {
        File.WriteAllText(
            path,
            string.Format(
                CultureInfo.InvariantCulture,
                "command={0}{1}suite={2}{1}passed={3}{1}total={4}{1}failed={5}{1}",
                result.Command,
                Environment.NewLine,
                result.Suite,
                result.Passed ? "true" : "false",
                result.TotalCount,
                result.FailCount));
    }

    private static string FormatRunId(DateTimeOffset endedAtUtc)
    {
        return endedAtUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", CultureInfo.InvariantCulture);
    }
}
