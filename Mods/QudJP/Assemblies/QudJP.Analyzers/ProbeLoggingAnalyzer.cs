using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QudJP.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProbeLoggingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QJ004";

    private static readonly string[] QudJPVerboseMarkerFragments =
    {
        "/v1:",
        "Translator: missing key",
    };

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Verbose probe logs must use RuntimeDiagnostics",
        messageFormat: "Direct verbose probe logging through {0} should use RuntimeDiagnostics.LogVerboseProbe",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (IsRuntimeDiagnosticsSourceFile(invocation.SyntaxTree.FilePath))
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol is null)
        {
            return;
        }

        var targetName = GetForbiddenTargetName(methodSymbol);
        if (targetName is null)
        {
            return;
        }

        var argumentCount = invocation.ArgumentList.Arguments.Count;
        for (var index = 0; index < argumentCount; index++)
        {
            var argumentExpression = invocation.ArgumentList.Arguments[index].Expression;
            if (!ContainsVerboseProbeMarker(argumentExpression, context.SemanticModel, context.CancellationToken))
            {
                continue;
            }

            var diagnostic = Diagnostic.Create(Rule, argumentExpression.GetLocation(), targetName);
            context.ReportDiagnostic(diagnostic);
            return;
        }
    }

    private static bool IsRuntimeDiagnosticsSourceFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(filePath), "RuntimeDiagnostics.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetForbiddenTargetName(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType?.ToDisplayString();
        if (string.Equals(containingType, "System.Diagnostics.Trace", StringComparison.Ordinal)
            && IsForbiddenTraceMethod(methodSymbol.Name))
        {
            return "Trace." + methodSymbol.Name;
        }

        return (containingType, methodSymbol.Name) switch
        {
            ("QudJP.QudJPMod", "LogToUnity") => "QudJPMod.LogToUnity",
            ("QudJP.RuntimeDiagnostics", "LogStatus") => "RuntimeDiagnostics.LogStatus",
            ("QudJP.RuntimeDiagnostics", "LogWarning") => "RuntimeDiagnostics.LogWarning",
            ("QudJP.RuntimeDiagnostics", "LogError") => "RuntimeDiagnostics.LogError",
            ("UnityEngine.Debug", "Log") => "Debug.Log",
            ("UnityEngine.Debug", "LogWarning") => "Debug.LogWarning",
            ("UnityEngine.Debug", "LogError") => "Debug.LogError",
            ("UnityEngine.Debug", "LogAssertion") => "Debug.LogAssertion",
            ("UnityEngine.Debug", "LogFormat") => "Debug.LogFormat",
            ("UnityEngine.Debug", "LogWarningFormat") => "Debug.LogWarningFormat",
            ("UnityEngine.Debug", "LogErrorFormat") => "Debug.LogErrorFormat",
            ("UnityEngine.Debug", "LogAssertionFormat") => "Debug.LogAssertionFormat",
            _ => null,
        };
    }

    private static bool IsForbiddenTraceMethod(string methodName)
    {
        return methodName is "TraceInformation"
            or "TraceWarning"
            or "TraceError"
            or "Write"
            or "WriteIf"
            or "WriteLine"
            or "WriteLineIf";
    }

    private static bool ContainsVerboseProbeMarker(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var constantValue = semanticModel.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string constantText)
        {
            return ContainsVerboseProbeMarker(constantText);
        }

        var staticText = GetStaticStringText(expression);
        if (staticText is not null)
        {
            return ContainsVerboseProbeMarker(staticText);
        }

        if (TryGetLocalInitializer(expression, semanticModel, cancellationToken, out var initializer))
        {
            return ContainsVerboseProbeMarker(initializer, semanticModel, cancellationToken);
        }

        return false;
    }

    private static bool ContainsVerboseProbeMarker(string text)
    {
        if (text.Contains("no pattern for", StringComparison.Ordinal))
        {
            return true;
        }

        if (!text.Contains("[QudJP]", StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < QudJPVerboseMarkerFragments.Length; index++)
        {
            if (text.Contains(QudJPVerboseMarkerFragments[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetStaticStringText(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression)
                => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => GetInterpolatedStringText(interpolated),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression)
                => GetStaticStringText(binary.Left) + GetStaticStringText(binary.Right),
            _ => null,
        };
    }

    private static string GetInterpolatedStringText(InterpolatedStringExpressionSyntax interpolated)
    {
        var text = string.Empty;
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax stringText)
            {
                text += stringText.TextToken.ValueText;
            }
        }

        return text;
    }

    private static ExpressionSyntax UnwrapParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool TryGetLocalInitializer(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax initializer)
    {
        initializer = null!;
        expression = UnwrapParentheses(expression);
        if (expression is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
        if (symbol is not ILocalSymbol localSymbol)
        {
            return false;
        }

        foreach (var syntaxReference in localSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Initializer.Value: { } value })
            {
                initializer = value;
                return true;
            }
        }

        return false;
    }
}
