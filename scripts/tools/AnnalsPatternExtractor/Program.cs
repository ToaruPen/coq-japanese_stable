using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QudJP.Tools.AnnalsPatternExtractor;

string? sourceRoot = null;
string? include = null;
string? output = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source-root":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --source-root"); return 2; }
            sourceRoot = args[++i]; break;
        case "--include":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --include"); return 2; }
            include = args[++i]; break;
        case "--output":
            if (i + 1 >= args.Length) { Console.Error.WriteLine("Missing value for --output"); return 2; }
            output = args[++i]; break;
        case "--help":
            Console.Out.WriteLine("Usage: AnnalsPatternExtractor --source-root <dir> --include <glob> --output <json-path>");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (sourceRoot is null || include is null || output is null)
{
    Console.Error.WriteLine("Missing required argument. Use --help.");
    return 2;
}

if (!Directory.Exists(sourceRoot))
{
    Console.Error.WriteLine($"--source-root does not exist: {sourceRoot}");
    return 1;
}

var globPattern = include;
var files = Directory.GetFiles(sourceRoot, globPattern, SearchOption.TopDirectoryOnly)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

if (files.Count == 0)
{
    Console.Error.WriteLine($"No files matched --include '{include}' under {sourceRoot}");
    return 1;
}

var extractor = new Extractor();
foreach (var file in files)
{
    Console.Out.WriteLine($"[extract] processing {Path.GetFileName(file)}");
    extractor.ProcessFile(file);
}

foreach (var diag in extractor.Diagnostics)
{
    Console.Error.WriteLine($"[warn] {diag}");
}

var doc = new CandidateDocument
{
    SchemaVersion = "1",
    Candidates = extractor.Candidates.OrderBy(c => c.Id, StringComparer.Ordinal).ToList(),
};
CandidateWriter.WriteToFile(output, doc);

Console.Out.WriteLine($"[extract] wrote {doc.Candidates.Count} candidate(s) to {output}");
return 0;
