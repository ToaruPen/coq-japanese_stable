using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace QudJP;

internal static class InventoryActionMenuCloseTimingObservability
{
    private const string ProbeMarker = "InventoryActionMenuCloseTiming/v1";
    private const int MaxActiveMenuDepth = 16;
    private const double RecentCancelWindowMilliseconds = 2000d;

    private static readonly object SyncRoot = new();
    private static readonly Stack<ActiveMenuContext> activeMenuStack = new();

    private static int nextSequence;
    private static int activeSequence;
    private static long activeStartTimestamp;
    private static int lastCancelSequence;
    private static long lastCancelTimestamp;
    private static bool suppressInventoryRefreshUntilPopupHidden;

    [ThreadStatic]
    private static int inventoryRefreshSuppressionBypassDepth;

    internal static TimingScope BeginMenu(int actionCount)
    {
        var timestamp = Stopwatch.GetTimestamp();
        int sequence;
        var excessiveNestingDetected = false;
        lock (SyncRoot)
        {
            sequence = ++nextSequence;
            if (activeMenuStack.Count >= MaxActiveMenuDepth)
            {
                excessiveNestingDetected = true;
                activeMenuStack.Clear();
                activeSequence = 0;
                activeStartTimestamp = 0;
            }

            activeMenuStack.Push(new ActiveMenuContext(activeSequence, activeStartTimestamp));
            activeSequence = sequence;
            activeStartTimestamp = timestamp;
        }

        if (excessiveNestingDetected)
        {
            Trace.TraceWarning("QudJP: {0} exceeded active menu nesting limit; previous context was dropped.", nameof(InventoryActionMenuCloseTimingObservability));
        }

        Log(sequence, "menu-open-begin", TimeSpan.Zero, () => "action_count=" + actionCount.ToString(CultureInfo.InvariantCulture));
        return TimingScope.Start(sequence);
    }

    internal static void EndMenu(TimingScope scope, bool canceled)
    {
        if (!scope.HasValue)
        {
            return;
        }

        scope.Stop();
        var timestamp = Stopwatch.GetTimestamp();
        lock (SyncRoot)
        {
            if (canceled)
            {
                lastCancelSequence = scope.Sequence;
                lastCancelTimestamp = timestamp;
                suppressInventoryRefreshUntilPopupHidden = true;
            }
            else
            {
                lastCancelSequence = 0;
                lastCancelTimestamp = 0;
                suppressInventoryRefreshUntilPopupHidden = false;
            }

            if (activeSequence == scope.Sequence)
            {
                RestorePreviousActiveMenuContext();
            }
        }

        Log(
            scope.Sequence,
            "menu-return",
            scope.Elapsed,
            () => "result=" + (canceled ? "cancel" : "action"));
    }

    internal static void LogPopupHideRequest(string? popupId)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled || !IsInventoryActionMenuPopupId(popupId))
        {
            return;
        }

        var sequence = GetActiveSequence();
        if (sequence <= 0)
        {
            return;
        }

        Log(sequence, "popup-hide-request", ElapsedSinceActiveStart(), () => "popup_id=" + popupId);
    }

    internal static void LogPopupHiddenAfterFrameDelay(
        string? popupId,
        int previousHideNextFrame,
        int currentHideNextFrame)
    {
        if (!IsInventoryActionMenuPopupId(popupId))
        {
            return;
        }

        if (!TryGetRecentSequenceElapsed(out var sequence, out var elapsed))
        {
            return;
        }

        if (RuntimeDiagnostics.VerboseProbesEnabled)
        {
            Log(
                sequence,
                "popup-hidden-after-frame-delay",
                elapsed,
                () => "popup_id=" + popupId
                    + ";previous_hide_next_frame=" + previousHideNextFrame.ToString(CultureInfo.InvariantCulture)
                    + ";current_hide_next_frame=" + currentHideNextFrame.ToString(CultureInfo.InvariantCulture));
        }

        ClearInventoryRefreshSuppression(sequence);
    }

    internal static bool ShouldSuppressInventoryRefreshAfterCancel()
    {
        if (inventoryRefreshSuppressionBypassDepth > 0)
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!suppressInventoryRefreshUntilPopupHidden || lastCancelSequence <= 0)
            {
                return false;
            }

            return ElapsedBetween(lastCancelTimestamp, Stopwatch.GetTimestamp()).TotalMilliseconds
                <= RecentCancelWindowMilliseconds;
        }
    }

    internal static void RunWithInventoryRefreshSuppressionBypassed(Action action)
    {
        inventoryRefreshSuppressionBypassDepth++;
        try
        {
            action();
        }
        finally
        {
            inventoryRefreshSuppressionBypassDepth--;
        }
    }

    internal static bool ShouldObservePopupUpdate()
    {
        return RuntimeDiagnostics.VerboseProbesEnabled || ShouldSuppressInventoryRefreshAfterCancel();
    }

    internal static void LogInventoryRefreshSuppressed()
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled || !TryGetRecentCancel(out var sequence, out var elapsed))
        {
            return;
        }

        Log(sequence, "inventory-refresh-suppressed", elapsed, () => null);
    }

    internal static TimingScope BeginInventoryRefresh()
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled || !TryGetRecentCancel(out var sequence, out var elapsed))
        {
            return TimingScope.Empty;
        }

        Log(sequence, "inventory-refresh-begin", elapsed, () => null);
        return TimingScope.Start(sequence);
    }

    internal static void EndInventoryRefresh(TimingScope scope)
    {
        if (!scope.HasValue)
        {
            return;
        }

        scope.Stop();
        Log(scope.Sequence, "inventory-refresh-end", scope.Elapsed, () => null);
    }

    internal static string BuildLogLineForTests(int sequence, string phase, TimeSpan elapsed, string? detail)
    {
        return BuildLogLine(sequence, phase, elapsed, detail);
    }

    internal static void LogForTests(string phase, Func<string?> detailFactory)
    {
        if (!RuntimeDiagnostics.VerboseProbesEnabled)
        {
            return;
        }

        Log(1, phase, TimeSpan.Zero, detailFactory);
    }

    internal static bool ShouldSuppressInventoryRefreshAfterCancelForTests()
    {
        return ShouldSuppressInventoryRefreshAfterCancel();
    }

    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            nextSequence = 0;
            activeSequence = 0;
            activeStartTimestamp = 0;
            activeMenuStack.Clear();
            lastCancelSequence = 0;
            lastCancelTimestamp = 0;
            suppressInventoryRefreshUntilPopupHidden = false;
        }
    }

    private static bool TryGetRecentCancel(out int sequence, out TimeSpan elapsed)
    {
        var timestamp = Stopwatch.GetTimestamp();
        lock (SyncRoot)
        {
            sequence = lastCancelSequence;
            elapsed = ElapsedBetween(lastCancelTimestamp, timestamp);
        }

        return sequence > 0 && elapsed.TotalMilliseconds <= RecentCancelWindowMilliseconds;
    }

    private static int GetActiveSequence()
    {
        lock (SyncRoot)
        {
            return activeSequence;
        }
    }

    private static void ClearInventoryRefreshSuppression(int sequence)
    {
        lock (SyncRoot)
        {
            if (lastCancelSequence == sequence)
            {
                suppressInventoryRefreshUntilPopupHidden = false;
            }
        }
    }

    private static TimeSpan ElapsedSinceActiveStart()
    {
        var timestamp = Stopwatch.GetTimestamp();
        lock (SyncRoot)
        {
            return ElapsedBetween(activeStartTimestamp, timestamp);
        }
    }

    private static bool TryGetRecentSequenceElapsed(out int sequence, out TimeSpan elapsed)
    {
        var timestamp = Stopwatch.GetTimestamp();
        lock (SyncRoot)
        {
            if (lastCancelSequence > 0)
            {
                var cancelElapsed = ElapsedBetween(lastCancelTimestamp, timestamp);
                if (cancelElapsed.TotalMilliseconds <= RecentCancelWindowMilliseconds)
                {
                    sequence = lastCancelSequence;
                    elapsed = cancelElapsed;
                    return true;
                }
            }

            sequence = activeSequence;
            elapsed = ElapsedBetween(activeStartTimestamp, timestamp);
            return sequence > 0;
        }
    }

    private static TimeSpan ElapsedBetween(long startTimestamp, long endTimestamp)
    {
        if (startTimestamp <= 0 || endTimestamp < startTimestamp)
        {
            return TimeSpan.Zero;
        }

        var ticks = endTimestamp - startTimestamp;
        return TimeSpan.FromSeconds((double)ticks / Stopwatch.Frequency);
    }

    private static void RestorePreviousActiveMenuContext()
    {
        if (activeMenuStack.Count <= 0)
        {
            activeSequence = 0;
            activeStartTimestamp = 0;
            return;
        }

        var previousContext = activeMenuStack.Pop();
        activeSequence = previousContext.Sequence;
        activeStartTimestamp = previousContext.StartTimestamp;
    }

    private static bool IsInventoryActionMenuPopupId(string? popupId)
    {
        return !string.IsNullOrEmpty(popupId)
            && CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                popupId!,
                "InventoryActionMenu",
                CompareOptions.IgnoreCase) >= 0;
    }

    private static void Log(int sequence, string phase, TimeSpan elapsed, Func<string?> detailFactory)
    {
        RuntimeDiagnostics.LogVerboseProbe(() => BuildLogLine(sequence, phase, elapsed, detailFactory()));
    }

    private static string BuildLogLine(int sequence, string phase, TimeSpan elapsed, string? detail)
    {
        var builder = new StringBuilder();
        builder.Append("[QudJP] ");
        builder.Append(ProbeMarker);
        builder.Append(": seq=");
        builder.Append(sequence.ToString(CultureInfo.InvariantCulture));
        builder.Append(";phase=");
        builder.Append(EscapeFieldValue(phase));
        builder.Append(";elapsed_ms=");
        builder.Append(elapsed.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture));
        if (detail is { Length: > 0 })
        {
            builder.Append(";detail=");
            builder.Append(EscapeFieldValue(detail));
        }

        return builder.ToString();
    }

    private static string EscapeFieldValue(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case ';':
                    builder.Append("\\;");
                    break;
                case '=':
                    builder.Append("\\=");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    private readonly struct ActiveMenuContext
    {
        internal ActiveMenuContext(int sequence, long startTimestamp)
        {
            Sequence = sequence;
            StartTimestamp = startTimestamp;
        }

        internal int Sequence { get; }

        internal long StartTimestamp { get; }
    }

    internal readonly struct TimingScope
    {
        private readonly Stopwatch? stopwatch;

        private TimingScope(int sequence)
        {
            Sequence = sequence;
            stopwatch = Stopwatch.StartNew();
        }

        internal static TimingScope Empty => default;

        internal int Sequence { get; }

        internal TimeSpan Elapsed => stopwatch?.Elapsed ?? TimeSpan.Zero;

        internal bool HasValue => Sequence > 0 && stopwatch is not null;

        internal static TimingScope Start(int sequence)
        {
            return new TimingScope(sequence);
        }

        internal void Stop()
        {
            stopwatch?.Stop();
        }
    }
}
