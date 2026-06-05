using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class AnnalsPatternsCandidateInventoryTests
{
    private static string RepositoryRoot => TestProjectPaths.GetRepositoryRoot();

    [Test]
    public void AnnalsDictionaryPatterns_AreRegisteredAsAcceptedCandidates()
    {
        var dictionary = ReadJson<JournalPatternDocumentDto>(
            Path.Combine(RepositoryRoot, "Mods", "QudJP", "Localization", "Dictionaries", "annals-patterns.ja.json"));
        var candidates = ReadJson<AnnalsCandidateDocumentDto>(
            Path.Combine(RepositoryRoot, "scripts", "_artifacts", "annals", "candidates_pending.json"));

        Assert.That(dictionary.Patterns, Is.Not.Null);
        Assert.That(candidates.Candidates, Is.Not.Null);

        var accepted = candidates.Candidates!
            .Where(static candidate => candidate.Status == "accepted")
            .Select(static candidate => PatternKey(candidate.ExtractedPattern, candidate.JaTemplate))
            .ToHashSet(System.StringComparer.Ordinal);

        foreach (var pattern in dictionary.Patterns!)
        {
            Assert.That(
                accepted,
                Does.Contain(PatternKey(pattern.Pattern, pattern.Template)),
                "annals-patterns.ja.json contains a pattern/template pair that is not registered as an accepted candidate: "
                + pattern.Pattern);
        }
    }

    [Test]
    public void AnnalsCandidates_HaveNoNeedsManualEntries()
    {
        var candidates = ReadJson<AnnalsCandidateDocumentDto>(
            Path.Combine(RepositoryRoot, "scripts", "_artifacts", "annals", "candidates_pending.json"));

        Assert.That(
            candidates.Candidates,
            Is.Not.Null,
            "scripts/_artifacts/annals/candidates_pending.json must contain a candidates array.");
        Assert.That(
            candidates.Candidates!
                .Where(static candidate => candidate.Status == "needs_manual")
                .Select(static candidate => candidate.Id),
            Is.Empty,
            "annals candidate inventory contains unexpected needs_manual candidate IDs.");
    }

    private static T ReadJson<T>(string path)
    {
        using var stream = File.OpenRead(path);
        var serializer = new DataContractJsonSerializer(typeof(T));
        var result = serializer.ReadObject(stream);
        Assert.That(result, Is.Not.Null, $"failed to deserialize {path}");
        return (T)result!;
    }

    private static string PatternKey(string? pattern, string? template) =>
        (pattern ?? string.Empty) + "\u001f" + (template ?? string.Empty);
}

[DataContract]
internal sealed class AnnalsCandidateDocumentDto
{
    [DataMember(Name = "candidates")]
    public List<AnnalsCandidateDto>? Candidates { get; set; }
}

[DataContract]
internal sealed class AnnalsCandidateDto
{
    [DataMember(Name = "id")]
    public string? Id { get; set; }

    [DataMember(Name = "extracted_pattern")]
    public string? ExtractedPattern { get; set; }

    [DataMember(Name = "status")]
    public string? Status { get; set; }

    [DataMember(Name = "ja_template")]
    public string? JaTemplate { get; set; }
}
