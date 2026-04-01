using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ModManagementTranslationPatchTests
{
    [TestCase("{{y|by Example Author}}", "{{y|作者: Example Author}}")]
    [TestCase("{{C|by Example Author}}", "{{C|作者: Example Author}}")]
    [TestCase("by Example Author", "作者: Example Author")]
    [TestCase("Example Author", "Example Author")]
    public void ModMenuLine_TranslateAuthorLabel_TranslatesOnlyPrefix(string source, string expected)
    {
        var translated = ModMenuLineTranslationPatch.TranslateAuthorLabel(source);
        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("ConfirmDependencies", "{{W|Dependencies}}", "{{W|依存関係}}")]
    [TestCase("ConfirmUpdate", " has a new version available: ", "の新しいバージョンが利用可能です: ")]
    [TestCase("ConfirmUpdate", "\n\nDo you want to download it?", "\n\nダウンロードしますか？")]
    [TestCase("ConfirmUpdate", "{{W|Update Available}}", "{{W|更新あり}}")]
    [TestCase("DownloadUpdate", "Updating ", "")]
    [TestCase("DownloadUpdate", "...", "を更新中…")]
    [TestCase("AppendDependencyConfirmation", "Invalid", "無効")]
    [TestCase("AppendDependencyConfirmation", "OK", "OK")]
    [TestCase("AppendDependencyConfirmation", "Version mismatch", "バージョン不一致")]
    [TestCase("AppendDependencyConfirmation", "Missing", "未検出")]
    [TestCase("OtherMethod", "Upload failed", "Upload failed")]
    public void ModInfo_TranslateLiteralForTests_ReturnsExpectedValue(string methodName, string source, string expected)
    {
        var translated = ModInfoTranslationPatch.TranslateLiteralForTests(methodName, source);
        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("Committing changes...", "変更を確定中…")]
    [TestCase("Submitting update... Please wait.", "更新を送信中…お待ちください。")]
    [TestCase("Upload complete!", "アップロード完了！")]
    [TestCase("Upload failed", "アップロード失敗")]
    [TestCase("Error: I/O Failure!", "エラー: I/O失敗!")]
    [TestCase("Error: I/O Failure! :(", "エラー: I/O失敗! :(")]
    [TestCase("Unknown result: Timeout", "Unknown result: Timeout")]
    public void SteamWorkshopUploaderView_TranslateText_ReturnsDictionaryBackedValue(string source, string expected)
    {
        using var tempDirectory = new TempDirectoryScope("qudjp-mod-management-l1");
        Translator.SetDictionaryDirectoryForTests(tempDirectory.Path);
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "ui-modpage.ja.json"),
            """
            {"entries":[
              {"key":"Committing changes...","text":"変更を確定中…"},
              {"key":"Submitting update... Please wait.","text":"更新を送信中…お待ちください。"},
              {"key":"Upload complete!","text":"アップロード完了！"},
              {"key":"Upload failed","text":"アップロード失敗"},
              {"key":"Error: I/O Failure!","text":"エラー: I/O失敗!"},
              {"key":"Error: I/O Failure! :(","text":"エラー: I/O失敗! :("}
            ]}
            """);

        var translated = SteamWorkshopUploaderViewTranslationPatch.TranslateText(source);
        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void ModScrollerOne_TranslateLiteralForTests_TranslatesDisabledScriptsSuffix()
    {
        const string source =
            " contains scripts and has been permanently disabled in the options.\n{{K|(Options->Modding->Allow scripting mods)}}";

        var translated = ModScrollerOneTranslationPatch.TranslateLiteralForTests(source);

        Assert.That(
            translated,
            Is.EqualTo(" にはスクリプトが含まれていますが、オプションで永続的に無効化されています。\n{{K|(オプション->Mod->スクリプトModを許可)}}"));
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope(string prefix)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Translator.ResetForTests();
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
