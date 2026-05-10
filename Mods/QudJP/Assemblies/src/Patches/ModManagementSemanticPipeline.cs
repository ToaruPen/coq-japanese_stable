using System;

namespace QudJP.Patches;

internal static class ModManagementSemanticPipeline
{
    private const string AuthorPrefix = "by ";
    private const string JapaneseAuthorPrefix = "作者: ";
    private const string DisabledScriptsSuffix =
        " contains scripts and has been permanently disabled in the options.\n{{K|(Options->Modding->Allow scripting mods)}}";
    private const string DisabledScriptsSuffixJa =
        " にはスクリプトが含まれていますが、オプションで永続的に無効化されています。\n{{K|(オプション->Mod->スクリプトModを許可)}}";

    internal static string TranslateLiteral(string methodName, string source)
    {
        return methodName switch
        {
            "ConfirmDependencies" => TranslateConfirmDependenciesLiteral(source),
            "ConfirmUpdate" => TranslateConfirmUpdateLiteral(source),
            "DownloadUpdate" => TranslateDownloadUpdateLiteral(source),
            "AppendDependencyConfirmation" => TranslateAppendDependencyConfirmationLiteral(source),
            _ => source,
        };
    }

    internal static string TranslateTagText(string source)
    {
        return TranslateDictionaryBackedText(source);
    }

    internal static string TranslateWorkshopUploaderText(string source)
    {
        return TranslateDictionaryBackedText(source);
    }

    private static string TranslateDictionaryBackedText(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        return StringHelpers.TryGetTranslationExactOrLowerAscii(source, out var translated)
            ? translated
            : source;
    }

    internal static string TranslateDisabledScriptsSuffix(string source)
    {
        return string.Equals(source, DisabledScriptsSuffix, StringComparison.Ordinal)
            ? DisabledScriptsSuffixJa
            : source;
    }

    internal static string TranslateAuthorLabel(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var authorPrefixIndex = source.IndexOf(AuthorPrefix, StringComparison.Ordinal);
        if (authorPrefixIndex < 0
            || (authorPrefixIndex != 0 && source[authorPrefixIndex - 1] != '|'))
        {
            return source;
        }

        return source.Substring(0, authorPrefixIndex)
            + JapaneseAuthorPrefix
            + source.Substring(authorPrefixIndex + AuthorPrefix.Length);
    }

    private static string TranslateConfirmDependenciesLiteral(string source)
    {
        return string.Equals(source, "{{W|Dependencies}}", StringComparison.Ordinal)
            ? "{{W|依存関係}}"
            : source;
    }

    private static string TranslateConfirmUpdateLiteral(string source)
    {
        return source switch
        {
            " has a new version available: " => "の新しいバージョンが利用可能です: ",
            "\n\nDo you want to download it?" => "\n\nダウンロードしますか？",
            ".\n\nDo you want to download it?" => ".\n\nダウンロードしますか？",
            "{{W|Update Available}}" => "{{W|更新あり}}",
            _ => source,
        };
    }

    private static string TranslateDownloadUpdateLiteral(string source)
    {
        return source switch
        {
            "Updating " => string.Empty,
            "..." => "を更新中…",
            _ => source,
        };
    }

    private static string TranslateAppendDependencyConfirmationLiteral(string source)
    {
        return source switch
        {
            "Invalid" => "無効",
            "Version mismatch" => "バージョン不一致",
            "Missing" => "未検出",
            _ => source,
        };
    }
}
