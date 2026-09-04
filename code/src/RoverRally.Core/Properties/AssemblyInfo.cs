using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyTitle("RoverRally.Core")]
[assembly: AssemblyDescription("Shared models, telemetry and station services")]
[assembly: AssemblyCompany("Kadima Proving Ground")]
[assembly: AssemblyProduct("RoverRally Ground Station")]
[assembly: ComVisible(false)]
[assembly: Guid("8f3a1c22-5b47-4e19-9d6a-11c4e7b0a331")]
[assembly: AssemblyVersion("3.6.0.0")]
[assembly: AssemblyFileVersion("3.6.0.0")]
// The SDK normally emits this attribute itself for a "-windows" TargetFramework,
// but GenerateAssemblyInfo=false (kept for the metadata above) turns that off too,
// so StationSettings' Registry calls read as platform-unsafe (CA1416) without it.
[assembly: SupportedOSPlatform("windows")]
