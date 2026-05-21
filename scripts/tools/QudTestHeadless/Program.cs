using System;
using System.IO;
using QudJP;
using QudJP.Patches;
using QudJP.QudTest;

namespace QudTestHeadless;

internal static class Program
{
    private const string DefaultCommand = "qudtest";
    private const string DefaultFixtures = "Mods/QudJP/QudTest/fixtures";
    private const string DefaultOutput = ".artifacts/qudtest";
    private const string DefaultModLanguage = "ja";

    public static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            ConfigureHeadlessLocalization(options.ProjectRoot);

            var result = QudTestRunner.Run(options.Command, options.FixturesDirectory, options.ModLanguage);
            var resultPath = QudTestArtifactWriter.Write(options.OutputDirectory, result);
            Console.WriteLine(resultPath);
            Console.WriteLine("passed=" + (result.Passed ? "true" : "false"));
            Console.WriteLine("total=" + result.TotalCount);
            Console.WriteLine("failed=" + result.FailCount);
            return result.Passed ? 0 : 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static void ConfigureHeadlessLocalization(string projectRoot)
    {
        var dictionariesDirectory = Path.Combine(projectRoot, "Mods", "QudJP", "Localization", "Dictionaries");
        Translator.SetDictionaryDirectoryForTests(dictionariesDirectory);
        StartReplaceTranslationPatch.SetDictionaryPathForTests(Path.Combine(dictionariesDirectory, "templates-variable.ja.json"));
    }

    private sealed record CliOptions(
        string Command,
        string FixturesDirectory,
        string OutputDirectory,
        string ModLanguage,
        string ProjectRoot)
    {
        public static CliOptions Parse(string[] args)
        {
            var command = DefaultCommand;
            var fixtures = DefaultFixtures;
            var output = DefaultOutput;
            var modLanguage = DefaultModLanguage;
            string? projectRoot = null;

            for (var index = 0; index < args.Length; index++)
            {
                var arg = args[index];
                switch (arg)
                {
                    case "--command":
                        command = ReadValue(args, ref index, arg);
                        break;
                    case "--fixtures":
                        fixtures = ReadValue(args, ref index, arg);
                        break;
                    case "--output":
                        output = ReadValue(args, ref index, arg);
                        break;
                    case "--mod-language":
                        modLanguage = ReadValue(args, ref index, arg);
                        break;
                    case "--project-root":
                        projectRoot = ReadValue(args, ref index, arg);
                        break;
                    case "-h":
                    case "--help":
                        throw new ArgumentException(
                            "Usage: QudTestHeadless [--command qudtest[:suite]] [--fixtures path] [--output path] [--mod-language ja] [--project-root path]");
                    default:
                        throw new ArgumentException("Unknown argument: " + arg);
                }
            }

            var resolvedRoot = Path.GetFullPath(projectRoot ?? Directory.GetCurrentDirectory());
            return new CliOptions(
                RequireNonEmpty(command, "--command"),
                ResolvePath(resolvedRoot, RequireNonEmpty(fixtures, "--fixtures")),
                ResolvePath(resolvedRoot, RequireNonEmpty(output, "--output")),
                RequireNonEmpty(modLanguage, "--mod-language"),
                resolvedRoot);
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException(option + " requires a value.");
            }

            index++;
            return args[index];
        }

        private static string RequireNonEmpty(string value, string option)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(option + " must be non-empty.");
            }

            return value;
        }

        private static string ResolvePath(string projectRoot, string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));
        }
    }
}
