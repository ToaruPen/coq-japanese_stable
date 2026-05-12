using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QudJP.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PipelineTranslatorEntrypointAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QJ005";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Pipeline translator call must be crash-safe",
        messageFormat: "Pipeline method '{0}' must call translator '{1}' through a crash-safe helper or a local try-catch",
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
        if (context.Node is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !memberAccess.Name.Identifier.ValueText.StartsWith("TryTranslate", StringComparison.Ordinal))
        {
            return;
        }

        var containingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (containingMethod is null || !IsPipelineEntrypoint(containingMethod))
        {
            return;
        }

        if (IsInsideExceptionCatchTry(invocation, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (methodSymbol is null || IsSameContainingType(containingMethod, methodSymbol))
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            containingMethod.Identifier.ValueText,
            methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsPipelineEntrypoint(MethodDeclarationSyntax method)
    {
        if (method.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return false;
        }

        return typeDeclaration.Identifier.ValueText switch
        {
            "MessageQueueSemanticPipeline" => method.Identifier.ValueText == "TryTranslateQueuedMessage",
            "PopupShowSemanticPipeline" => method.Identifier.ValueText == "TranslateMessage",
            _ => false,
        };
    }

    private static bool IsSameContainingType(MethodDeclarationSyntax containingMethod, IMethodSymbol methodSymbol)
    {
        if (containingMethod.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return false;
        }

        return methodSymbol.ContainingType?.Name == typeDeclaration.Identifier.ValueText;
    }

    private static bool IsInsideExceptionCatchTry(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var tryStatement in node.Ancestors().OfType<TryStatementSyntax>())
        {
            if (!tryStatement.Block.Span.Contains(node.SpanStart))
            {
                continue;
            }

            for (var index = 0; index < tryStatement.Catches.Count; index++)
            {
                if (IsExceptionCatch(tryStatement.Catches[index], semanticModel, cancellationToken))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsExceptionCatch(
        CatchClauseSyntax catchClause,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var declaration = catchClause.Declaration;
        if (declaration is null)
        {
            return false;
        }

        var typeSymbol = semanticModel.GetTypeInfo(declaration.Type, cancellationToken).Type;
        return typeSymbol?.ToDisplayString() == "System.Exception"
            || GetUnqualifiedName(declaration.Type) == "Exception";
    }

    private static string GetUnqualifiedName(TypeSyntax typeSyntax)
    {
        return typeSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => typeSyntax.ToString(),
        };
    }
}
