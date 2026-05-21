using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Wish;

namespace QudJP
{
    [HasModSensitiveStaticCache]
    public static class QudJPLoader
    {
        private static readonly object AssemblyLoadLock = new object();
        private static Assembly loadedAssembly;

        [ModSensitiveCacheInit]
        public static void Bootstrap()
        {
            var total = Stopwatch.StartNew();
            try
            {
                UnityEngine.Debug.Log("[QudJP] Bootstrap: resolving QudJP.dll path...");

                Assembly assembly;
                var loadAssembly = Stopwatch.StartNew();
                try
                {
                    assembly = GetOrLoadAssembly();
                }
                finally
                {
                    LogStartupTiming("bootstrap.load_assembly", loadAssembly.Elapsed);
                }

                Type modType = assembly.GetType("QudJP.QudJPMod");
                if (modType == null)
                {
                    UnityEngine.Debug.LogError("[QudJP] Bootstrap: type 'QudJP.QudJPMod' not found in assembly");
                    throw new InvalidOperationException("[QudJP] Bootstrap: type 'QudJP.QudJPMod' not found in assembly");
                }

                MethodInfo initMethod = modType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
                if (initMethod == null)
                {
                    UnityEngine.Debug.LogError("[QudJP] Bootstrap: method 'Init' not found on QudJP.QudJPMod");
                    throw new InvalidOperationException("[QudJP] Bootstrap: method 'Init' not found on QudJP.QudJPMod");
                }

                var invokeInit = Stopwatch.StartNew();
                try
                {
                    initMethod.Invoke(null, null);
                }
                finally
                {
                    LogStartupTiming("bootstrap.invoke_init", invokeInit.Elapsed);
                }

                UnityEngine.Debug.Log("[QudJP] Bootstrap: initialization complete.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[QudJP] Bootstrap failed: " + ex);
                throw;
            }
            finally
            {
                total.Stop();
                LogStartupTiming("bootstrap.total", total.Elapsed);
            }
        }

        public static void RunQudTest(string command)
        {
            try
            {
                Assembly assembly = GetOrLoadAssembly();
                Type qudTestType = assembly.GetType("QudJP.QudTest.QudTestRuntimeEntrypoint");
                if (qudTestType == null)
                {
                    throw new InvalidOperationException(
                        "[QudJP] Bootstrap: type 'QudJP.QudTest.QudTestRuntimeEntrypoint' not found in assembly");
                }

                MethodInfo qudTestRunMethod = qudTestType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (qudTestRunMethod == null)
                {
                    throw new InvalidOperationException(
                        "[QudJP] Bootstrap: method 'Run' not found on QudJP.QudTest.QudTestRuntimeEntrypoint");
                }

                qudTestRunMethod.Invoke(null, new object[] { command });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[QudJP] QudTest bridge failed: " + ex);
                throw;
            }
        }

        private static Assembly GetOrLoadAssembly(string modPath = null)
        {
            lock (AssemblyLoadLock)
            {
                if (loadedAssembly != null)
                {
                    return loadedAssembly;
                }

                string resolvedModPath = modPath ?? ResolveModPath();
                string dllPath = System.IO.Path.Combine(resolvedModPath, "Assemblies", "QudJP.dll");

                if (!File.Exists(dllPath))
                {
                    UnityEngine.Debug.LogError("[QudJP] Bootstrap: QudJP.dll not found at " + dllPath);
                    throw new FileNotFoundException("[QudJP] Bootstrap: QudJP.dll not found at " + dllPath, dllPath);
                }

                UnityEngine.Debug.Log("[QudJP] Bootstrap: loading assembly from " + dllPath);
                loadedAssembly = Assembly.LoadFrom(dllPath);
                return loadedAssembly;
            }
        }

        private static string ResolveModPath()
        {
            foreach (var mod in ModManager.Mods)
            {
                if (mod.ID == "QudJP")
                {
                    return mod.Path;
                }
            }

            UnityEngine.Debug.LogError("[QudJP] Bootstrap: mod 'QudJP' not found in ModManager.Mods");
            throw new InvalidOperationException("[QudJP] Bootstrap: mod 'QudJP' not found in ModManager.Mods");
        }

        private static void LogStartupTiming(string phase, TimeSpan elapsed)
        {
            UnityEngine.Debug.Log(
                "[QudJP] StartupTiming/v1: phase="
                + phase
                + " elapsed_ms="
                + elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    [HasWishCommand]
    public static class QudJPQudTestWishBridge
    {
        [WishCommand("qudtest", null)]
        public static void RunDefault()
        {
            QudJPLoader.RunQudTest("qudtest:all");
        }

        [WishCommand("qudtest:all", null)]
        public static void RunAll()
        {
            QudJPLoader.RunQudTest("qudtest:all");
        }

        [WishCommand("qudtest:runtime", null)]
        public static void RunRuntime()
        {
            QudJPLoader.RunQudTest("qudtest:runtime");
        }

        [WishCommand("qudtest:wish", null)]
        public static void RunWish()
        {
            QudJPLoader.RunQudTest("qudtest:wish");
        }

        [WishCommand("qudtest:bindings", null)]
        public static void RunBindings()
        {
            QudJPLoader.RunQudTest("qudtest:bindings");
        }

        [WishCommand("qudtest:bindings-all", null)]
        public static void RunBindingsAll()
        {
            QudJPLoader.RunQudTest("qudtest:bindings-all");
        }
    }
}
