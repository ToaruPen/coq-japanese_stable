#if HAS_TMP
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UguiText = UnityEngine.UI.Text;
#endif
using System;
#if !HAS_TMP
using System.Diagnostics;
#endif
using System.IO;
using System.Reflection;
using System.Threading;

namespace QudJP;

public static class FontManager
{
    private static int isInitialized;

#if HAS_TMP
    private static readonly string[] VanillaLegacyFontNames =
    {
        "LiberationSans",
        "Liberation Sans",
    };

    private static readonly string[] LegacyFallbackOsFontNames =
    {
        "Noto Sans CJK JP",
        "Hiragino Sans",
        "Hiragino Kaku Gothic ProN",
        "Yu Gothic UI",
        "Yu Gothic",
        "MS Gothic",
        "Arial Unicode MS",
    };

    private static TMP_FontAsset? primaryFontAsset;
    private static Font? legacyFont;
#endif

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref isInitialized, 1) == 1)
        {
            return;
        }

#if HAS_TMP
        try
        {
            var fontPath = ResolveFontPath();
            if (!File.Exists(fontPath))
            {
                throw new FileNotFoundException($"CJK font not found: {fontPath}", fontPath);
            }

            Debug.Log($"[QudJP] FontManager: Loading CJK font from {fontPath}");

            var fontAsset = TMP_FontAsset.CreateFontAsset(fontPath, 0, 96, 6, GlyphRenderMode.SDFAA, 4096, 4096);
#pragma warning disable CA1508 // Unity objects may be null despite non-nullable annotations
            if (fontAsset is null)
            {
                throw new InvalidOperationException("TMP_FontAsset.CreateFontAsset returned null");
            }
#pragma warning restore CA1508

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;

            primaryFontAsset = fontAsset;
            legacyFont = fontAsset.sourceFontFile;
            if (legacyFont is null)
            {
                legacyFont = TryCreateLegacyFallbackFont();
            }

            fontAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

            var previousDefaultFont = TMP_Settings.defaultFontAsset;
            if (previousDefaultFont is not null && !ReferenceEquals(previousDefaultFont, fontAsset))
            {
                EnsureFontListed(fontAsset.fallbackFontAssetTable, previousDefaultFont, prepend: false);
            }

            TMP_Settings.defaultFontAsset = fontAsset;

            TMP_Settings.fallbackFontAssets ??= new List<TMP_FontAsset>();
            EnsureFontListed(TMP_Settings.fallbackFontAssets, fontAsset, prepend: true);
            if (previousDefaultFont is not null && !ReferenceEquals(previousDefaultFont, fontAsset))
            {
                EnsureFontListed(TMP_Settings.fallbackFontAssets, previousDefaultFont, prepend: false);
            }

            var patchedFontAssetCount = EnsureFallbackOnAllFontAssets(fontAsset);

            if (!fontAsset.TryAddCharacters("日本語テスト"))
            {
                throw new InvalidOperationException("TryAddCharacters failed for probe string '日本語テスト'");
            }

            Debug.Log($"[QudJP] FontManager: CJK font registered. defaultFontAsset='{fontAsset.name}', patchedAssets={patchedFontAssetCount}.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QudJP] FontManager failed: {ex}");
            throw;
        }
#else
        Trace.TraceInformation("[QudJP] FontManager: TMP unavailable (CI build). Font injection skipped.");
#endif
    }

#if HAS_TMP
    internal static void ApplyToText(TMP_Text text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var fontAsset = primaryFontAsset
            ?? throw new InvalidOperationException("QudJP FontManager: primary TMP font asset is not initialized.");

        if (text.font is null)
        {
            text.font = fontAsset;
        }
        else
        {
            EnsureFallbackChain(text.font, fontAsset);
        }

        RefreshAfterFontAssignment(text);
    }

    internal static void ForcePrimaryFont(TMP_Text text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var fontAsset = primaryFontAsset
            ?? throw new InvalidOperationException("QudJP FontManager: primary TMP font asset is not initialized.");

        if (text.font is not null && !ReferenceEquals(text.font, fontAsset))
        {
            EnsureFallbackChain(text.font, fontAsset);
        }

        text.font = fontAsset;
        RefreshAfterFontAssignment(text);
    }

    internal static bool TryWarmPrimaryFontCharactersForUi(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var fontAsset = primaryFontAsset;
        if (fontAsset is null)
        {
            Debug.LogWarning("[QudJP] FontManager: primary TMP font asset is not initialized; skipping UI glyph warmup.");
            return false;
        }

        try
        {
            var (stripped, _) = ColorCodePreserver.Strip(text);
            if (string.IsNullOrEmpty(stripped))
            {
                return false;
            }

            return fontAsset.TryAddCharacters(stripped, out _);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QudJP] FontManager: UI glyph warmup failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    internal static TMP_FontAsset? GetPrimaryFontAssetForDiagnostics()
    {
        return primaryFontAsset;
    }

    internal static void ApplyToInputField(TMP_InputField inputField)
    {
        if (inputField is null)
        {
            throw new ArgumentNullException(nameof(inputField));
        }

        if (inputField.textComponent is not null)
        {
            ApplyToText(inputField.textComponent);
        }

        if (inputField.placeholder is TMP_Text placeholder)
        {
            ApplyToText(placeholder);
        }
    }

    internal static void ApplyToLegacyText(UguiText text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (text.font is not null && !ShouldApplyLegacyFallback(text))
        {
            return;
        }

        legacyFont ??= TryCreateLegacyFallbackFont();

        var fallbackFont = legacyFont
            ?? throw new InvalidOperationException("QudJP FontManager: legacy font is not initialized.");

        if (text.font is null || IsVanillaLegacyFont(text.font))
        {
            text.font = fallbackFont;
        }
    }

    private static Font? TryCreateLegacyFallbackFont()
    {
        try
        {
            var font = Font.CreateDynamicFontFromOSFont(LegacyFallbackOsFontNames, 16);
            Debug.Log($"[QudJP] FontManager: legacy fallback font initialized from OS font '{font.name}'.");

            return font;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[QudJP] FontManager: failed to initialize legacy fallback font: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string ResolveFontPath()
    {
        var asmPath = Assembly.GetExecutingAssembly().Location;
        string asmDir;
        if (string.IsNullOrWhiteSpace(asmPath))
        {
            asmDir = AppContext.BaseDirectory;
        }
        else
        {
            var dirName = Path.GetDirectoryName(asmPath);
            asmDir = string.IsNullOrWhiteSpace(dirName) ? AppContext.BaseDirectory : dirName;
        }

        var modRoot = Directory.GetParent(asmDir);
        return Path.Combine(modRoot is null ? asmDir : modRoot.FullName, "Fonts", "NotoSansCJKjp-Regular-Subset.otf");
    }

    private static int EnsureFallbackOnAllFontAssets(TMP_FontAsset fontAsset)
    {
        var patchedCount = 0;
        var existingAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (var index = 0; index < existingAssets.Length; index++)
        {
            var existingAsset = existingAssets[index];
            if (existingAsset is null || ReferenceEquals(existingAsset, fontAsset))
            {
                continue;
            }

            if (EnsureFallbackChain(existingAsset, fontAsset))
            {
                patchedCount++;
            }
        }

        return patchedCount;
    }

    private static bool EnsureFallbackChain(TMP_FontAsset targetAsset, TMP_FontAsset fallbackAsset)
    {
        if (ReferenceEquals(targetAsset, fallbackAsset))
        {
            return false;
        }

        targetAsset.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        return EnsureFontListed(targetAsset.fallbackFontAssetTable, fallbackAsset, prepend: true);
    }

    private static bool EnsureFontListed(List<TMP_FontAsset>? fontList, TMP_FontAsset fontAsset, bool prepend)
    {
        if (fontList is null)
        {
            throw new InvalidOperationException("QudJP FontManager: target font list is null.");
        }

        var existingIndex = fontList.FindIndex(candidate => ReferenceEquals(candidate, fontAsset));
        if (existingIndex == 0 && prepend)
        {
            return false;
        }

        if (existingIndex >= 0)
        {
            fontList.RemoveAt(existingIndex);
        }

        if (prepend)
        {
            fontList.Insert(0, fontAsset);
        }
        else
        {
            fontList.Add(fontAsset);
        }

        return true;
    }

    private static void RefreshAfterFontAssignment(TMP_Text text)
    {
        text.havePropertiesChanged = true;
        text.UpdateMeshPadding();
        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
    }

    private static bool IsVanillaLegacyFont(Font font)
    {
        return MatchesKnownFontName(font.name, VanillaLegacyFontNames);
    }

    private static bool MatchesKnownFontName(string? value, string[] candidates)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        for (var index = 0; index < candidates.Length; index++)
        {
            if (string.Equals(value, candidates[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldApplyLegacyFallback(UguiText text)
    {
        var value = text.text;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] > 0x7F)
            {
                return true;
            }
        }

        return false;
    }

#endif
}
