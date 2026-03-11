using System;
using System.IO;
using System.Reflection;
using XRL;

namespace QudJP
{
    [HasModSensitiveStaticCache]
    public static class QudJPLoader
    {
        [ModSensitiveCacheInit]
        public static void Bootstrap()
        {
            try
            {
                UnityEngine.Debug.Log("[QudJP] Bootstrap: resolving QudJP.dll path...");

                string modPath = null;
                foreach (var mod in ModManager.Mods)
                {
                    if (mod.ID == "QudJP")
                    {
                        modPath = mod.Path;
                        break;
                    }
                }

                if (modPath == null)
                {
                    UnityEngine.Debug.LogError("[QudJP] Bootstrap: mod 'QudJP' not found in ModManager.Mods");
                    throw new InvalidOperationException("[QudJP] Bootstrap: mod 'QudJP' not found in ModManager.Mods");
                }

                string dllPath = System.IO.Path.Combine(modPath, "Assemblies", "QudJP.dll");

                if (!File.Exists(dllPath))
                {
                    UnityEngine.Debug.LogError("[QudJP] Bootstrap: QudJP.dll not found at " + dllPath);
                    throw new FileNotFoundException("[QudJP] Bootstrap: QudJP.dll not found at " + dllPath, dllPath);
                }

                UnityEngine.Debug.Log("[QudJP] Bootstrap: loading assembly from " + dllPath);
                Assembly assembly = Assembly.LoadFrom(dllPath);

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

                initMethod.Invoke(null, null);

                UnityEngine.Debug.Log("[QudJP] Bootstrap: initialization complete.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[QudJP] Bootstrap failed: " + ex);
                throw;
            }
        }
    }
}
