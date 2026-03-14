using System.Diagnostics;

namespace QudJP.Tests;

internal static class TestTraceHelper
{
    internal static string CaptureTrace(Action action)
    {
        using var writer = new StringWriter();
        using var listener = new TextWriterTraceListener(writer);
        Trace.Listeners.Add(listener);

        try
        {
            action();
            Trace.Flush();
            listener.Flush();
            return writer.ToString();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }
}
