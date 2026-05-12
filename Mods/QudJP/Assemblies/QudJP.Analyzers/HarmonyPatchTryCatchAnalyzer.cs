using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace QudJP.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HarmonyPatchTryCatchAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QJ001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Harmony patch body must have try-catch",
        messageFormat: "Method '{0}' in [HarmonyPatch] class '{1}' must wrap its body in try-catch and log exceptions with Trace.TraceError",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.ValueText is "TargetMethod" or "TargetMethods")
        {
            return;
        }

        if (method.Parent is not TypeDeclarationSyntax containingType)
        {
            return;
        }

        if (!HasHarmonyAttribute(containingType.AttributeLists) || !IsHarmonyPatchMethod(method))
        {
            return;
        }

        if (method.Body is null || !IsBodyWrappedWithTryCatch(method.Body, context.SemanticModel, context.CancellationToken))
        {
            var diagnostic = Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.ValueText, containingType.Identifier.ValueText);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsBodyWrappedWithTryCatch(
        BlockSyntax body,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (body.Statements.Count != 1 || body.Statements[0] is not TryStatementSyntax tryStatement)
        {
            return false;
        }

        for (var index = 0; index < tryStatement.Catches.Count; index++)
        {
            if (IsExceptionCatch(tryStatement.Catches[index], semanticModel, cancellationToken))
            {
                return true;
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
            return true;
        }

        var typeSymbol = semanticModel.GetTypeInfo(declaration.Type, cancellationToken).Type;
        if (typeSymbol?.ToDisplayString() == "System.Exception")
        {
            return true;
        }

        var typeName = GetUnqualifiedName(declaration.Type);
        return typeName == "Exception";
    }

    private static bool IsHarmonyPatchMethod(MethodDeclarationSyntax method)
    {
        if (method.Identifier.ValueText is "Prefix" or "Postfix")
        {
            return true;
        }

        return HasAnyAttribute(method.AttributeLists, static attributeName =>
            attributeName is "HarmonyPrefix" or "HarmonyPrefixAttribute" or "HarmonyPostfix" or "HarmonyPostfixAttribute");
    }

    private static bool HasHarmonyAttribute(SyntaxList<AttributeListSyntax> attributes)
    {
        return HasAnyAttribute(attributes, static attributeName =>
            attributeName.StartsWith("Harmony", System.StringComparison.Ordinal));
    }

    private static bool HasAnyAttribute(
        SyntaxList<AttributeListSyntax> attributes,
        System.Func<string, bool> predicate)
    {
        for (var listIndex = 0; listIndex < attributes.Count; listIndex++)
        {
            var attributeList = attributes[listIndex];
            for (var attributeIndex = 0; attributeIndex < attributeList.Attributes.Count; attributeIndex++)
            {
                var attribute = attributeList.Attributes[attributeIndex];
                var name = GetUnqualifiedName(attribute.Name);
                if (predicate(name))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static string GetUnqualifiedName(NameSyntax nameSyntax)
    {
        return nameSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => nameSyntax.ToString(),
        };
    }
}
