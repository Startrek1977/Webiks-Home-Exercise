using System;
using System.IO;
using System.Reflection;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Monitoring
{
    /// <summary>
    /// Runs the post-run analyzers that the vehicle dynamics team drop into
    /// the Analyzers folder. They are loaded into their own application domain
    /// so that a faulty analyzer cannot bring the station down, and so the
    /// assemblies can be replaced between runs without a restart.
    /// </summary>
    public class AnalyzerHost : IDisposable
    {
        private AppDomain _domain;

        public string AnalyzerDirectory { get; set; }

        public AnalyzerHost()
        {
            AnalyzerDirectory = "Analyzers";
        }

        public void Load()
        {
            AppDomainSetup setup = new AppDomainSetup();
            setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
            setup.PrivateBinPath = AnalyzerDirectory;
            setup.ShadowCopyFiles = "true";

            _domain = AppDomain.CreateDomain("RoverRally.Analyzers", null, setup);

            Log.Info("Analyzer domain created.");
        }

        public string[] Discover()
        {
            if (!Directory.Exists(AnalyzerDirectory))
            {
                return new string[0];
            }

            return Directory.GetFiles(AnalyzerDirectory, "*.Analyzer.dll");
        }

        public object Run(string assemblyPath, string typeName, RunSnapshot snapshot)
        {
            if (_domain == null) Load();

            AssemblyName name = AssemblyName.GetAssemblyName(assemblyPath);
            object analyzer = _domain.CreateInstanceAndUnwrap(name.FullName, typeName);

            MethodInfo entry = analyzer.GetType().GetMethod("Analyze");
            if (entry == null)
            {
                Log.Warn("Analyzer " + typeName + " has no Analyze method.");
                return null;
            }

            return entry.Invoke(analyzer, new object[] { snapshot });
        }

        public void Dispose()
        {
            if (_domain == null) return;

            try
            {
                AppDomain.Unload(_domain);
            }
            catch (Exception ex)
            {
                Log.Warn("Analyzer domain did not unload cleanly: " + ex.Message);
            }
            finally
            {
                _domain = null;
            }
        }
    }
}
