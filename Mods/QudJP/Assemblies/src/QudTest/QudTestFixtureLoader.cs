using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace QudJP.QudTest;

public static class QudTestFixtureLoader
{
    public static IReadOnlyList<QudTestFixtureDocument> LoadDirectory(string fixturesDirectory)
    {
        if (string.IsNullOrWhiteSpace(fixturesDirectory))
        {
            throw new ArgumentException("fixturesDirectory must be non-empty.", nameof(fixturesDirectory));
        }

        if (!Directory.Exists(fixturesDirectory))
        {
            throw new DirectoryNotFoundException("QudTest fixtures directory not found: " + fixturesDirectory);
        }

        var documents = new List<QudTestFixtureDocument>();
        foreach (var path in Directory.GetFiles(fixturesDirectory, "*.json").OrderBy(static path => path, StringComparer.Ordinal))
        {
            var document = JsonAssetLoader.LoadFromFile<QudTestFixtureDocument>(path);
            Validate(document, path);
            documents.Add(document);
        }

        if (documents.Count == 0)
        {
            throw new InvalidDataException("No QudTest fixture files found in " + fixturesDirectory);
        }

        return documents;
    }

    private static void Validate(QudTestFixtureDocument document, string path)
    {
        if (document.SchemaVersion != 1)
        {
            throw new SerializationException("Unsupported QudTest fixture schemaVersion in " + path);
        }

        if (string.IsNullOrWhiteSpace(document.Suite))
        {
            throw new SerializationException("QudTest fixture suite is required in " + path);
        }

        if (document.Cases.Count == 0)
        {
            throw new SerializationException("QudTest fixture contains no cases in " + path);
        }

        foreach (var testCase in document.Cases)
        {
            if (string.IsNullOrWhiteSpace(testCase.Id)
                || string.IsNullOrWhiteSpace(testCase.Route))
            {
                throw new SerializationException("QudTest fixture case id and route are required in " + path);
            }
        }
    }
}
