using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QudJP.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VerboseProbeLoggingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QJ004";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Verbose probes must use RuntimeDiagnostics",
        messageFormat: "Verbose probe marker '{0}' must be logged through RuntimeDiagnostics.LogVerboseProbe",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol is null || IsApprovedProbeLogger(methodSymbol) || !IsDirectRuntimeLogger(methodSymbol))
        {
            return;
        }

        foreach (var stringPart in invocation.ArgumentList.Arguments.SelectMany(argument => GetStringParts(argument.Expression, context.SemanticModel)))
        {
            if (!TryExtractProbeMarker(stringPart.Value, out var marker))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, stringPart.Location, marker));
        }
    }

    private static bool IsApprovedProbeLogger(IMethodSymbol methodSymbol)
    {
        return methodSymbol.Name == "LogVerboseProbe"
            && methodSymbol.ContainingType?.ToDisplayString() == "QudJP.RuntimeDiagnostics";
    }

    private static bool IsDirectRuntimeLogger(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (containingType == "QudJP.QudJPMod" && methodSymbol.Name == "LogToUnity")
        {
            return true;
        }

        if (containingType == "QudJP.RuntimeDiagnostics"
            && methodSymbol.Name is "LogStatus" or "LogWarning" or "LogError")
        {
            return true;
        }

        if (containingType == "UnityEngine.Debug" && methodSymbol.Name.StartsWith("Log", StringComparison.Ordinal))
        {
            return true;
        }

        return containingType == "System.Diagnostics.Trace"
            && methodSymbol.Name is "TraceInformation" or "TraceWarning" or "TraceError";
    }

    private static IEnumerable<StringPart> GetStringParts(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var foundSyntaxString = false;
        foreach (var literal in expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>())
        {
            if (literal.Token.Value is string value)
            {
                foundSyntaxString = true;
                yield return new StringPart(value, literal.GetLocation());
            }
        }

        foreach (var text in expression.DescendantNodesAndSelf().OfType<InterpolatedStringTextSyntax>())
        {
            foundSyntaxString = true;
            yield return new StringPart(text.TextToken.ValueText, text.GetLocation());
        }

        if (foundSyntaxString)
        {
            yield break;
        }

        var constant = semanticModel.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is string constantValue)
        {
            yield return new StringPart(constantValue, expression.GetLocation());
        }
    }

    private static bool TryExtractProbeMarker(string value, out string marker)
    {
        marker = string.Empty;
        var markerIndex = value.IndexOf("/v", StringComparison.Ordinal);
        if (markerIndex <= 0)
        {
            return false;
        }

        var start = markerIndex - 1;
        while (start >= 0 && IsMarkerCharacter(value[start]))
        {
            start--;
        }

        start++;
        if (start >= markerIndex)
        {
            return false;
        }

        var end = markerIndex + 2;
        while (end < value.Length && char.IsDigit(value[end]))
        {
            end++;
        }

        if (end == markerIndex + 2)
        {
            return false;
        }

        marker = value.Substring(start, end - start);
        return true;
    }

    private static bool IsMarkerCharacter(char value)
    {
        return char.IsLetterOrDigit(value);
    }

    private readonly struct StringPart
    {
        internal StringPart(string value, Location location)
        {
            Value = value;
            Location = location;
        }

        internal string Value { get; }

        internal Location Location { get; }
    }
}
