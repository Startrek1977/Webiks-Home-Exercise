using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;

[assembly: AssemblyTitle("RoverRally Station")]
[assembly: AssemblyDescription("Ground control station for the proving ground rover fleet")]
[assembly: AssemblyCompany("Kadima Proving Ground")]
[assembly: AssemblyProduct("RoverRally Ground Station")]
[assembly: ComVisible(false)]
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: AssemblyVersion("3.6.0.0")]
[assembly: AssemblyFileVersion("3.6.0.0")]
// The SDK normally emits this attribute itself for a "-windows" TargetFramework,
// but GenerateAssemblyInfo=false (kept for the metadata above) turns that off too.
// Without it, every Core member RoverRally.Core marks windows-only (StationSettings,
// DriveController, SessionCacheFile, ...) reads as platform-unsafe (CA1416) here too.
[assembly: SupportedOSPlatform("windows")]
