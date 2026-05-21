using System;
using System.Collections.Generic;
using System.Linq;

namespace QudJP.QudTest;

public static class QudTestRunner
{
    public static QudTestRunResult Run(string command, string fixturesDirectory, string modLanguage)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (fixturesDirectory is null)
        {
            throw new ArgumentNullException(nameof(fixturesDirectory));
        }

        if (modLanguage is null)
        {
            throw new ArgumentNullException(nameof(modLanguage));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("command must be non-empty.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(fixturesDirectory))
        {
            throw new ArgumentException("fixturesDirectory must be non-empty.", nameof(fixturesDirectory));
        }

        if (string.IsNullOrWhiteSpace(modLanguage))
        {
            throw new ArgumentException("modLanguage must be non-empty.", nameof(modLanguage));
        }

        var suite = ParseSuite(command);
        var startedAt = DateTimeOffset.UtcNow;
        var result = new QudTestRunResult
        {
            Command = command,
            Suite = suite,
            ModLanguage = modLanguage,
            StartedAtUtc = startedAt,
        };

        if (string.Equals(suite, "bindings-all", StringComparison.Ordinal))
        {
            result.Cases.AddRange(QudTestPatchBindingExecutor.ExecuteAll());
        }
        else
        {
            var documents = QudTestFixtureLoader.LoadDirectory(fixturesDirectory);
            foreach (var testCase in EnumerateCases(documents, suite))
            {
                result.Cases.Add(ExecuteCase(testCase));
            }
        }

        if (result.Cases.Count == 0)
        {
            result.Cases.Add(NoCasesMatched(suite));
        }

        result.EndedAtUtc = DateTimeOffset.UtcNow;
        result.TotalCount = result.Cases.Count;
        result.PassCount = result.Cases.Count(static testCase => testCase.Passed);
        result.FailCount = result.TotalCount - result.PassCount;
        result.Passed = result.FailCount == 0;
        return result;
    }

    private static QudTestCaseResult NoCasesMatched(string suite)
    {
        return new QudTestCaseResult
        {
            Id = "qudtest.no-cases",
            Route = "suite",
            Input = suite,
            Expected = "at least one fixture case",
            Actual = string.Empty,
            Passed = false,
            Diagnostic = "no fixture cases matched suite: " + suite,
        };
    }

    private static IEnumerable<QudTestCase> EnumerateCases(IReadOnlyList<QudTestFixtureDocument> documents, string suite)
    {
        foreach (var document in documents)
        {
            if (!ShouldRunDocument(document, suite))
            {
                continue;
            }

            foreach (var testCase in document.Cases)
            {
                yield return testCase;
            }
        }
    }

    private static QudTestCaseResult ExecuteCase(QudTestCase testCase)
    {
        var expected = QudTestPatchBindingExecutor.Expected(testCase);
        try
        {
            var actual = QudTestRouteExecutor.Execute(testCase);
            var passed = string.Equals(actual, expected, StringComparison.Ordinal);
            return new QudTestCaseResult
            {
                Id = testCase.Id,
                Route = testCase.Route,
                Input = testCase.Input,
                Expected = expected,
                Actual = actual,
                Passed = passed,
                Diagnostic = passed ? string.Empty : "actual did not match expected",
            };
        }
        catch (Exception ex)
        {
            return new QudTestCaseResult
            {
                Id = testCase.Id,
                Route = testCase.Route,
                Input = testCase.Input,
                Expected = expected,
                Actual = string.Empty,
                Passed = false,
                Diagnostic = ex.GetType().Name + ": " + ex.Message,
            };
        }
    }

    private static string ParseSuite(string command)
    {
        const string prefix = "qudtest:";
        if (string.Equals(command, "qudtest", StringComparison.Ordinal))
        {
            return "all";
        }

        return command.StartsWith(prefix, StringComparison.Ordinal)
            ? command.Substring(prefix.Length)
            : command;
    }

    private static bool ShouldRunDocument(QudTestFixtureDocument document, string suite)
    {
        return string.Equals(suite, "all", StringComparison.Ordinal)
            || string.Equals(document.Suite, suite, StringComparison.Ordinal);
    }
}
