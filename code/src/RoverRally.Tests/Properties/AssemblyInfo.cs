using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("RoverRally.Tests")]
[assembly: AssemblyCompany("Kadima Proving Ground")]
[assembly: AssemblyProduct("RoverRally Ground Station")]
[assembly: ComVisible(false)]
[assembly: Guid("5c1b8e77-9f04-42a6-b3d1-8a2f6e44c019")]
[assembly: AssemblyVersion("3.6.0.0")]
[assembly: AssemblyFileVersion("3.6.0.0")]
// The SDK normally emits this attribute itself for a "-windows" TargetFramework,
// but GenerateAssemblyInfo=false (kept for the metadata above) turns that off too.
// Without it, every Core member RoverRally.Core marks windows-only (StationSettings,
// DriveController, SessionCacheFile, ...) reads as platform-unsafe (CA1416) here too.
[assembly: SupportedOSPlatform("windows")]
