using System;
using System.IO;
using System.Reflection;

#if HAS_GAME_DLL
using XRL;
using XRL.Messages;
#endif

namespace QudJP.QudTest;

public static class QudTestRuntimeEntrypoint
{
    public static void Run(string command)
    {
        var fixtureDirectory = ResolveFixtureDirectory();
        var outputDirectory = ResolveOutputDirectory();
        var result = QudTestRunner.Run(command, fixtureDirectory, modLanguage: "ja");
        var resultPath = QudTestArtifactWriter.Write(outputDirectory, result);
        Report("QudJP QudTest wrote " + resultPath + " passed=" + result.Passed);
    }

    internal static string ResolveFixtureDirectory()
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrEmpty(assemblyDirectory))
        {
            throw new InvalidOperationException("Could not resolve QudJP assembly directory.");
        }

        var modDirectory = Directory.GetParent(assemblyDirectory)?.FullName;
        if (string.IsNullOrEmpty(modDirectory))
        {
            throw new InvalidOperationException("Could not resolve QudJP mod directory.");
        }

        return Path.Combine(modDirectory, "QudTest", "fixtures");
    }

    internal static string ResolveOutputDirectory()
    {
#if HAS_GAME_DLL
        return DataManager.LocalPath("QudTest");
#else
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Freehold Games",
            "CavesOfQud",
            "QudTest");
#endif
    }

    private static void Report(string message)
    {
#if HAS_GAME_DLL
        MessageQueue.AddPlayerMessage(message);
        UnityEngine.Debug.Log("[QudJP] " + message);
#else
        _ = message;
#endif
    }
}
