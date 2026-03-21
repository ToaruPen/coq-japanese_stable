namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class ColorRestoreOwnershipTests
{
    [Test]
    public void ColorCodePreserverRestore_IsOnlyUsedInsideComposer()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var sourceRoot = Path.Combine(root, "Mods", "QudJP", "Assemblies", "src");
        var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        var violations = new List<string>();
        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            var file = files[fileIndex];
            if (file.EndsWith("ColorAwareTranslationComposer.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!lines[lineIndex].Contains("ColorCodePreserver.Restore(", StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/') + ":" + (lineIndex + 1));
            }
        }

        Assert.That(violations, Is.Empty, "Direct Restore callers: " + string.Join(", ", violations));
    }
}
