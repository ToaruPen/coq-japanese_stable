using System;
using System.Diagnostics;
using System.Globalization;

namespace QudJP;

internal static class RuntimeStartupTiming
{
    internal const string Marker = "[QudJP] StartupTiming/v1:";

    internal static IDisposable Measure(string phase, string? detail = null)
    {
        return new TimingScope(phase, detail);
    }

    internal static void LogElapsed(string phase, TimeSpan elapsed, string? detail = null)
    {
        var message = Marker
            + " phase="
            + EscapeFieldValue(phase)
            + " elapsed_ms="
            + elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += " detail=" + EscapeFieldValue(detail!);
        }

        RuntimeDiagnostics.LogStatus(message);
    }

    private static string EscapeFieldValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(" ", "\\ ")
            .Replace(";", "\\;")
            .Replace("=", "\\=");
    }

    private sealed class TimingScope : IDisposable
    {
        private readonly string phase;
        private readonly string? detail;
        private readonly Stopwatch stopwatch;
        private bool disposed;

        internal TimingScope(string phase, string? detail)
        {
            this.phase = phase;
            this.detail = detail;
            stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopwatch.Stop();
            LogElapsed(phase, stopwatch.Elapsed, detail);
        }
    }
}
