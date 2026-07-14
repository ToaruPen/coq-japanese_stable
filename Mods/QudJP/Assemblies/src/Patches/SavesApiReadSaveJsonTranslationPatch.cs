using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;

namespace QudJP.Patches;

[HarmonyPatch]
public static class SavesApiReadSaveJsonTranslationPatch
{
    private const string Context = nameof(SavesApiReadSaveJsonTranslationPatch);
    private const string TemplateKey = "Total size: {0}";
    private const string Prefix = "Total size: ";

    [HarmonyTargetMethod]
    private static MethodBase? TargetMethod()
    {
        var targetType = AccessTools.TypeByName("Qud.API.SavesAPI");
        if (targetType is null)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch target type not found.");
            return null;
        }

        var method = AccessTools.Method(targetType, "ReadSaveJson", new[] { typeof(string), typeof(string) });
        if (method is null)
        {
            Trace.TraceError("QudJP: SavesApiReadSaveJsonTranslationPatch.ReadSaveJson(string, string) not found.");
        }

        return method;
    }

    public static void Postfix(ref Task<Qud.API.SaveGameInfo> __result)
    {
        try
        {
            __result = AdaptCompletion(__result, TranslateResult);
        }
        catch (Exception ex)
        {
            TraceTransformFailure(ex);
        }
    }

    internal static Task<T> AdaptCompletion<T>(Task<T> task, Action<T> transform)
    {
        return task.ContinueWith(
            completedTask =>
            {
                if (completedTask.Status == TaskStatus.RanToCompletion)
                {
                    try
                    {
                        transform(completedTask.GetAwaiter().GetResult());
                    }
                    catch (Exception ex)
                    {
                        TraceTransformFailure(ex);
                    }
                }

                return completedTask;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).Unwrap();
    }

    internal static void TranslateResult(object? result)
    {
        if (result is null)
        {
            return;
        }

        var current = GetSize(result);
        if (current is null
            || current.Length == 0
            || !current.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }

        var translatedTemplate = Translator.Translate(TemplateKey);
        if (string.Equals(translatedTemplate, TemplateKey, StringComparison.Ordinal))
        {
            return;
        }

        var translated = translatedTemplate.Replace("{0}", current.Substring(Prefix.Length));
        if (string.Equals(translated, current, StringComparison.Ordinal))
        {
            return;
        }

        if (!SetSize(result, translated))
        {
            return;
        }

        DynamicTextObservability.RecordTransform(Context, TemplateKey, current, translated);
    }

    private static void TraceTransformFailure(Exception exception)
    {
        try
        {
            Trace.TraceError(
                "QudJP: SavesApiReadSaveJsonTranslationPatch completion transform failed: {0}",
                exception);
        }
        catch (Exception)
        {
            // Diagnostic logging must not turn a healthy game task into a faulted task.
        }
    }

    private static string? GetSize(object result)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, "Size");
        if (property is not null && property.CanRead && property.PropertyType == typeof(string))
        {
            return property.GetValue(result) as string;
        }

        var field = AccessTools.Field(type, "Size");
        return field?.FieldType == typeof(string)
            ? field.GetValue(result) as string
            : null;
    }

    private static bool SetSize(object result, string translated)
    {
        var type = result.GetType();
        var property = AccessTools.Property(type, "Size");
        if (property is not null && property.CanWrite && property.PropertyType == typeof(string))
        {
            property.SetValue(result, translated);
            return true;
        }

        var field = AccessTools.Field(type, "Size");
        if (field?.FieldType == typeof(string))
        {
            field.SetValue(result, translated);
            return true;
        }

        return false;
    }
}
