# Solution Log

> Please fill in this document as you work through the task. Be honest and specific — there are no wrong answers here. We use this to understand your thought process, not to judge your speed.

---

## Time Tracking

| Phase | Start Time | End Time | Duration |
|---|---|---|---|
| Getting the existing application built and running | 2026-08-31 19:45 | 2026-08-31 22:13 | 2h 27m |
| Reading the code / working out what it does | 2026-08-31 22:13 | 2026-08-31 23:04 | 0h 51m |
| Retargeting to .NET 8 | 2026-09-03 18:22 | 2026-09-04 15:56 | 1h 45m |
| Moving to 64-bit | 2026-09-03 23:59 | 2026-09-04 16:32 | 1h 36m |
| The telemetry SDK | 2026-09-01 15:54 | 2026-09-01 20:09 | 4h 15m |
| Bug fixes | 2026-09-02 14:37 | 2026-09-05 16:16 | 9h 09m |
| Logging | 2026-09-04 20:03 | 2026-09-04 20:46 | 0h 43m |
| Tests | 2026-09-03 22:58 | 2026-09-05 14:18 | 1h 11m |
| Cleanup | 2026-09-03 15:27 | 2026-09-04 20:03 | 4h 55m |
| Documentation | 2026-09-04 16:32 | 2026-09-06 12:47 | 1h 18m |
| Extra credit / follow-on work (#28-#32, #74) | 2026-09-05 14:40 | 2026-09-06 13:42 | 3h 44m |
| **Total** | | | **31h 54m** |

*This table is a reconstruction, not a live log - see "Time Tracking" under
Additional Notes at the end of this document for why, and for the method.*

---

## Approach

*How did you start? Did you run it first or read it first? What order did you do things in, and why?*

I built and ran it first, against the simulator, before changing anything. A
station that draws five moving vehicles tells you more in thirty seconds than an
hour of reading does, and it gave me a known-good baseline to compare against
later.

Then I read the code and the three documents, mapped out every task and its
dependencies, and raised one GitHub issue per task. I work them one at a time.
The rule I set myself is that every issue leaves the solution building, the
station running against the simulator, and the tests passing - no "this will work
again after the next one" states, because a half-migrated repo is not a usable
stopping point.

The ordering falls out of the dependencies rather than preference. The telemetry
SDK came before the retarget because a 32-bit-only vendor binary blocks the move
to 64-bit outright. The emergency-stop fix comes after the codec, because it
needs a codec we control.

*This log is filled in as issues land. Sections below that are still empty are
work not yet started.*

---

## The Migration

*What did the retarget actually involve? What broke, and what did you have to change to get it building?*

### Project by project

| Project | What you changed |
|---|---|
| `RoverRally.Core` | Converted to SDK-style (`Microsoft.NET.Sdk`), still net48/x86 (#12). `packages.config` → `PackageReference` for Newtonsoft.Json 6.0.8; dropped the explicit `System`/`System.Core` references, which the SDK supplies implicitly for net48 — everything else (`System.Configuration`, `System.Runtime.Remoting`, `System.Xml`, `System.Xml.Linq`) stayed explicit, since only those two are implicit outside `netcoreapp`/`net5+` — `System.Runtime.Remoting` was later dropped outright by #8, once `StationMonitorService`, its only caller, was deleted; set `GenerateAssemblyInfo=false` rather than delete `AssemblyInfo.cs`, which still carries the real title/company/version metadata. No `Compile` items needed listing — the implicit glob reproduces the existing 24 files exactly. Retargeted to `net8.0-windows` in #15: `System.Xml`/`System.Xml.Linq` references dropped outright (implicit under net8), `System.Configuration` became a `System.Configuration.ConfigurationManager` 8.0.1 `PackageReference`, and it finally picked up the explicit `OutputPath` App and Tests already had. |
| `RoverRally.App` | Same treatment, still net48/x86 (#13). `UseWPF=true` replaced the four explicit `PresentationCore`/`PresentationFramework`/`WindowsBase`/`System.Xaml` references and took over globbing the XAML — `App.xaml` as `ApplicationDefinition`, the rest as `Page` — so the old `ApplicationDefinition`/`Page`/`Compile` item list came out entirely rather than being converted line by line. Kept `AssemblyName=RoverRally.Station` explicit, since SDK-style otherwise derives it from the project file name (`RoverRally.App`) and every doc in the repo names the exe by the old name. `Data\rovers.json` and `Data\session-cache.bin` moved from `<None Include>` to `<None Update>`, since the SDK's own default glob already picks up any non-code file as `None` — `Include`-ing them again is a duplicate-item error. Retargeted to `net8.0-windows` in #15: `System.Configuration` reference became the same `ConfigurationManager` package as Core, and the dead `App.config` `<startup>` block naming `.NETFramework,Version=v4.8` came out. |
| `RoverRally.Tests` | Converted to SDK-style (`Microsoft.NET.Sdk`), still net48/x86 (#14) - the third and last project in the solution to move off the old format. `packages.config` → `PackageReference`, upgrading `MSTest.TestFramework`/`MSTest.TestAdapter` from 2.2.10 to 4.4.0 and adding `Microsoft.NET.Test.Sdk` 18.9.0, which `dotnet test` needs and `packages.config` restore never provided - this is also why `dotnet test` could not run the suite at all before this issue, not just why it needed extra flags. Dropped the explicit `<Compile Include>` list (the implicit glob reproduces the existing ten files exactly) and the `ProjectTypeGuids` test-project marker; kept `Properties/AssemblyInfo.cs` with `GenerateAssemblyInfo=false` rather than delete it, matching Core and App instead of the issue's literal wording. Retargeted to `net8.0-windows` in #15 - no package or reference changes needed here, MSTest 4.4.0/Test.Sdk 18.9.0 already ran on net8. |

Splitting the format conversion out from the retarget (#12 before #15) paid for
itself immediately: restoring via `PackageReference` for the first time pulled
in NuGet's audit check, which `packages.config` restore never runs, and it
flagged the same Newtonsoft.Json CVE the brief already defers to #18 as a
brand-new `NU1903` warning — a warning that had nothing to do with the
framework move and everything to do with the file format change. I suppressed
just that one advisory rather than let it slip in as unexplained noise, since
the CVE itself is still deliberately unfixed.

Rebuilding App surfaced a second file-format surprise that Core's own
conversion had already run into without it being written down here: setting
`PlatformTarget=x86` on an SDK-style project changes the *default*
`OutputPath` to `bin\x86\Debug\`, not `bin\Debug\` —
`AppendTargetFrameworkToOutputPath=false` only strips the `net48` folder, not
the platform one. This solution has no AnyCPU configuration, so every
SDK-style project here hits it. `RoverRally.Station.exe`'s path is the one
thing #13's acceptance criteria pin down explicitly, so I caught it by
checking the actual build output rather than trusting "Build succeeded", and
pinned `OutputPath` per configuration in `RoverRally.App.csproj` to keep it at
`bin\Debug\`/`bin\Release\`. `RoverRally.Core.csproj` has the same gap — its
output genuinely sits at `bin\x86\Debug\` today, and its own PR history (#45)
even shows a follow-up commit titled "Preserve core x86 output defaults" that
turned out to fix a different problem (`PlatformTarget` missing from restore
evaluation), not this one. Nothing depends on Core's literal output path yet,
so it's silent rather than broken, but it's real drift from the old-style
behaviour and worth folding into whichever issue next touches that file.

Codex's review on the PR caught that my first fix was still fragile: I had
conditioned `OutputPath` on `'$(Configuration)|$(Platform)' == 'Debug|x86'`,
which matches when the project builds through the `.sln` (which supplies
`Platform=x86`) but silently stops matching — falling back to the SDK's
default `bin\x86\$(Configuration)\` — for anyone who builds
`RoverRally.App.csproj` directly without also passing `-p:Platform=x86` by
hand. I reproduced the standalone-build scenario four ways (bare `msbuild`,
`-p:Platform=AnyCPU`, `-p:Platform=x86`, and `dotnet build`) before touching
anything: three of the four already landed at `bin\Debug\` correctly, and
adding an old-style-style `<Platform Condition="'$(Platform)'==''">x86</Platform>`
default — the obvious first fix — measurably did nothing, because the SDK's
own implicit top import sets `$(Platform)` to `AnyCPU` before this file's own
`PropertyGroup` ever runs, unlike old-style csproj where the user's
`PropertyGroup` was the first thing evaluated. The real fix was smaller than
the review implied a fix should be: `OutputPath`, `DebugType`, and `Optimize`
never actually needed to depend on `$(Platform)` at all, since `PlatformTarget`
is already unconditioned — so I dropped `$(Platform)` from all three
conditions and keyed them on `$(Configuration)` alone. Verified `OutputPath`
evaluates to the same `bin\Debug\`/`bin\Release\` regardless of whether
`$(Platform)` is unset, `AnyCPU`, or `x86`.

The issue's own baseline - "4 tests - 3 passed, 1 skipped" - was stale by the
time I got to it, predating `BatteryGaugeTests`, `DriveControllerTests`,
`FrameCodecTests`, `GeofenceMonitorTests`, `RoverTests` and
`TelemetryReceivedEventArgsTests`, all added by issues that landed in between.
I did not take the number in the issue on trust: I stashed the conversion,
rebuilt the original `packages.config` project from a clean `bin`/`obj`, and
ran the documented `vstest.console.exe` command against it to get the real
baseline first - 77 tests, 76 passed, 1 skipped. Only then did I pop the stash
and confirm `dotnet test` reproduced that exactly, with nothing silently
dropped.

The one piece of this that could not be purely mechanical: `MSTest.TestFramework`
4.x removed `ExpectedExceptionAttribute` outright, and there is no version past
2.2.10 that keeps it, so "upgrade MSTest" and "no source changes" were not both
achievable. The six tests in `Crc8Tests` asserting an exception type moved to
`Assert.ThrowsExactly<TException>(() => ...)`, which has the same
exact-type-only semantics `ExpectedException` had by default - same coverage,
same pass/fail behaviour, current API.

Copilot's review on the PR caught that I had left `RoverRally.Tests.csproj`
with the same gap App's own PR had already found and fixed: the Debug/Release
`PropertyGroup`s were conditioned on `'$(Configuration)|$(Platform)' ==
'Debug|x86'`, and `OutputPath` was left unpinned, so `dotnet test` run
directly against the `.csproj` - which leaves `$(Platform)` at its `AnyCPU`
default - silently landed at `bin\Debug\` with `DebugType=portable`, while
building through the `.sln` landed at `bin\x86\Debug\` with
`DebugType=full`. I reproduced both before touching anything, with
`-getProperty` rather than eyeballing the build log, then applied the exact
fix App already carries: pin `OutputPath` to `bin\$(Configuration)\` and key
the conditions on `$(Configuration)` alone. Verified both entry points now
evaluate identically for Debug and Release. I should have copied that pattern
onto Tests the first time, since I had already written up why App needed it a
few paragraphs above.

### Retargeting to .NET 8 (#15)

*What did the retarget itself surface, once every upstream blocker (#6-#14)
was already gone?*

**What I found.** With the vendor DLL, `BinaryFormatter`, `AppDomain`, and
`System.Runtime.Remoting` already gone, and all three projects already
SDK-style, `net48` → `net8.0-windows` compiled clean on the first try for
every source file - the codebase really was as clean going in as the earlier
issues' spikes said it would be. Two real carry-overs still needed handling,
neither a source change: `ConfigurationManager.AppSettings` (`StationSettings.cs`
in Core; `App.xaml.cs` and `MainWindow.xaml.cs` in App) isn't part of the net8
base class library, so both projects needed a `System.Configuration.ConfigurationManager`
package reference where they'd previously had a bare `<Reference
Include="System.Configuration">`; and the same bare-reference pattern for
`System.Xml`/`System.Xml.Linq` in Core doesn't resolve at all under SDK-style
net8 (no GAC, no by-name assembly resolution) - both came out, since those
namespaces are implicit under net8 anyway. `Microsoft.Win32.Registry`, also
used in `StationSettings.cs`, needed nothing: Windows-only reference
assemblies like it ship as part of the `net8.0-windows` target by default (the
SDK actually normalized the TFM to `net8.0-windows7.0` once I set it, picking
a default Windows API contract version I never specified).

What actually surfaced only once the build ran: 6 new `CA1416` platform-
compatibility warnings on `StationSettings.cs`'s Registry calls in Core. The
SDK normally emits `[assembly: SupportedOSPlatform("windows")]` for you on a
`-windows` TFM, but all three projects set `GenerateAssemblyInfo=false` back
in #12/#13/#14 to keep their real, hand-authored `AssemblyInfo.cs` - and that
flag turns off every attribute the SDK would otherwise generate, not just the
title/version ones I meant to keep. Adding the attribute by hand to Core's
`AssemblyInfo.cs` cleared those 6, but the next rebuild came back with 747 new
`CA1416` warnings in `RoverRally.Tests` and a smaller batch in `RoverRally.App`
- marking Core's own assembly Windows-only makes every public member of it
read as Windows-only too, so any *consumer* assembly not itself marked
Windows-only lights up on every call into `DriveController`, `SessionCacheFile`,
`FrameCodec`, anything. Same root cause, same fix, applied to App's and Tests'
own `AssemblyInfo.cs` as well.

**What I decided.** Add `[assembly: SupportedOSPlatform("windows")]` by hand
to all three `AssemblyInfo.cs` files rather than suppress `CA1416` in the
`.csproj`s. The warning is correct - this whole solution is Windows-only, that
is the entire reason for the `-windows` suffix - the attribute was just never
generated because `GenerateAssemblyInfo=false` silently took it with it.
Suppressing the warning code would have hidden a real signal if a future
change made one of these projects less platform-specific; declaring the
platform explicitly says what's actually true instead. Left `LangVersion`
unpinned in all three - it now floats to the SDK's default (C# 12) - since
this issue is a mechanical retarget, not license to start writing newer
syntax. Also cleaned up two things directly tied to the framework this issue
retires, both agreed with the exercise owner before touching them: pinned an
explicit `OutputPath` on `RoverRally.Core.csproj` (closing the `bin\x86\Debug\`
vs `bin\Debug\` drift #13 had already found and fixed on App and Tests but
left noted, not fixed, on Core), and removed `App.config`'s dead
`<startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />
</startup>` block, which named the exact framework version this issue
retargets away from.

The restore also surfaced an expected `NU1701` compatibility warning on
`Newtonsoft.Json` 6.0.8 in all three projects - it has no netstandard/net5+
asset, so NuGet falls back to its `.NETFramework` build via compatibility
shims. That's the same CVE this repo already knows about and has already
deferred to #18; I left it exactly as deferred rather than pulled forward,
since upgrading it was never this issue's job and doing it here would have
mixed an unrelated package bump into a TFM-only change.

**How I verified it.** `msbuild -t:Rebuild -p:Configuration=Debug
-p:Platform=x86` after deleting all three projects' `bin`/`obj` (stale
outputs can mask reference changes, per the lesson already written down in
this repo) - 0 errors, and after the `SupportedOSPlatform` fix, 0 `CA1416`
warnings anywhere, just the 4 expected `NU1701` warnings on Newtonsoft.Json.
`dotnet test` - 94 tests, 93 passed, 1 skipped
(`TrackProjectionTests.PlacesTheStartLine`, the one pre-existing skip), 0
failed; the total has grown since #9's 87 as later issues added coverage, but
the skip and failure counts - what actually matters for "did this issue break
anything" - are unchanged.

Then the real station against the real simulator, same discipline as every
previous migration issue: `RoverRally.Station.exe`, launched from a shell so
its log was visible, against `dotnet run --project code/simulator/RoverRally.Simulator`.
It logged `Loaded 5 rover(s) from the roster.`, then `Migrated 8 legacy
session cache record(s)...` and `Loaded 8 run(s) from the session cache.` -
the migration message reappears here because deleting `bin`/`obj` redeploys a
fresh copy of the source-controlled, still-legacy `Data/session-cache.bin`
fixture on every clean rebuild; #11's migrator only ever touches the
build-output copy, never the checked-in one, so this is expected on a clean
rebuild and not a regression - then `Telemetry listener started on UDP
14550.`. Both processes ran for a full minute with the simulator streaming
all 5 vehicles at 5 Hz the whole time and nothing but those five startup
lines in the station's log - no decode failures, no exceptions, no crash.

Copilot's review on the PR caught one thing I'd left alone on purpose and
turned out to be wrong about: `RoverRally.Core.csproj` still conditioned its
`DebugType`/`Optimize` `PropertyGroup`s on `'$(Configuration)|$(Platform)' ==
'Debug|x86'`, the exact pattern #13 and #14 had already found and fixed on
App and Tests - it stops matching whenever the project builds outside the
`.sln` without also passing `-p:Platform=x86` by hand, since the SDK
defaults `$(Platform)` to `AnyCPU` first. I'd treated it as out of scope for
a TFM-only retarget; the review was right that it wasn't. I reproduced it
with `-getProperty` rather than trusting the report: standalone, Core's
`DebugType` evaluated to `portable`, not `full`, exactly as the finding
said. Applied the same fix App and Tests already carry - drop `$(Platform)`
from the conditions, key them on `$(Configuration)` alone - and reverified
both entry points now agree (`DebugType=full`, `Optimize=false` for Debug,
whether or not `$(Platform)` is set). Same review also caught a stale
comment on the `NU1903` suppression still saying "this format conversion",
left over from #12's wording, which I updated to name this issue instead.

**Addendum (#81).** Debug's `DebugType` moved again after this, `full` →
`portable`, in all three projects - found as an uncommitted, undocumented
working-tree diff with no PR or review behind it, so #81 is the paper trail
that was missing. Not a regression of the bug above: the conditioning fix
this section describes is untouched, all three still key their Debug/Release
`PropertyGroup`s on `$(Configuration)` alone, so `portable` evaluates the
same way from the `.sln` and standalone that `full` did.

My first pass at this addendum claimed `portable` would unify the Debug PDB
*format* with Release's `pdbonly`. I did not verify that before writing it
down, and it was wrong: I inspected the actual built `.pdb` files' header
bytes rather than trusting the docs from memory, and on Windows `pdbonly`
produces a classic Windows PDB (`Microsoft C/C++ MSF 7.00` signature), not
the portable format (`BSJB` signature) `portable`/`embedded` produce. So
keeping `portable` on Debug alone while leaving `pdbonly` on Release would
have left the two configurations on genuinely different PDB formats, the
opposite of what I'd claimed. Moved Release to `portable` too so the claim
is actually true: both configurations now emit the same cross-platform
portable-format PDB, verified the same way (header bytes, not memory).

I also wrote, in that same first pass, that `full` and `pdbonly` "only ever
differed in Edit-and-Continue support" and that this was the one real cost
of moving to `portable`. I did not verify that claim either, and this time
I checked before letting it stand a second time: Microsoft's own C#
compiler-options reference says plainly that `pdbonly` and `full` produce
the identical Windows PDB on Windows, and that "for all compiler versions
starting with C# 6.0, there is no difference between pdbonly and full" -
this project is on C# 12. Whatever debugger-attach distinction the two
values once had is documented as obsolete, and I found nothing in current
Microsoft documentation suggesting `portable` costs Visual Studio anything
`full`/`pdbonly` still provides. So there is no known tradeoff here at
all: `portable` is simply the SDK's own current default for `DebugType` on
both configurations, and this change adopts that default explicitly on
Release rather than leaving it pinned to a legacy value that, on this
platform, was never anything other than an alias for `full`.

### Moving to 64-bit

*What did 64-bit break that .NET 8 on its own did not? How did you find it?*

**What I found.** `Core/Session/SessionCacheRecord.cs` is a
`[StructLayout(LayoutKind.Sequential)]` struct read and written with
`Marshal.SizeOf`/`Marshal.PtrToStructure`/`Marshal.StructureToPtr`, and it
carried `public IntPtr SessionHandle` - a "session handle returned by the SDK"
that nothing downstream ever read. `IntPtr` is 4 bytes on x86 and 8 bytes on
x64, and its alignment shifts every field that follows it, so the record's
marshalled size is not a fixed number - it's whatever the CLR decides for the
process that happens to be running. The shipped
`RoverRally.App/Data/session-cache.bin` is exactly 256 bytes: I hex-dumped it
and confirmed 8 records of 32 bytes each, which is the x86 answer. The same
struct measures 40 bytes on x64, so `raw.Length / RecordSize` in
`SessionCacheFile.Read` would silently yield 6 records instead of 8, and every
field after the handle would decode from the wrong offset - a run's distance
would read back as its peak speed, and so on. Nothing about this shows up
until the platform actually flips to x64; on x86 - even on .NET 8 - the bug is
invisible. That's what makes it the one migration step 64-bit forces rather
than one .NET 8 forces on its own.

I worked out the byte layout by hand before touching anything: `RoverId` at
offset 0, the handle at offset 4 (4 bytes on x86), `StartedUtcTicks` at 8,
`EndedUtcTicks` at 16, `DistanceCm` at 24, `PeakSpeedCmS` at 28, 32 bytes
total. Removing the handle and letting the compiler naturally re-pad
`StartedUtcTicks` back to its required 8-byte alignment leaves a 4-byte gap in
exactly the same place the handle used to sit - every other field keeps its
offset, and the total stays 32 bytes on both architectures. Compacting the
record to 28 bytes instead would have shifted every field after `RoverId` and
silently misparsed the 8 runs already on disk, which breaks the fleet grid
until a real format migration lands - that's #11's job, not this one, and
doing it here would have violated the project's always-green rule for however
long #11 took to follow.

**What I decided.** Rather than lean on that padding behaviour, I dropped
`Marshal`/`StructLayout` from both the struct and `SessionCacheFile` entirely
and hand-pack the record at fixed byte offsets with `BitConverter`, keeping
offset 4 as an explicitly reserved, always-zero gap. `RecordSize` is now a
`public const int` of `32`, not something measured from the struct's shape at
runtime. This satisfies the issue's requirement that the format not depend on
`Marshal.SizeOf`, while still producing byte-identical output to the old x86
layout, so the shipped cache file needed no migration for this issue.

**How I verified it.** A golden-byte test (`SessionCacheFileTests`) copies the
real 256 bytes of the shipped `session-cache.bin` into the test - not
reconstructed from the struct, so a transcription bug in the packing logic
can't hide from it the way a self-generated fixture could - and asserts all 8
records decode to the exact `RoverId`, `StartedUtcTicks`, `EndedUtcTicks`,
`DistanceCm` and `PeakSpeedCmS` values I computed independently with
PowerShell's `BitConverter`, not by re-deriving the C# packing code. A
separate round-trip test writes and reads back a record with distinct values
for every field. Then, because a passing unit test doesn't prove the real
application wires things together correctly, I ran the actual station against
the actual shipped cache file: `RoverRally.Station.exe`, launched from a
console so its `Log.Info` calls were visible, logged
`Loaded 8 run(s) from the session cache.` - the same count as before the
change, through the real `MainWindow.LoadSessionHistory` → `SessionCacheFile.Read`
path, not a test double.

### Migrating the legacy session cache (#11)

*What did the shipped file still need, once #10/#51 could already read it?*

**What I found.** #10/#51 kept the record layout byte-identical to the old
x86 one on purpose, so the shipped `session-cache.bin` needed no migration to
keep reading correctly - offset 4 stayed a reserved gap that `Read` ignores
and `Append` always writes as zero. That is not the same as the file itself
being in the new format. I hex-dumped the real 256 bytes again for this issue
and every record's reserved slot still holds a real, non-zero legacy
`SessionHandle` value (`0x00001000`, `0x00002000`, ...) left over from the
x86 process that wrote it. `SessionCacheFile.Read` tolerates that gap
unconditionally, so the station has never actually needed those bytes to be
zero - but the issue asks for a converter that produces a genuinely clean
file, not one that merely gets away with ignoring the old bytes forever.

**What I decided.** `SessionCacheMigrator.MigrateIfNeeded`, called once from
`MainWindow.LoadSessionHistory` ahead of `SessionCacheFile.Read`, so a site's
history is upgraded the first time they run the new station with no manual
step. It reads the file with its own explicit offset table - independent of
`SessionCacheFile`'s private constants, matching the issue's stated legacy
layout, and verified against the same real bytes - and treats a record's
reserved slot being non-zero as the one signal that distinguishes an
unconverted file from a converted one, since that is the only byte-level
difference between the two formats. If every record's slot is already zero
it does nothing and says so in the log; otherwise it backs the original up
to a fixed `<path>.legacy` sibling first, then zeroes the slot in every
record and writes the result back to the original path. It refuses outright,
without touching anything, if the file's length isn't a whole multiple of
the 32-byte record size (truncated or corrupt input), or if a `.legacy`
backup already exists - a stray leftover backup is treated as evidence a
previous run already migrated the file, not something safe to overwrite.

**How I verified it.** `SessionCacheMigratorTests` covers all of it against
the real 256-byte shipped fixture: a successful migration (backup created and
byte-identical to the original, every reserved slot zeroed, and
`SessionCacheFile.Read` on the result still returning the same 8 runs'
`RoverId`/`StartedUtcTicks`/`EndedUtcTicks`/`DistanceCm`/`PeakSpeedCmS` values
`SessionCacheFileTests` already pins down), the already-migrated no-op
(nothing written, no backup created), a missing file, a truncated file
(throws, original untouched), and a pre-existing backup (throws, neither
file touched). I also added a case to `SessionCacheFileTests` confirming
`SessionCacheRecord.FromTicks` still clamps ticks below `DateTime.MinValue`
instead of throwing, since the issue asked me to confirm that guard rather
than assume it.

Then, same discipline as #10/#51: I ran the real station against the real
simulator rather than trusting the test suite alone. First launch logged
`Migrated 8 legacy session cache record(s) at ...; original preserved at
...session-cache.bin.legacy.`, followed by the usual `Loaded 8 run(s) from
the session cache.`; a second launch logged `... has no legacy
session-handle bytes left; nothing to migrate.` instead, and still loaded
all 8 runs. Both runs only touched the build-output copy under
`bin\Debug\Data\` - `git status` confirmed the source-controlled
`RoverRally.App/Data/session-cache.bin` fixture was untouched, since the
station always resolves the cache path from
`AppDomain.CurrentDomain.BaseDirectory`.

Two automated reviews on the pull request each caught a real gap. Codex
pointed out that `LoadSessionHistory` shared one `try` block across
`MigrateIfNeeded` and `Read`, so a migration failure - a legacy file with a
torn trailing record, say - would skip the read entirely and show no history
at all, where `Read` alone would have discarded the torn bytes and shown
every complete run. I reproduced it before fixing it: crafted a 37-byte file
(one full record plus a torn tail) in the build-output `Data\` folder and ran
the real station against it. It logged the migration failure and then still
logged `Loaded 1 run(s) from the session cache.` - the fix was splitting the
two calls into their own `try`/`catch` blocks so a migration failure no
longer prevents the fallback read. Copilot separately caught that the write
itself - `File.Copy` for the backup, then `File.WriteAllBytes` straight onto
the original path - was two non-atomic steps; an interruption between them
(disk full, power loss, AV lock) could leave a half-written
`session-cache.bin` behind, and since the backup would already exist by
then, a later run would refuse to retry rather than clean it up. Both steps
are now one `File.Replace` call - write the converted bytes to a temp file,
then swap it into place with the backup created in the same atomic
operation - so an interruption leaves either the untouched original or the
fully-converted file, never something in between.

### Flipping the platform to x64 (#16)

*Everything risky was already done by #10, #11 and #15 - so what was actually
left, and how did I prove the result is genuinely 64-bit rather than just
csproj text that says so?*

**What I found.** A full-repo search for `x86`, `PlatformTarget`,
`Prefer32Bit`, `Marshal`, `StructLayout`, `IntPtr` and `DllImport` turned up
exactly four files still carrying an x86 *build setting*: `RoverRally.sln`
(`Debug|x86` and `Release|x86` are the only two configurations it defines -
there is no AnyCPU row to fall back to) and the three solution `.csproj`
files, each with an unconditioned `<PlatformTarget>x86</PlatformTarget>`.
That count is about configuration, not every mention of the string - prose
references to `x86` survive elsewhere (`docs/architecture.md`, and this log)
describing the format's history, and those are deliberately untouched.
`RoverRally.Simulator.csproj` is outside the solution, targets plain
`net8.0`, and never had a `PlatformTarget` to begin with. Nothing else
qualified: `Marshal`, `StructLayout`, `IntPtr` and `DllImport` are all gone
from live code (only historical doc comments in `Session/SessionCacheMigrator.cs`
and `RoverRally.Tests/SessionCacheFileTests.cs` still mention the old
`IntPtr` handle #10 removed), and the vendor's 32-bit-only telemetry DLL - the
one thing that could never have run as x64 no matter how the csproj was configured - was
already deleted. I also checked whether the registry profile key
(`StationSettings`, `HKCU\Software\RoverLink\Station\Profile_<hash>`) could
break for an operator switching from the x86 station to the x64 one:
`Registry.CurrentUser` is used with no explicit `RegistryView`, and
`HKEY_CURRENT_USER\Software` is never subject to WOW6432Node redirection -
only `HKLM\SOFTWARE` is - so a 32-bit and 64-bit process read the exact same
key. An operator's saved speed unit and last-selected rover survive the flip
untouched. So the actual change was narrow: two `.sln` configuration rows and
three `PlatformTarget` values.

**What I decided.** Explicit `x64` in all three csproj files, not
AnyCPU+`Prefer32Bit=false` - it is the smaller diff, and it keeps the
`-p:Platform=<value>` command shape CLAUDE.md already documents instead of
needing "Any CPU" quoting. I swapped every `Debug|x86`/`Release|x86` row in
the `.sln`'s `SolutionConfigurationPlatforms` and all three GUIDs'
`ProjectConfigurationPlatforms` entries for `Debug|x64`/`Release|x64`, and
changed `PlatformTarget` in `RoverRally.Core.csproj`, `RoverRally.App.csproj`
and `RoverRally.Tests.csproj` from `x86` to `x64`, updating each project's own
rationale comment about `Debug|x86`/`-p:Platform=x86` to match. I also added
`RoverRally.Tests/PlatformTests.cs` with one test,
`RunsAsA64BitProcess`, asserting `Environment.Is64BitProcess` - the issue
explicitly warns not to assume 64-bitness from the build configuration, and a
one-time manual check does not stop a future accidental revert to x86; a
permanent test does.

**How I verified it.** Deleted `code/src/*/bin` and `code/src/*/obj` first -
CLAUDE.md's own documented trap is that a stale x86 output directory can mask
whether the platform actually changed - then ran
`MSBuild.exe code/RoverRally.sln -t:Rebuild -p:Configuration=Debug
-p:Platform=x64`, which succeeded outright. Rather than trust that, I read the
built `RoverRally.Station.exe`'s PE header directly: the `IMAGE_FILE_HEADER`
`Machine` field at the offset named by `e_lfanew` (byte 0x3C) came back
`0x8664` - `IMAGE_FILE_MACHINE_AMD64` - not `0x14C` (`IMAGE_FILE_MACHINE_I386`).
`dotnet test` on the rebuilt `RoverRally.Tests.csproj` passed 94 of 95 (the
one skip is the pre-existing `PlacesTheStartLine`, same baseline as before),
including the new `RunsAsA64BitProcess` and, more importantly,
`SessionCacheFileTests`/`SessionCacheMigratorTests` re-validating the golden
32-byte record layout under the new architecture.

Then the end-to-end check the issue actually asks for: I launched the real
simulator, then the real built `RoverRally.Station.exe` with its console
output captured. It logged `Session cache at ...session-cache.bin has no
legacy session-handle bytes left; nothing to migrate.` followed by `Loaded 8
run(s) from the session cache.` - the same 8 runs #10 and #11 verified,
now read back by a station that is not merely "not crashing" but provably
native x64. To confirm that last part while the process was actually running
- not just the exe file on disk - I called `IsWow64Process` from `kernel32`
against the live PID: it returned `false` while
`Environment.Is64BitOperatingSystem` on the same machine returned `true`,
meaning the station is a genuine 64-bit process, not a 32-bit one running
under WOW64 emulation - the same distinction Task Manager's "Platform" column
shows. Finally I drove the actual UI against the running simulator: telemetry
populated the track view for all 5 rovers, ARM/DISARM flipped
`STATION`/`VEHICLE` state correctly, and EMERGENCY STOP produced the real
confirmation dialog ("Emergency stop sent to Falafel. The stop is held until
you press ARM.") and left the station in `STATION: STOP LATCHED - VEHICLE:
STOPPED` until I pressed ARM again - the #20 latch behavior working through
the real UI, not only through `DriveControllerTests`.

---

## The Telemetry SDK

*What did you find, what did you decide, and how did you verify that the result is correct?*

**What I found.** `lib/RoverLink.Telemetry.dll`, version 1.4.2, from RoverLink
Systems, who no longer exist. It is a managed assembly, but the PE header marks
it 32-bit only, so a 64-bit process cannot load it at all. That makes it a hard
blocker for the platform move rather than a tidiness item - no amount of
retargeting gets past it.

The surface actually in use is tiny, so reimplementation was viable. But I did
not take the shipped documentation's word for what that surface is. I dumped the
assembly's `#Strings` metadata heap and enumerated the real public members:

- `Crc8.Compute(byte[], int, int)`
- `TelemetryFrame` - **12** scalar properties and **4** derived booleans
  (`IsArmed`, `IsEmergencyStopped`, `IsCharging`, `HasGpsFix`)
- `FrameCodec.TryDecode` / `EncodeCommand`, plus three public constants

The vendor's own `RoverLink.Telemetry.xml` documents only 9 of the properties.
`RoverId`, `Sequence`, `TimestampMs` and all four booleans are undocumented - and
`MainWindow.ApplyFrame` uses four of them. Coding to the documentation would have
produced a codec that compiled inside `RoverRally.Core` and then failed to build
the application one project downstream.

**What I decided.** A clean-room reimplementation in `RoverRally.Core.Telemetry`,
and delete the binary. Two independent specifications exist in the repository:
`docs/rover-link-protocol.md` and the simulator's `FrameWriter.cs`. I treated
`FrameWriter.cs` as authoritative, because the simulator is what the station is
actually tested against, and used the protocol notes as corroboration. The public
signatures are unchanged from the vendor's, so no calling code moved.

I put the types in our own namespace rather than keeping `RoverLink.Telemetry`.
Keeping it would have meant a literally zero-line source diff, but our assembly
would then be squatting a dead vendor's namespace and the code would still read
as though the SDK were present. Moving cost four `using` directives.

**How I verified it.** Four independent ways, because a codec that is subtly
wrong still passes a lazy test:

1. *Against a mirror of the simulator's writer.* `RoverRally.Tests` targets net48
   while the simulator targets net8.0 and is not in the solution, so a project
   reference is impossible; I transcribed `FrameWriter` into the test project.
   That covers all 256 byte values for the CRC, buffers of every length from 0 to
   64, all 16 status-flag combinations, every rejection path, and clamping at and
   beyond the limits.
2. *Against golden frames that do not come from that mirror.* A mirror shares any
   transcription error with the implementation, so by construction it cannot
   catch one. The telemetry golden frame is bytes emitted by executing the real
   net8.0 `FrameWriter`. The command golden frame is assembled from the protocol
   notes, checksummed with the simulator's own `Crc8`, and confirmed accepted by
   the simulator's `TryReadCommand` - closing the loop against rover-side code
   rather than against my own arithmetic.
3. *Against the protocol notes' worked example.* The CRC of `01 02 03` is `0x48`,
   which is independent of both implementations.
4. *By breaking it on purpose.* I mutated the clamp limit and the CRC accumulator
   and confirmed 12 tests went red across encode, decode and checksum. A green
   suite proves nothing until you have watched it fail.

Then end to end: the real simulator, and the real `TelemetryClient` and
`CommandSender` over UDP. 215 frames decoded, none malformed, all five vehicles,
sane readouts - and the simulator logged `!! emergency stop received for Falafel`
and braked, so the command direction is accepted by the rover-side reader on the
wire and not merely in a unit test.

The vendor DLL and its XML are deleted, both project references are gone, and I
confirmed that no built assembly still names `RoverLink` anywhere in its
metadata.

This landed on net48 and needed no retargeting, which is why it went first.

---

## Bugs

### The three Dana asked for

| Bug | Root cause | Fix |
|---|---|---|
| Emergency stop | The station never re-asserted the stop. `MainWindow._emergencyStopLatched` was assigned and never read (CS0414), and `DriveTimer_Tick` hard-coded `emergencyStop: false` every 200ms. The vehicle is level-triggered - `RoverSim.Apply` overwrites its held flag from each frame - so the latch survived exactly one tick. Intermittent because `EmergencyStop_Click` transmitted `armed: false` without clearing the local `_armed`, so the next tick sent stale armed plus whatever the throttle slider read | Moved the latch into a `DriveController` in Core, which every tick consults; engaging the stop also disarms locally, and only an explicit re-arm clears it. Also removed the `IsReconnecting` guard that could refuse to send the stop at all |
| Battery readout | `BatteryGauge.ToPercent` subtracted `EmptyMilliVolts` and cast the difference to `ushort` before scaling. Any pack at or below 9.0V - including a failed sensor reporting 0 mV, which is the simulator's own failure sentinel - produced a negative difference that wrapped to a huge positive `ushort`, which the existing high-side clamp then capped at 100%. `IsCritical` just calls `ToPercent`, so the low-battery check inherited the same blind spot and could never fire for a flat pack | Kept the subtraction as signed `int` and clamped the result to `0..100` on both ends instead of only the top. A pack below empty, or a 0 mV failed-sensor reading, now reads 0% instead of wrapping past it - no special-casing the sensor failure separately, since it's just the most extreme case of "below empty" |
| Imperial speed | `SpeedConverter.Format` converted cm/s to km/h, rounded that km/h value to an `int` with `Math.Round`, and then fed it straight back into `ToMilesPerHour` - a method whose own parameter is cm/s, not km/h. The cm/s-to-km/h factor was effectively applied a second time to a number that was already km/h, and the intermediate rounding threw away precision before that second, bogus conversion ran. For 500 cm/s (18.0 km/h, which should read 11.2 mph) it reported 0.4 mph - about a twentieth of the real speed, matching what the visiting engineer clocked | `ToMilesPerHour` is now called directly on the original cm/s input instead of the already-converted km/h value, and the `Math.Round` round-trip through `int` is gone - the exact cm/s figure feeds the conversion, and display rounding happens once, at the very end, the same way the km/h branch already worked. `Format` also only computes the unit it's actually formatting now, instead of always computing km/h up front |

### Anything else you found

*Bugs Dana didn't mention. For each: what's wrong, where, how you found it, and what you did about it.*

1. **The emergency stop could refuse to send at all.** `MainWindow.EmergencyStop_Click`
   returned early when `_telemetry.IsReconnecting`, showing "The link is reconnecting.
   Try again in a moment." I found it while reading the handler for the latch fix. That
   flag belongs to the *inbound* telemetry listener; commands go out through a separate
   `UdpClient` owned by `CommandSender` and are unaffected by it. So an unrelated inbound
   socket rebinding made the big red button do nothing, in the same handler as the bug I
   was already there to fix. The stop is now always sent, and a degraded link is reported
   alongside it rather than instead of it.
2. **The emergency stop could throw instead of stopping anything.** Both drive
   handlers called `_commands.Send(...)` and read `_telemetry.IsReconnecting`
   without checking either for null, while `DriveTimer_Tick` next to them
   already guarded `_commands`. If the link never started, ARM threw a
   `NullReferenceException` and the stop threw after sending. An automated
   review on the pull request pointed at the missing guard; I took the finding
   and widened it, because the interesting half is not the exception. A safety
   control that appears to work and quietly does nothing is worse than one that
   is plainly broken, so the stop now reports that it could not send and tells
   the operator to stop the vehicle by hand.

   The same review round found the drive state indicator refreshing only when a
   frame arrived for the selected rover, so changing the fleet selection left
   the previous rover's state on screen under the new rover's name -
   indefinitely if the new one was not transmitting. I reproduced it before
   fixing it: armed Falafel, stopped the simulator, selected Sandstorm, and the
   line still read `VEHICLE: ARMED` for a vehicle that had never been armed.
   That is the same mis-attribution as the geofence defect below, in a
   different control.
3. **Selecting a rover in the Fleet grid silently cleared its emergency stop
   (#37).** Not something Dana flagged - I found it running the station against
   the simulator after #20 landed. `DriveController`'s latch is global to the
   station, not per rover. Stop Falafel, select Sandstorm, select Falafel again
   with nobody touching ARM or the throttle: the very next drive-timer tick sent
   Falafel `emergencyStop: false`, because that flag is all the latch the
   vehicle itself keeps, and the newly-selected vehicle was not what the
   station's own latch had in mind. Same failure as #20 - the vehicle carries on
   after a stop - reached by a different door, and #20 had only bolted the one
   it was told about.

   The fix, agreed with the exercise owner as an interim guard rather than the
   full per-rover redesign that `#35` still owes: the station will not transmit
   `emergencyStop: false` to a vehicle whose own telemetry reports the stop
   latched, or that has not reported at all within the vehicle's own
   two-second command-loss window - unless the operator explicitly re-arms
   *that* vehicle, throttle centred, same gate as any other re-arm. `#35`
   is not superseded; this closes the hole without answering the question #35
   still owes about commanding vehicles the station is not displaying.

   Running the fix against the real simulator caught something no unit test
   did: a throwaway harness that re-armed Falafel and then drove the very next
   tick immediately - no sleep, deliberately - found the re-arm relatching
   itself. The tick's freshest telemetry was still the pre-rearm frame, 5Hz
   telemetry not having caught up with a re-arm that was itself milliseconds
   old, and the guard trusted it. `DriveController` now tracks when it last
   decided what to transmit and only lets a reported stop justify a *new*
   latch if the frame reporting it is at least as new as that. Two tests pin
   this down directly: `DoesNotRelatchOnAStaleFrameFromBeforeAnExplicitReArm`
   and `StillAdoptsAStopConfirmedByAFrameNewerThanTheReArm`. Worth recording
   because it is the second time in this exercise that a defect only showed up
   against the real simulator - the codec's transcription-mirror problem was
   the first - and both times a passing unit-test suite was the wrong signal to
   stop on.

   A Codex review on the pull request that carried this fix into `#4`'s branch
   (PR #40 - the two land in the same history because `main` had already
   merged this work by the time that branch was rebased forward) found the
   remaining gap directly: a rover with **no** telemetry at all is silent by
   definition, and the freshness guard's silence branch adopted a stop from
   that alone on *every* tick, with no freshness check at all - including the
   tick right after an operator explicitly re-armed it, throttle centred,
   through the same gate as any other re-arm. Arming a rover with a dead
   telemetry link "worked" for exactly one tick and then relatched itself.
   I reproduced it first - `AnExplicitReArmOfANeverReportedRoverPersistsOnTheNextTick`
   goes red against the code as it stood - before fixing it: the controller
   now remembers whether the rover it most recently addressed *was* silent at
   that decision, and only treats continued silence for that same rover as
   already accounted for, not as fresh evidence to re-latch on. A rover that
   was reporting fine and then genuinely goes silent mid-drive is unaffected -
   that is still new information and still latches, which
   `StillLatchesWhenAPreviouslyReportingRoverGoesSilent` pins down so the fix
   cannot swing the other way into never latching a real silence-onset stop.

   **`#35` closes the question this interim guard deliberately left open.**
   `DriveController` itself needed no logic changes at all - every public
   method already took `roverId` on every call, which is what made the
   freshness guard above possible in the first place. What it was missing was
   never being asked to hold more than one rover's state at a time. `MainWindow`
   now goes through a new `DriveControllerRegistry` (`RoverRally.Core.Control`)
   instead of holding one shared `DriveController` field: a thin
   `Dictionary<byte, DriveController>` that hands back the same instance for
   the same rover id every time, creating one on first use. Nothing needs to
   be explicitly "restored" on a selection change - a rover's controller is
   never thrown away to begin with, so switching away and back finds it
   exactly as it was left. `ArmButton`'s label and the drive-state indicator
   were previously updated ad hoc at three separate call sites (`Arm_Click`,
   `EmergencyStop_Click`, the drive timer's own adoption branch) and not at
   all on a bare selection change, which is exactly why the button could
   describe a different rover than the one on screen. Both are now read from
   one `UpdateDriveDisplay()` method (renamed from `UpdateDriveStateText`)
   called from every place selection or state can change, including the
   `SelectedRover` property-changed handler that previously only refreshed
   the text.

   **The non-selected-rover question the interim guard left open is answered
   as "one vehicle under command," not "a fleet under command."** The drive
   timer still addresses only whichever rover is selected, exactly as before;
   it does not loop over the roster transmitting held stops to vehicles that
   aren't on screen. Not transmitting is not the same as not latching, per the
   original `#35` text: a deselected, previously-stopped rover's own
   `DriveController` still holds `IsEmergencyStopLatched = true` in memory the
   entire time it isn't addressed, so reselecting it reasserts the stop on the
   very next tick rather than needing to be told about it again. This was the
   owner's explicit call, not mine to make unilaterally - broadening to a
   fleet-wide transmit loop is a materially larger claim about what this
   station does than the exercise asked for.

   `DriveControllerTests` gained two cases pinning down exactly the isolation
   this issue is about: `StoppingOneRoversControllerDoesNotAffectAnotherRoversController`
   and `ALatchedRoversControllerSurvivesAddressingADifferentRoverAndSwitchingBack`
   (the direct "selection change while another vehicle is latched" case the
   acceptance criteria ask for). A new `DriveControllerRegistryTests` covers
   the registry itself - same id returns the same instance, different ids
   return different instances, a fresh instance starts unarmed and unlatched,
   and arming one rover through the registry never arms another. I checked
   the registry tests were worth having by mutating `For` to always return one
   shared instance and rebuilding: two of the four went red, on exactly the
   assertions that matter (different ids sharing a controller; arming one
   arming the other) - then rebuilt clean from reverted source before trusting
   the full suite again, per the stale-`bin` lesson `#20`'s mutation testing
   already cost me once.

4. **The geofence alert named the wrong rover (#4).** Not something Dana
   flagged - I found it reading `GeofenceMonitor` while working out what else
   in the station shared state the way `DriveController` used to. `IsOutside`
   took a `position` parameter and then tested `_lastEvaluated`, a single
   field left over from the *previous* call, instead. With one
   `GeofenceMonitor` serving the whole fleet and `MainWindow` calling it once
   per rover per frame, "the previous call" was whichever rover's frame the
   station had decoded immediately before - so every verdict was actually
   about a different vehicle. The simulator sends the fleet in the same
   order every tick, which made the mis-attribution deterministic: Sandstorm
   is the only rover built with `takesWideLines: true`, and its excursions
   were coming back as a warning about Mishmish, the rover one slot behind it
   in the roster, while Mishmish stayed on the racing line the whole time. A
   marshal reading that warning would have been sent to the wrong vehicle.
   I re-scoped the issue from P1 to P0 once I'd traced that through - a
   one-sample lag is a nuisance, sending help to a car that isn't in trouble
   while the one that is goes unreported is not.

   The comment above the old code called the lag "a sample of hysteresis."
   It wasn't - hysteresis needs a consecutive-sample threshold or separate
   enter/exit boundaries, and a single stale field is neither, it is just a
   bug. I decided against adding real hysteresis in its place: this is a
   safety alert, and delaying it to smooth a single noisy GPS fix trades a
   correctness problem for a timeliness one. `IsOutside` now takes
   `(roverId, position)` and reports the fence state of exactly the position
   it is given, immediately - no memory of any previous call, for this rover
   or any other. `roverId` doesn't affect the answer; it stays in the
   signature so a call site can't check one rover's position while logging
   the verdict under another's name, and so a future per-rover hysteresis
   scheme would not need to change every caller. Getting rid of the lag also
   got rid of the shared state that caused the mis-attribution in the first
   place - there is nothing left in the monitor for one rover's call to leak
   into another's.

   Six tests in `GeofenceMonitorTests` cover inside, outside, crossing both
   directions, the first-ever sample for a rover (the old code forced this
   to "inside" no matter where the rover actually was), and - the direct
   regression test - interleaved calls for two different rovers, asserting
   each verdict matches only its own rover's position. I checked the tests
   were worth having by reinstating the original shared-field version
   (adapted to the new signature) and rebuilding: five of the six went red,
   including the interleaved one, which failed on the very first call it
   made for Sandstorm - which is exactly the bug.

   Verifying against the real simulator needed a throwaway console harness
   rather than the WPF station, because `RoverRally.Station.exe` is a
   `WinExe` and its `Log.Warn` output goes nowhere I could capture headless.
   The harness reproduces `MainWindow.ApplyFrame`'s geofence handling exactly
   - one `GeofenceMonitor`, the real fence bounds from `App.config`, one
   `IsOutside(roverId, position)` call per rover per frame - against the real
   simulator over real UDP. It surfaced something the issue itself warned
   about: the simulator sends a no-fix, zeroed-position frame for every
   rover on its own periodic schedule (`tick % 40 == rover.Id` in
   `Program.cs`, so once per rover every 8 seconds, staggered), and `(0,0)`
   is outside any fence, so every rover warns on its own schedule regardless
   of this fix. That's `#36`, not this bug - and the harness confirmed the
   difference: those warnings were each rover reporting its own real `(0,0)`
   frame, not another rover's position under its name. Ninety seconds in, a
   genuine excursion showed up on top of that noise -
   `12:14:31.803 [WARN] Sandstorm has left the fenced area. pos=32.282813, 34.920425`
   - a real, non-zero position, and Sandstorm was the only rover it was ever
   attributed to across the whole run. `#36` still needs its own fix before
   the fence alert is fully trustworthy, exactly as the issue said; what this
   change fixes is that whichever position a verdict is based on, it is now
   always the position - and the rover - it was actually asked about.

5. **A no-fix frame moved the rover, then blamed it for leaving the fence
   (#36).** Not something Dana flagged - I found it running the station
   against the simulator to verify #20, and #4's own verification harness
   had already surfaced the other half of it in passing (see above).
   `docs/rover-link-protocol.md` is explicit that a no-fix frame's
   latitude and longitude are zeroed and are not a position, but nothing in
   the station read `TelemetryFrame.HasGpsFix` - only three
   `FrameCodecTests` cases did - so `MainWindow.ApplyFrame` took the
   zeroed reading at face value. The simulator drops the fix for each
   rover on its own schedule, `tick % 40 == rover.Id`, once every 8 seconds
   at 5Hz; `(0,0)` is the Gulf of Guinea, well outside the surveyed track,
   so every rover raised "has left the fenced area" on its own cycle
   regardless of where it actually was - a warning that meant nothing,
   indistinguishable in the log from one that did. `TrackView.UpdateRover`
   made the visible half worse: it already worked out the projected point
   was off-canvas and skipped the marker, label, and heading for it, but
   appended the point to the trail `Polyline` before that guard, so the bad
   sample still drew a line from the marker's real position down to the
   bottom-left corner of the map on every cycle.

   `RoverRally.Tests` has no reference to `RoverRally.App` - the same
   constraint the `DriveController` extraction in #20 ran into - so the
   decision of whether a frame gets to move the rover had to live
   somewhere testable. I moved the field-application block out of
   `MainWindow.ApplyFrame` and into `Rover.ApplyFrame(frame, receivedUtc)`
   in Core: every field applies unconditionally except `Position`, which
   only updates when `frame.HasGpsFix`. `MainWindow.ApplyFrame` now calls
   that and skips the geofence check entirely for a no-fix frame, instead
   of judging a reading that isn't a position - it leaves the last verdict
   standing rather than guessing. `TrackView.UpdateRover` got the one-line
   fix the issue asked for: the on-canvas guard now runs before the trail
   gets a new point, not after.

   `RoverTests` covers the scenario the issue asked for directly: a good
   frame, a no-fix frame, and a second good frame in sequence, asserting
   the rover's position holds through the middle frame and resumes moving
   on the third, plus that everything else a no-fix frame carries (speed,
   heading, battery, status) still lands. I checked the tests were worth
   having by reverting the `HasGpsFix` gate and rebuilding: the two tests
   that exercise a no-fix frame both went red, on exactly the assertion
   that matters - the rover jumped to `(0,0)` instead of holding still.
   Verified against the running simulator too, station stdout redirected
   to a file rather than the throwaway harness #4 needed: watched the
   Track tab and the log across several 8-second cycles for every rover,
   with no fence warnings and no trail excursions, only the marker sitting
   still on a fix-less tick and picking back up on the next one.

   A Codex review on the pull request found the gap the simulator's own
   pattern - one isolated no-fix frame at a time - couldn't surface:
   `MainWindow.ApplyFrame` was still calling `Track.UpdateRover`
   unconditionally, and since `Rover.ApplyFrame` leaves `Position` at its
   last known value on a no-fix frame, that call just re-appended the same
   retained point to the trail on every no-fix tick. One point is
   invisible; a real outage lasting long enough - 300 consecutive no-fix
   frames, a minute at 5Hz - would fill the whole `TrailLength` buffer
   with copies of a stale point and evict the genuine route underneath it.
   `Track.UpdateRover` now runs inside the same `frame.HasGpsFix` gate as
   the geofence check: a no-fix frame draws nothing, exactly as it judges
   nothing.

6. **The per-operator registry key broke across restarts once the station moved
   to .NET 8 (#17).** The issue named the exact line: `StationSettings.ProfileKey`
   built the registry subkey from `Environment.UserName.GetHashCode()`. That hash
   is stable within a process on .NET Framework but randomized per process on
   .NET Core and later - a deliberate hash-flooding mitigation, not a runtime bug.
   Once #15 retargeted every project to net8.0-windows, every launch computed a
   different `Profile_XXXXXXXX` key, so `PreferredSpeedUnit` and
   `LastSelectedRoverId` were written somewhere new each time and never read back
   - preferences looked like they silently reset on every start, and the
   abandoned keys piled up under `HKCU\Software\RoverLink\Station` forever.

   Fixed by deriving the key from the sanitized username itself instead of any
   hash - `StationSettings.BuildProfileKeyName` replaces anything outside
   `[A-Za-z0-9._-]` with `_` and falls back to a fixed placeholder for a null or
   empty username - which is deterministic by construction and, unlike the old
   hash, human-readable in the registry. `StationSettingsTests` pins down
   determinism, that two different usernames still land in different slots, and
   that unsafe characters get replaced rather than crashing or degenerating to an
   empty key. I confirmed the regression itself was fixed the way the codec bugs
   were - against the real thing, not just a unit test - by loading the built
   `RoverRally.Core.dll` and driving `StationSettings` from two separate,
   short-lived processes: the second read back exactly what the first wrote,
   which is precisely what the old `GetHashCode()` derivation could never
   guarantee. `MainWindow` now also runs a one-time `CleanUpAbandonedProfileKeys()`
   sweep at startup that removes any sibling `Profile_*` key that isn't the
   current one; I seeded three fake leftover keys - one was a genuine
   `Profile_4C859CCA` this station had already abandoned on this machine before
   the fix - and watched the sweep remove all three while leaving the live key
   and its values untouched.

   A Codex review on the pull request caught a real data-loss case my first cut
   of the sweep missed: a station upgrading straight from the old net48 build
   still has a `Profile_<hash>` key whose hash *was* stable there, holding the
   operator's real, live preferences - not every sibling is randomized-hash
   garbage, only the ones minted during the window between #15's retarget and
   this fix. My original sweep deleted every non-current sibling outright, so
   that one genuine legacy profile would have been destroyed, unread, on the
   very first launch after upgrading. `CleanUpAbandonedProfileKeys` now copies
   any `SpeedUnit`/`LastSelectedRoverId` a sibling holds into the current key
   first - without overwriting a value already there - and only then deletes
   it. I re-verified against the real registry: seeded a legacy-shaped key with
   real values, ran the sweep, and confirmed both values landed under the new
   key and read back correctly through `StationSettings`; then seeded an empty
   orphan and one with a conflicting `SpeedUnit`, ran it again, and confirmed
   the empty one is simply removed while the conflicting value does not clobber
   what the (by-then-current) key already holds.

   Worth calling out, per the issue's own "also worth considering": `HKCU` is
   already scoped to one Windows user, so the per-operator subkey only actually
   separates two shifts sharing a PC if each shift logs into its own Windows
   account. If a station instead shares one Windows login across shifts - which
   the original comment's "shifts sharing a PC" wording doesn't rule out - every
   operator computes the same `Environment.UserName` and lands in the same slot
   regardless of this fix; the split was never doing anything for that setup, on
   either .NET Framework or .NET 8. I kept the split - removing it was out of
   scope for this issue and the acceptance criteria assume it stays - but it's a
   decision worth the owner making deliberately rather than by default.

---

## What You Removed

*Dana asked you to use your judgement about what still belongs. What did you take out, and how did you satisfy yourself it was safe to remove?*

**`AnalyzerHost`, `StationMonitorService`, `RunSnapshot`.** Both are hard
blockers for the .NET 8 move on their own terms - `AnalyzerHost` loads
"post-run analyzers" into a second `AppDomain`, and `AppDomain.CreateDomain`
is not supported outside .NET Framework (it throws `PlatformNotSupportedException`
on .NET Core/5+); `StationMonitorService` publishes the fleet over
`System.Runtime.Remoting`, which has no .NET 8 implementation at all, with
`RunSnapshot` serialized through `BinaryFormatter`. Before deciding whether to
port them or drop them, I spiked (#1) whether anything actually uses them,
because a working office-overview client is a different problem from a dead
one.

Nothing does. I traced every reference to both types across the App, Core,
and Tests projects, the `.csproj` compile lists, `App.config`, and every doc
in the repo. Both classes compile into `RoverRally.Core`
(`RoverRally.Core.csproj:57` for `AnalyzerHost`, `:58-59` for `RunSnapshot`
and `StationMonitorService`) but neither is ever constructed anywhere. No
`Analyzers` folder and no `*.Analyzer.dll` exist anywhere in the repo, source
or build output, so `AnalyzerHost.Discover()` has nothing to find even in
principle. At the time of this spike, `App.config:18-20` did define
`StationMonitorEndpoint` and `StationMonitorPort`, and the only code that read
them was `SettingsView.xaml.cs:31-32`, which concatenated the two values into
a read-only `TextBlock` for display - neither value was ever passed to
`StationMonitorService.Publish`, and nothing called `Publish` at all. (Both
citations are stale as of #8, which deleted that config and that display line
outright - see the `#8 closes...` paragraph below for what replaced them.) The
"office overview client" is not a running feature with dead config left over
from decommissioning it; it's a Settings-tab label pointing at a service that
never starts.

`docs/operations-guide.md:121-129` documents both as working - "the overview
client in the office can attach to a running station," "post-run analyzers...
are picked up automatically." I did not take that as evidence of use, for the
same reason "trust the code over the documents" mattered on the telemetry SDK
and cut the other way on the emergency stop: a document tells you what's
*supposed* to happen, not what does. Here the code, the build output, and the
absence of a single caller all agree with each other and disagree with the
document. `docs/architecture.md:12` dates the office overview client to 2019,
and `docs/architecture.md:16` notes nobody has owned the station full time
since 2021, which is consistent with docs describing a feature that quietly
stopped being used and was never unwritten. `INSTRUCTIONS.md` - Dana's brief,
kept local per this repo's own convention (`.git/info/exclude`) and not
committed, so it isn't visible on GitHub alongside this diff, but readable on
disk exactly like every other doc I cite here - never mentions monitoring,
remote overview, or analyzers in any form.

**Decision: remove outright, not port.** Porting either one means building new
functionality from scratch - a real remote-monitoring channel, a real plugin
host - for a feature with zero current callers and no client ask. That's a
new feature dressed as a migration, and the risk profile is worse than doing
nothing: `BinaryFormatter` in particular is a deserialization hazard the
industry has been actively moving away from, so reviving it on the far side
of this migration would be a step backward even if I did wire it up.

I haven't deleted the files yet - that's #7 and #8, which this issue blocks -
but I've scoped it here so it isn't rediscovered: `Core/Monitoring/AnalyzerHost.cs`,
`Core/Monitoring/StationMonitorService.cs`, `Core/Monitoring/RunSnapshot.cs`
(and `RunSnapshotEntry`), the `System.Runtime.Remoting` project reference, the
two keys then at `App.config:18-20`, the display line then at
`SettingsView.xaml.cs:31-32`, and the stale "Remote monitoring" /
"Post-run analyzers" sections of `docs/operations-guide.md:121-129`. All of
these line numbers are as of this spike; #7 and #8 below record what each
issue actually removed and where. Full evidence trace, with the same
file:line citations, is on #1.

**#7 closes the `AnalyzerHost` half of this.** I re-checked the spike's
evidence myself before deleting anything rather than taking my own prior
write-up on trust: a repo-wide grep for `AnalyzerHost` still turned up nothing
but the type's own file and this file's own historical notes about it, and
`RoverRally.Core.csproj` has
no explicit `<Compile Include>` for it - the SDK-style implicit glob was
already picking it up, so deleting the file needed no project-file edit.
`Core/Monitoring/AnalyzerHost.cs` is gone; `RunSnapshot.cs` and
`StationMonitorService.cs` stay untouched, since `RunSnapshot` is #9's to
remove once both #7 and #8 have landed. The "Post-run analyzers..." line came
out of `docs/operations-guide.md`'s "Remote monitoring" section; the rest of
that section, describing the office-overview client, is #8's to close out.
Rebuilt the solution and reran the test suite after the deletion - still 76
passed, 1 skipped, 0 failed - to confirm nothing depended on the type despite
the spike's own trace.

**#8 closes the `StationMonitorService` half of this.** Same re-check
discipline as #7: a fresh repo-wide grep for `StationMonitorService`,
`StationMonitorEndpoint`, `StationMonitorPort`, and `MonitorEndpointText`
turned up nothing outside the exact files the spike had already listed, so
nothing had grown a new dependency on any of them in the meantime.
`Core/Monitoring/StationMonitorService.cs` is gone, and with it the only
source in the repo that used `System.Runtime.Remoting`, so the reference came
out of `RoverRally.Core.csproj` too - the acceptance criterion is "no
`System.Runtime.Remoting` reference," not just "no `StationMonitorService`,"
and I checked the build output confirms neither. `App.config` loses the
`StationMonitorEndpoint`/`StationMonitorPort` keys and the comment that came
with them; `SettingsView` loses the "Overview endpoint" row and the display
line that read those two keys into it, which was the only code anywhere that
touched them. (This supersedes earlier citations in this section to `App.config:18-20` / `SettingsView.xaml.cs:31-32` and the migration-table entry that listed `System.Runtime.Remoting` as still explicit.) `docs/operations-guide.md`'s "Remote monitoring" section and the
"office overview client" mention in `docs/architecture.md`'s history line are
gone too, for the reason the spike gave: the docs described a feature that
had exactly one caller, and that caller printed two config values to a
read-only text box.

`Core/Monitoring/RunSnapshot.cs` stays untouched, same as #7 left it - it's
#9's to remove, and `StationMonitorService` was its only caller, so it is now
orphaned rather than deleted. Rebuilt the solution and reran the test suite
after the deletion - still 76 passed, 1 skipped, 0 failed.

**#9 closes the `RunSnapshot` half of this, and with it the last
`BinaryFormatter` usage in the solution.** Same re-check discipline as #7 and
#8: a fresh repo-wide grep for `RunSnapshot`, `RunSnapshotEntry`, and
`BinaryFormatter` turned up nothing outside `RunSnapshot.cs` itself -
`StationMonitorService`, its only caller, was already gone by #8, so nothing
had grown a new dependency on it in the meantime. `Core/Monitoring/RunSnapshot.cs`
is gone, and with it `Core/Monitoring/` itself, now empty. No `.csproj` edit
was needed - the same implicit SDK-style glob that picked the file up without
an explicit `<Compile Include>` stops picking it up once it's deleted.

This one is worth calling out on security grounds specifically, not just as
migration cleanup. `RunSnapshot.Deserialize` fed bytes taken straight off
`StationMonitorService`'s Remoting channel into `BinaryFormatter.Deserialize`.
`BinaryFormatter` deserializes by walking arbitrary type metadata embedded in
the payload itself and instantiating whatever it names - it does not just
read data into a known shape, it lets the payload pick the shape. Feeding it
untrusted network bytes is a textbook remote-code-execution vector, which
is exactly why .NET disables `BinaryFormatter` by default starting with .NET 8
and removes it outright in .NET 9. Even setting the .NET-version deprecation
aside, this would have been worth fixing on its own terms - the RCE risk does
not depend on which runtime is hosting it, only on the fact that untrusted
bytes reached `Deserialize` at all. Removing the caller in #8 closed the
network exposure; removing the type itself here closes the possibility of a
future caller reopening it by construction, rather than by convention.

Rebuilt the solution and reran the test suite after the deletion - 87 passed,
1 skipped, 0 failed (the pass count has grown since #8's 76 as later issues
added coverage; the skip and failure counts are what matter here, and both
are unchanged).

**The Track tab's skid-pad ellipse (#66).** Much smaller than the three
above, but the same judgement call in miniature. While manually verifying
#26 against the real simulator I noticed `TrackView.xaml` draws a static
circle below the oval loop on every run, regardless of site. The oval right
above it is commented "drawn to scale from the site survey" and its geometry
is a real stadium shape; the circle's only comment is `<!-- Skid pad -->`,
with no survey backing, no lat/lon anchoring through `TrackProjection`
(everything else on the map that's meant to be real - the geofence polygon,
the rover markers - goes through `TryProject`), no config key, no test, and
no mention in either doc. A repo-wide search turned up exactly one hit for
"skid pad": the XAML comment itself. I raised #66 to make the removal a
decision on record rather than a drive-by edit, and decided to delete it
outright rather than invent per-site configuration for a shape no site has
ever actually measured - that would have been new speculative surface area,
not a fix. `TrackView.xaml.cs` never referenced it, so the change is a
single XAML deletion. Rebuilt and reran the suite afterward - 147 passed, 0
skipped, 0 failed, unchanged from before the deletion.

---

## Logging

*What did you build, and what decisions did you make about it?*

**What I built (#22).** `Core/Logging/Log.cs` was `Console.WriteLine`/
`Debug.WriteLine` only, so unless a developer had Visual Studio's Output
window open, every log line went nowhere - exactly Dana's complaint, and no
help at all when she needs something to attach to a fault report from a
laptop in a tent that nobody is watching. `Log` is now a thin composition
root over Serilog: `Log.Configure(StationOptions)`, called once from
`App.xaml.cs.OnStartup`, builds a pipeline writing to the console, the
attached debugger (preserving the exact behaviour the old docstring
promised), and a rolling file - `station-.log` under a configurable
directory, `RollingInterval.Day` plus `rollOnFileSizeLimit`, so a file rolls
on whichever comes first. The existing static `Log.Debug/Info/Warn/Error(...)`
facade kept its exact signatures and now routes through that same pipeline,
so the ~30 call sites in `MainWindow`, `App.xaml.cs`, `StationSettings`,
`StationOptions` and `RoverRoster` needed zero changes to start landing in
the file.

**The hard constraint was the disk, not the plumbing.** The brief is blunt
about it - a few gigabytes free at best, unattended for a week - so I fixed
the rolling policy rather than exposing it: `Log.FileSizeLimitBytes` (5 MB)
and `Log.RetainedFileCountLimit` (10). Serilog's retention counts every file
matching the rolling pattern, day-rolls and size-rolls alike, and deletes the
oldest beyond the count, so the worst case is exactly `5 MB x 10 = 50 MB`,
regardless of how many days that spans - about 2% of "a few gigabytes," with
room to spare for the site's other files. `LogTests.WritingManyLogEntriesNeverExceedsTheConfiguredRetentionFootprint`
demonstrates this rather than leaving it asserted in prose: it points a real
Serilog logger at a temp directory with a small size/retention pair, writes
roughly 400 KB of lines - two orders of magnitude past that pair's own 6 KB
bound - and asserts the total bytes actually left on disk never exceeds it.
If Serilog's retention ever silently stopped deleting old files, that test
goes red; a worst-case number that's only ever been typed into a doc, never
exercised, is not something I was willing to put in this section.

Log directory and minimum level are the two things the acceptance criteria
ask to be configurable, and only those two are: `StationOptions.LogDirectory`,
read from a new `"LogDirectory"` key in `appsettings.json`'s existing
`"Station"` section, right next to the `"LogLevel"` key that already drove
nothing but the console/debug facade before this issue. The default -
`%LocalAppData%\RoverLink\Station\Logs`, expanded via
`Environment.ExpandEnvironmentVariables` - is per-user and does not sit under
the exe's own install location, which may be `Program Files`, unwritable by
a non-admin operator; it deliberately mirrors the `HKCU\Software\RoverLink\
Station\...` registry namespace #17 already established for the same
non-admin-writable reason.

**Failure to open the file must not take the station down.** `Log.Configure`
wraps the Serilog build in try/catch: if the file sink can't be created - bad
path, no permission, a full disk - the failure is written to the surviving
console/debug sinks and the pipeline continues without the file sink, rather
than the exception propagating into `App.xaml.cs.OnStartup` and killing
startup outright. `LogTests.ConfigureDoesNotThrowWhenTheLogDirectoryCannotBeCreated`
reproduces the failure with a real file sitting where a directory is
expected (so `Directory.CreateDirectory` inside the File sink genuinely
throws) and asserts `Configure` survives it and logging still works.

**Testability without a repo-wide rewrite.** The agreed scope named four
things: telemetry, the emergency-stop path, battery, and session cache.
`TelemetryClient` and `CommandSender` are real instance classes, so they got
straightforward constructor-injected `ILogger` (defaulting to
`Log.CreateLogger<T>()` when not supplied, so nothing else that constructs
them needs to change). `BatteryGauge`, `SessionCacheFile` and
`SessionCacheMigrator` are static utilities with call sites all over the
place, so each gained a new `ILogger`-taking overload with the real logic,
with the original overload becoming a one-line delegate through
`Log.CreateLogger(...)`. That kept every existing call site - production and
test - compiling unchanged, while giving new tests a real injection point.
`DriveController` - the emergency-stop path itself - stayed untouched:
that logging lives in `MainWindow.xaml.cs` today and stays there, on the
static facade; `DriveControllerTests` already covers the safety invariant
through state and return values, and the owner confirmed this class doesn't
need to change for this issue.

`BatteryGauge` had zero logging before this issue - it's pure arithmetic, no
failure mode of its own to report. Rather than thread a logger through it
for testability's sake alone, I gave it something real to say: `ToPercent`
now logs a Warning whenever the *raw* computed percentage falls outside
0-100 before clamping, in either direction - that's not "the pack is flat or
full," it's the reading itself being implausible, which is exactly the
sensor-failure case #2 already clamps silently. A flat pack reading 0 mV
(the simulator's own failure sentinel) now says so in the log instead of
just showing 0% with no explanation - I saw this fire for real during manual
verification below, on the very first run.

**How I verified it, beyond the unit tests.** Ran the real simulator and the
real `RoverRally.Station.exe`, launched from a shell so the console sink was
visible for comparison against the file. Both carried the same lines,
byte-for-byte apart from Serilog's own formatting, including a genuine
`[WRN] Battery reading 0 mV computed to -250% before clamping; a sensor may
be failing.` from one of the simulated rovers - the new behaviour working
against a real signal, not a crafted one. `%LocalAppData%\RoverLink\Station\
Logs\station-<date>.log` existed and matched. `dotnet test` - full suite
green (the pre-existing skip aside), including the two `LogTests` above.

I grepped the two other docs for "log" while I was in there, since a doc
that describes the old, nonexistent behaviour is worse than one that says
nothing. `docs/operations-guide.md`'s "Logs" section already claimed a
rolling log existed - at `C:\ProgramData\RoverRally\station.log`, a path
that was never real and, being under `ProgramData`, would have needed admin
rights this issue deliberately avoids requiring. Corrected it to the real
path and added the retention numbers an operator actually needs (roll
triggers, 50 MB worst case, the `LogDirectory` override). `docs/architecture.md`'s
"Known rough edges" list carried "There is no logging to disk from the
application itself," which this issue now makes untrue, so I removed it
rather than leave a fixed rough edge in a list of current ones. Neither is
the `App.config` staleness CLAUDE.md already defers to #26 - that's a
different claim in the same two files, still accurate, left alone.

---

## Tests

*What did you test and why? What did you have to change in the application to make it testable? What did you do about the three tests that were already there?*

*So far this covers the telemetry codec only; this section grows as other issues
land.*

The emergency stop needed a structural change before it could be tested at all.
The latch lived in `MainWindow.xaml.cs`, wired to a `DispatcherTimer` and reading
sliders directly, and `RoverRally.Tests` has no reference to `RoverRally.App`, no
WPF assemblies and no STA plumbing. So I moved the one decision that matters -
given latch state, armed state, throttle and steering, what should the next frame
contain - into `DriveController` in Core, and left the code-behind calling it. The
handlers now hold no logic worth testing, which is the point.

Ten tests cover it, and I checked they were worth having by putting the original
two faults back and rebuilding: the missing re-assertion and the stale armed flag.
Seven of the ten went red, including the one that reproduces Dana's report
directly - stop engaged, throttle left up, tick. The three that stayed green are
the ones covering normal driving, which the bug never touched.

That run also caught a weak test of my own. `NeverReportsItselfArmedWhileTheStopIsLatched`
originally checked the invariant only at the end of a sequence, and passed against
the broken code, because a controller that forgets to disarm on the stop and then
disarms on the next button press reaches the same final state - by a route that
would have driven the vehicle away in between. It now asserts after every step.

`RoverTests` (#36) is the same move again, for the same reason: whether a
telemetry frame gets to move a rover on the map is a decision, and it lived in
`MainWindow.ApplyFrame` where nothing in `RoverRally.Tests` could reach it. It
now lives in `Rover.ApplyFrame`, a plain method on the model that already owns
the state it mutates, and four tests pin down the one case that matters - a
no-fix frame between two good ones must not move the rover, but must still
apply everything else it carries.

Unit tests could not settle the last question, though, because the failure is a
property of a conversation over time rather than of a function. So there is also
a throwaway harness that runs the real `DriveController` and `CommandSender`
against the real simulator over real UDP at the station's own 200ms cadence: arm,
drive, stop with the throttle still at full, then sixty consecutive ticks. The
vehicle stayed at zero for all sixty and reported the stop on every frame. Twelve
seconds matters here - the old code released after one tick, and the simulator has
its own two-second command-loss failsafe that would have masked a broken latch if
the station had simply gone quiet.

27 new tests, in `Crc8Tests` and `FrameCodecTests`. I tested the codec heavily
because it is the one component where being subtly wrong is invisible - a frame
that decodes to plausible-but-wrong numbers looks exactly like a working station
until somebody trusts a reading. So the tests are exhaustive where exhaustive is
cheap: every byte value, every buffer length up to 64, every status-flag
combination, every documented rejection path.

Nothing in the application had to change to make this testable. The codec is a
pure function of bytes, which is precisely why it was worth carving out
faithfully rather than restructuring the call sites around it.

The one piece of scaffolding is `SimulatorFrameWriter.cs`, a deliberate duplicate
of the simulator's `FrameWriter`. The duplication is the point - it shares no
code with the implementation under test - but it is annotated so it stays in step
if the simulator ever changes.

It is worth recording what all that coverage still missed. An automated review on
the pull request found that the bounds check in `Crc8.Compute`,
`offset + count > buffer.Length`, overflows: C# arithmetic is unchecked by
default, so an `offset` of `int.MaxValue` wraps the sum negative, sails past the
guard, and reaches the indexer - the method threw `IndexOutOfRangeException`
where it documents `ArgumentOutOfRangeException`. My argument-validation tests
used *plausible* bad inputs (negative values, a range two past the end) and never
hostile ones, so they all passed. I reproduced it as a failing test first, then
rewrote the check as a subtraction that cannot overflow for non-negative inputs.

The lesson I am taking from that: exhaustive over the domain is not the same as
exhaustive over the edges. I had tested all 256 byte values and every buffer
length up to 64, which reads as thorough, and still had a guard clause that could
be walked straight through.

The same review pointed out that `TelemetryFrame` documented itself as immutable
while using `private set`, which still permits mutation from inside the class.
Get-only auto-properties compile to readonly backing fields, so the compiler now
enforces what the comment claims rather than merely asserting it.

The three pre-existing tests are untouched by this issue. `BatteryGaugeTests` and
both `SpeedConverterTests` pass; `TrackProjectionTests.PlacesTheStartLine` was
already skipped and still is. The `SpeedConverter` test that encodes the imperial
bug, and that skipped test, each have their own issue - changing them here would
have mixed unrelated work into a codec change.

`SpeedConverterTests.FormatsCruiseSpeedForTheImperialReadout` was that test, and
its own issue (#3) is now closed. Its assertion of `"0.4 mph"` for 500 cm/s was
not a false negative slipping past real coverage - it was the coverage,
correctly reporting the code it was written against. Fixing the bug meant
fixing the test's expectation to `"11.2 mph"` in the same change; leaving it
asserting the old value would have turned a real regression test into a
guaranteed failure the moment the bug was fixed. I added zero-speed and
maximum-controller-speed cases alongside it (0 cm/s and `ushort.MaxValue`
65535 cm/s, the ceiling of the protocol's `uint16` ground-speed field), each in
both units, so the imperial and metric readouts are pinned against each other
at the boundaries and not just at one mid-range value.

**#21** asked for the same two things this section already describes above -
extract the command-assembly decision out of `MainWindow.xaml.cs`, and cover
the emergency stop latch with tests - but by the time I picked it up, both had
already shipped: the extraction landed with #20, and the freshness-gate work
in #37/#39/#40 grew `DriveControllerTests` well past the original ten. Rather
than write parallel tests that would duplicate existing coverage, I checked
#21's five `What to cover` bullets against what is already there:

- Stop flag asserted on every subsequent frame, not just the first -
  `HoldsTheStopFlagOnEveryTickUntilReArmed`, which asserts it across 50 ticks.
- The latch survives throttle movement, the original failure mode -
  `IgnoresTheThrottleWhileTheStopIsLatched`, and the full-throttle tick inside
  `NeverReportsItselfArmedWhileTheStopIsLatched`.
- Only an explicit re-arm clears the latch - `ClearsTheLatchOnlyWhenTheOperatorReArms`
  and `RefusesToReArmWhileTheThrottleIsOffCentre`.
- Local armed state consistent with what is transmitted -
  `NeverReportsItselfArmedWhileTheStopIsLatched` and its `AssertInvariant`
  helper, plus `DisarmsLocallyWhenTheStopIsEngaged`.
- Encoded frames carry the flag at bit 0 -
  `EncodesTheLatchedStopAsBitZeroOfTheCommandFlags`, which asserts
  `datagram[8] & 0x01` directly and then round-trips the frame through
  `SimulatorFrameWriter.TryReadCommand` to confirm the rover-side reader
  agrees.

"A test fails against the pre-#20 behaviour and passes after it" is the
mutation-testing paragraph above - putting the original two faults back
turned seven of the ten tests that existed at the time red, including the one
that reproduces Dana's report directly. I re-ran the current suite
(`dotnet test --filter "FullyQualifiedName~DriveControllerTests"`) rather than
the mutation again: 23 passed, 0 failed. No source or test changes were
needed for #21 - it closes against work already covered here.

**#19** is the first coverage `StationSettings`'s config-backed properties have
ever had - `StationSettingsTests` covered only `BuildProfileKeyName` before
this. Moving the parsing/fallback logic out of the static facade and into a
plain `StationOptions.Load(IConfiguration)` made it directly testable without
touching a real file on disk: `StationOptionsTests` builds an in-memory
`IConfiguration` and asserts the three cases the issue's acceptance criteria
actually turn on - every key's documented default when the `Station` section
is absent entirely, every key honoured when present and valid, and (the one
that mattered most) a present-but-malformed value falling back to the same
default rather than propagating a `FormatException` or being silently
accepted as something it isn't. One case is culture-specific on purpose -
`TrackNorth`/`TrackSouth`/`TrackWest`/`TrackEast` parse under a forced
`de-DE` thread culture, where `,` is the decimal separator, to pin down that
`"32.5"` still means thirty-two point five and not three hundred twenty-five,
which is exactly the kind of bug that would only show up on a
non-English-locale build machine. `StationSettingsTests` gained one more case
- reading a config-backed property before `Configure()` is called throws
`InvalidOperationException` rather than returning a default or null, so a
future call site added ahead of the `App.xaml.cs` bootstrap fails loudly
instead of reading garbage. Verified end to end too, not just at the unit
level: I hand-edited the deployed `appsettings.json` to give `TelemetryPort`
and `TrackNorth` malformed values, relaunched the real station against the
simulator, and confirmed the log showed both `Error` lines naming the bad
key and value before the station came up listening on the default port
14550 - then restored the file.

**#22** needed a different shape of testability than everything above: not
"extract a decision out of `MainWindow` so it can be reached at all" but
"give a handful of classes an injection point without rewriting the ones
that don't need one." `TelemetryClient` and `CommandSender` - real instance
classes with a single production construction site each - took
constructor-injected `ILogger`, defaulting to the static facade when not
supplied so nothing else that builds them needed to change; neither had any
test coverage before this issue, so `TelemetryClientTests`/
`CommandSenderTests` are new. `BatteryGauge`, `SessionCacheFile` and
`SessionCacheMigrator` are static utilities with call sites scattered across
the app and the existing test suite, so each gained a new `ILogger`-taking
overload carrying the real logic, with the original overload reduced to a
one-line delegate - every existing call site, production and test alike,
kept compiling unchanged, and the new tests inject a hand-rolled
`CapturingLogger` (this repo has no mocking framework, and one class did not
earn it one) directly into the new overload. `DriveController` - named in
the issue as "the emergency-stop path" - deliberately got none of this: that
logging stays in `MainWindow.xaml.cs` on the static facade, per the owner,
and `DriveControllerTests` above already covers the actual safety invariant
through state and return values, not logging. The rolling-file mechanism
itself is demonstrated, not just unit-tested against a mock sink - see
`LogTests` in the Logging section above, which writes real bytes to a real
temp directory and measures what's left after retention runs.

**#23** asked for boundary and failure coverage on the five calculation
classes, describing the repo as it stood when the issue was filed: four
tests, three single-value happy-path assertions, one asserting a known-wrong
result. By the time I picked it up that description no longer matched
reality, the same way #21 found `DriveControllerTests` had already outgrown
its own ask. `BatteryGaugeTests` and `GeofenceMonitorTests` grew alongside
the #2/#42 clamp fix and the #40 cross-rover leak fix; `SpeedConverterTests`
grew alongside #3's fix, described just above; and `SessionCacheFileTests`
already had a case covering
`SessionCacheRecord.FromTicks`'s lower-bound guard from the #11/#51
session-cache work. `TrackProjection` was the one class the issue's
description still fit exactly: one test, `PlacesTheStartLine`, `[Ignore]`d
because its expected pixels went stale when canvas sizing moved into the
view layer, and it never exercised the false-path branch at all.

So rather than write parallel suites that would duplicate what #2/#3/#40/#51
already established, I checked each class's existing coverage against the
issue's own list and closed only what was actually missing: an exact
mid-range percent for `BatteryGauge.ToPercent` (the existing
`ReportsAPercentage` only asserted `>= 0`) plus the exact `IsCritical`
threshold at 15%/16%; a direct cross-check that `ToKilometresPerHour` and
`ToMilesPerHour` agree for the same input, independent of `Format`'s
rounding, asserted both as exact expected values and as the literal
relationship `mph == kmh * 0.621371`; new `TrackProjectionTests` covering
all four corners, the centre, and the false path in all four directions,
since that class had nothing to build on; a point exactly on the fence
boundary plus a direct call to `GeofenceMonitor.Contains` (existing tests
only ever went through `IsOutside`); and `FromTicks`'s upper-bound guard,
the two exact boundary tick values that must *not* clamp, and what
`Duration` does when only one endpoint is corrupt. `PlacesTheStartLine`
stays skipped - fixing its stale pixels is a different, narrower change
than "add the missing coverage," and mixing the two would have made this
change harder to review for what it actually is.

Codex's review on the PR caught a real gap in the new corners/centre cases:
every one of them used equal lat/lon spans and a square canvas, so a
regression that swapped `_width`/`_height`, or swapped the latitude and
longitude spans, would still have passed all of them.
`ProjectsAnInteriorPointCorrectlyWithNonSquareBoundsAndARectangularCanvas`
closes that - unequal spans and a rectangular canvas give x and y different
scale factors, so a swap changes the projected point. All five classes were
already correct; this issue changed no production code, only tests
(`dotnet test` on the rebuilt suite: 146 passed, 0 failed, the one
pre-existing skip).

**#24** asked me to finally resolve that one pre-existing skip -
`TrackProjectionTests.PlacesTheStartLine`, the fourth test in a class the brief
describes as having three, invisible in a normal run for long enough that even
Dana miscounted. The `[Ignore]` comment said the expected pixels had gone stale
when canvas sizing moved into the view layer, and that held up under checking:
the test hardcoded a 640x400 canvas, but the only real caller,
`TrackView.Configure`, has used 720x480 since that dimension became the view's to
own. `TrackProjection.TryProject`'s arithmetic hasn't changed since the original
baseline import, and un-ignoring an unmodified copy of the test in isolation
passed outright, exact to the asserted 0.5 delta. So this was a stale fixture, not
a wrong implementation and not dead functionality - there's no start-line or
lap-timer feature anywhere in this codebase yet for #30 to build on; the name is
just descriptive of an interior test point at realistic real-world-scale bounds
(~700m across). I updated the canvas size to the real 720x480, recomputed the
expected pixels from the same fractional position the original author chose -
dead centre of the bounds, so x=360, y=240 - and added the `Assert.IsTrue(result)`
check every other test in the class already had but this one never did.
`TrackProjectionTests` is 11 for 11 now, no skip, and there is nothing left
ignored anywhere in the suite. No production code changed.

**#25** asked for a suite covering `SessionCacheFile`/`SessionCacheMigrator`:
round-trip, an architecture-independent fixed record size, legacy migration
against the real 8-run shipped fixture, idempotency, corrupt/truncated input,
and the `FromTicks` tick guard - and named itself blocked by #11. By the time
I picked it up, #11 was already closed, and its own acceptance criteria
("all 8 runs read back correctly", "idempotent or detects an already-converted
file", "corrupt/truncated input fails safely", "confirm `FromTicks` still
guards") were close enough to #25's that the same PR had already added both
`SessionCacheFileTests` and `SessionCacheMigratorTests` to satisfy them. So,
the same move as #21 and #23: I checked #25's own bullets against what that
PR actually shipped rather than writing a parallel suite. Round-trip is
`AppendThenReadRoundTripsEveryField`. The architecture-independence guard is
`RecordSize`, a hand-picked `public const int` of 32 rather than anything
`Marshal.SizeOf`-derived (the exact bug #10/#51 fixed), pinned directly by
`AppendWritesThirtyTwoBytesPerRecordRegardlessOfArchitecture`. Legacy
migration is `MigratesTheRealShippedBytesAndPreservesTheOriginalAsABackup`,
against the real bytes of `RoverRally.App/Data/session-cache.bin`, not a
hand-built approximation. Corrupt/truncated input is covered on both sides of
the boundary the two classes actually draw: the migrator refuses outright
(`ThrowsAndLeavesTheFileUntouchedWhenLengthIsNotAWholeNumberOfRecords`, an
`IOException` with the file untouched), while the tolerant runtime reader
warns and discards the trailing partial record instead of throwing
(`ReadLogsAWarningForATruncatedFileViaTheInjectedLogger`) - both "fail safely
rather than producing garbage", just safely in different ways for different
callers. The tick guard has four boundary cases already in
`SessionCacheFileTests`, added for #23 covering `FromTicks`'s lower bound and
extended here in review to also pin the upper bound and the corrupt-`Duration`
consequence. The one bullet not covered by an identical scenario is
idempotency worded literally as "run the migration twice": the existing
`ReturnsFalseAndWritesNothingWhenAlreadyMigrated` builds its already-migrated
file via `Append` rather than by calling `MigrateIfNeeded` a second time on a
freshly-migrated one. I checked whether that gap was real rather than just
differently-phrased: `IsAlreadyMigrated` only ever inspects whether every
record's reserved gap is already zero, with no memory of how the file arrived
at that state, so a second `MigrateIfNeeded` call against a file this same
migrator just converted takes the exact code path that test already exercises.
I raised this distinction with the exercise owner directly rather than
deciding it myself, and the owner's call was to close #25 on the existing
coverage without adding a test that would exercise an already-covered branch.
`dotnet test` on the unmodified suite: 147 passed, 0 failed, 0 skipped. No
production or test code changed for this issue.

---

## Architecture Decisions

| Decision | Options considered | Chosen | Rationale |
|---|---|---|---|
| RL-100 telemetry SDK | Stay 32-bit and keep the DLL; obtain a 64-bit build from the vendor; reimplement | Reimplement inside `RoverRally.Core` | The vendor no longer exists, so no 64-bit build is obtainable. The surface in use is 16 members and is fully specified in-repo by the simulator and the protocol notes |
| Codec namespace | Keep `RoverLink.Telemetry` for a zero-line diff; move to `RoverRally.Core.Telemetry` | `RoverRally.Core.Telemetry` | Squatting a defunct vendor's namespace hides the fact that the dependency is gone. Moving cost four `using` directives |
| `TelemetryFrame` shape | Vendor parity (mutable class); `readonly struct`; immutable class | Immutable class | Nothing mutates a frame after decode. net48 is C# 7.3, so `init` accessors were not available |
| The vendor binary | Leave it on disk unreferenced; delete it | Delete the DLL and its XML | Makes "no 32-bit dependency remains" verifiable in this change rather than deferred to the retarget |
| Codec test oracle | Mirror of the simulator's writer; golden bytes; both | Both | A mirror alone shares any transcription error with the implementation, so by construction it cannot detect one |
| Where the emergency stop latch lives | Make the simulator latch; hold it in `MainWindow`; extract a controller into Core | Extract `DriveController` into Core | The simulator stands in for firmware that cannot be changed in the field, and the vehicle is documented as level-triggered, so the latch belongs to the transmitter. Leaving it in code-behind would have left the one safety-critical control in the solution untestable |
| Re-arming with the throttle raised | Allow it; snap the slider to zero; refuse | Refuse, and say why | The operator pressing the button is the marshal standing on the track. Clearing the latch into a raised slider drives the vehicle at them, which is the hazard the ops guide already warns about |
| Where per-rover drive state is keyed (#35) | A bare `Dictionary<byte, DriveController>` field on `MainWindow`; a new `DriveControllerRegistry` in Core | `DriveControllerRegistry` in `RoverRally.Core.Control` | `DriveController` was already extracted into Core specifically so `RoverRally.Tests` could reach it without a WPF reference; a lookup-and-cache concern that only MainWindow could exercise would have put the one thing #20 deliberately made testable back behind an untestable wall. `DriveController.cs` itself needed zero logic changes - it already took `roverId` on every call |
| Roster JSON library (#18) | Bump `Newtonsoft.Json` to 13.x; replace with `System.Text.Json` | `System.Text.Json` | It ships in the `net8.0` shared framework, so it removes a third-party dependency entirely rather than keeping one the runtime no longer needs just to parse one flat array. `Data/rovers.json`'s keys already match `Rover`'s PascalCase properties exactly, so the swap needed no shape changes |
| STJ case sensitivity for the roster (#18) | `PropertyNameCaseInsensitive = true`, matching Newtonsoft's default; leave STJ's case-sensitive default | Case-sensitive default | The file matches PascalCase exactly today, so nothing about it needs case-insensitivity. This is a deliberate behavior change from Newtonsoft - the exact caveat the issue called out - accepted rather than silently carried forward: a future hand-edit with mismatched casing now leaves that field at its default instead of being silently tolerated |
| Held stops for non-selected rovers (#35) | Transmit to every previously-stopped rover every tick ("fleet under command"); transmit only to the selected rover, same as today | One vehicle under command | Per-rover state means a deselected rover's latch persists in memory and reasserts itself the instant it's reselected, without the station needing to keep addressing vehicles it isn't displaying. Looping the drive timer over the whole roster is a materially larger claim about what this station does than the exercise asked for, and the issue itself steers away from it |
| Where the legacy session cache migration runs (#11) | Auto-run on every station startup, ahead of `Read`; a library method only exercised by tests; a separate console tool | Auto-run on startup | A site's history has to survive with zero manual step, and `LoadSessionHistory`'s existing try/catch already keeps the app green if migration fails. A separate console tool is one more moving part to keep green for something that only ever needs to run once per file |
| How to detect an already-migrated session cache (#11) | Add a version/marker to the format; treat a record's reserved slot already being zero as the signal; always reprocess with no detection at all | Reserved slot already zero | #10/#51 kept the on-disk layout byte-identical to the legacy one for compatibility, so the reserved slot's zero-ness is the only byte-level difference between "legacy" and "migrated" bytes - the one signal available without changing the wire format itself |
| Config wiring after `App.config` (#19) | Full `Microsoft.Extensions.Hosting` generic host + `IOptions<StationOptions>` injected via DI; a static facade backed by a POCO bound once at startup | Static facade, no DI container | Nothing else in this WPF app uses dependency injection - `MainWindow` and every service it owns are still plain `new`. Introducing a host container to satisfy one issue's config keys is a structural change with a much bigger blast radius than #19 asked for. `StationSettings.Configure(StationOptions.Load(...))`, called once from `App.xaml.cs.OnStartup`, keeps the same ~16 call sites working unchanged |
| Malformed vs. absent config values (#19) | Fail fast (throw, refuse to start); log and fall back to the documented default | Log and fall back | An absent key using its default is documented, existing behaviour and stays silent on purpose. A typo'd value (`TelemetryPort: "abc"`) is different - reverting to a default *unnoticed* is the exact failure mode the issue called out - so it logs an `Error` naming the bad key and value before falling back, rather than refusing to launch the station over one bad line in a file a site operator hand-edits |

---

## Tools Used

*What AI tools did you use, and how? Where did they help, and where did they get in your way or get it wrong?*

| Tool | How I used it |
|---|---|
| GitHub Copilot code review | Automatic review on the pull request. Earned its place a third time on #19: `ReadLogLevel` used `Enum.TryParse` without `Enum.IsDefined`, so a numeric-but-undefined value like `"LogLevel": 99` parsed successfully into a real `LogLevel` that happens not to be `Debug`/`Info`/`Warn`/`Error` - and because it compares greater than every real level, it silently disabled all logging rather than triggering the "invalid value" fallback the rest of the parsing already had. I reproduced it as a failing test first (asserted `LogLevel.Info` against `"99"`, watched it fail with `actual: 99`) before fixing it with an `Enum.IsDefined` guard. The same review also flagged that `AddJsonFile("appsettings.json", optional: false)` crashes the station if the file is missing or has invalid JSON, unlike the old App.config behaviour of treating a missing file as all-defaults; I did not act on this one - see Codex's row below, the same "filed rather than fixed" call for the same reason. Earned its place twice before that too: on the codec it caught a genuine integer-overflow hole in the CRC bounds check that my own tests had walked past, and a documentation/implementation mismatch on `TelemetryFrame`. On the emergency stop it caught an unguarded null dereference in both drive handlers, and an indicator that refreshed on telemetry but not on selection. Its file-by-file summaries are noise; the substantive findings have all been real. I reproduce each one before accepting it rather than taking the diagnosis on trust, which is also how I found that its "stale indicator" report was worse than described - not a brief lag, but permanent when the newly selected vehicle is silent |
| Codex code review | Also automatic on the pull request. Caught the same `Enum.IsDefined` gap independently on #19, on the same line Copilot flagged - two reviewers converging on one real bug is a stronger signal than either alone, and it's the first Codex finding I've fixed rather than filed. Raised one finding on the emergency stop I decided *not* to act on: with a single station-wide latch, stopping rover A and then selecting rover B stops B instead, and re-arming B clears A's latch. It is correct - I reproduced both halves against the simulator - but the fix is per-rover state, which the repository owner had explicitly deferred out of this issue, and binding the latch to its rover silently answers a design question (whether the station commands vehicles it is not showing) that belongs to the owner rather than to a reviewer or to me. Filed rather than fixed. Copilot's `appsettings.json`-missing-crashes point in the row above, on this same PR, is the same situation again even though it came from a different reviewer: a real, correct observation about a decision (fail fast vs. fall back to all-defaults on a missing/corrupt config file) that the plan already made deliberately and the owner already approved, so overturning it from an automated review comment rather than a conversation with the owner would be answering a safety-relevant question that isn't mine or a reviewer's to answer unilaterally. Worth recording twice now that the useful output of a review is not always a diff |
| Claude Code (Opus) | Planning and implementation, driven issue by issue. Most useful on the mechanical-but-fiddly work: enumerating the vendor assembly's real member list out of its metadata, and generating exhaustive test cases. I had to direct the verification explicitly - left to itself it would have stopped at a green test run rather than mutation-testing the suite and driving the real simulator over UDP. It also produced the stale-DLL false alarm described under Challenges, by rebuilding while a deliberate mutation was still applied |

---

## Documentation vs. Reality

*Dana said to trust the code over the documents. Did that turn out to matter? Where?*

Yes, and specifically on the telemetry SDK.

The vendor's own API documentation, `RoverLink.Telemetry.xml`, lists 9
`TelemetryFrame` properties. The assembly exposes 16 members, and the application
uses four of the undocumented ones. This is the sharpest example in the exercise
so far: the documentation was not vague or merely out of date, it was an
incomplete subset, and following it would have produced a codec that compiled and
then broke the build one project downstream. I only caught it because I read the
assembly's metadata rather than the file that describes it.

`docs/rover-link-protocol.md` also tells you that a 64-bit build "is available on
request - contact RoverLink support and quote the site licence number". That
instruction is dead: `docs/architecture.md`, in the same folder, records that
RoverLink stopped trading and the support address bounces. Two documents that
contradict each other on the single question that decides the whole migration.

The counterpoint is worth recording, though, because "trust the code" is not the
same as "ignore the documents". The protocol notes were accurate exactly where it
mattered - the frame layouts matched `FrameWriter.cs` byte for byte, and the CRC
worked example was correct. That handed me a check on the wire format that was
independent of both the simulator and my own implementation. The documents were
wrong about the API surface and about the vendor; they were right about the
protocol.

The emergency stop is the sharpest counter-example, and it cuts the other way
entirely. `docs/operations-guide.md` says the stop latches and the vehicle stays
stopped until re-armed. The code did not do that. Read on its own, the code was
perfectly self-consistent - the station sent one stop frame, the drive timer
carried on sending `emergencyStop: false`, and the vehicle obeyed the most recent
frame exactly as `docs/rover-link-protocol.md` says it would. Nothing in it looks
broken. You only know it is wrong because a document tells you what the behaviour
is supposed to be.

So "trust the code over the documents" is the right rule for working out what the
system *does*, and useless for working out what it is *supposed* to do. On the
telemetry SDK the documents were an incomplete description of a working thing. On
the emergency stop the document was the only surviving statement of intent, and
the code was the thing that was wrong. Applied literally, the rule would have had
me read the drive timer, see it faithfully implementing a level-triggered
protocol, and move on.

The compiler was also telling me, for what it is worth: CS0414,
`_emergencyStopLatched` assigned but never used. A warning nobody had turned into
an error, sitting on the one safety control in the application, for long enough
that it had become part of the scenery.

**Issue #26 itself is a data point here.** It was written assuming
`README.md`, `docs/architecture.md` and `docs/operations-guide.md` still
described .NET Framework 4.8, x86, and a working vendor SDK - the state
before #15/#16. By the time I actually opened these files for #26, most of
that was already gone: #15, #16, #19, #20, #22, #35, #36 and #37 had each
corrected the spots their own change touched, as this log's own "What I'd Do
With More Time" section predicted back at #19. `docs/operations-guide.md`'s
emergency-stop section in particular matched `DriveController.cs` and
`MainWindow.xaml.cs` exactly, including the per-vehicle latch from #35 - it
needed no changes at all. So the issue's own premise was itself out of date,
which is the same lesson as the telemetry SDK above in miniature: an issue
description is a document like any other, and it drifts the moment the code
moves past it.

What was actually left, once I checked rather than assumed, was narrower and
sharper than "still describes 4.8/x86": three places where a *header or
summary line* disagreed with the *body of the same document*.
`docs/rover-link-protocol.md` opened with "SDK version in use: 1.4.2" while
its own "Codec" section, a page down, correctly said the vendor DLL had
already been replaced (#5). `docs/architecture.md`'s History section still
stated the station "has been on .NET Framework 4.8 and 32-bit... since 2019"
as a present-tense fact, contradicted by its own "Third-party code" section a
few lines later, which already talks about net8.0. And `README.md`'s
configuration table still pointed at `App.config`, which #19 deleted - the
one genuinely dangerous item on this list, since it sends an operator to edit
a file the station no longer reads, rather than just reading oddly. `CLAUDE.md`
had the same disease: it still warned that `docs/operations-guide.md`
describes remote monitoring as live, a line #8 had already made false. In
every case the fix was small once found; finding them meant reading whole
documents end to end rather than trusting a stale, bug-report-shaped list of
what was supposedly still wrong.

---

## Challenges

*What was the hardest part? What took longer than expected?*

The genuinely hard part was resisting the temptation to treat "the tests pass" as
"the codec is correct". The mirror-based tests were always going to pass - they
compare my implementation against my own transcription of the same source. That
is why the golden frames and the mutation run exist.

What cost me the most time was self-inflicted, and worth recording. After the
mutation test I reverted the source but rebuilt the test project while the
mutations were still compiled, which republished a broken `RoverRally.Core.dll`
into `bin\Debug`. I then copied that stale binary next to my end-to-end harness,
and spent a while staring at "0 frames decoded, every frame malformed" as though
it were a codec bug. Both directions failing at once was the clue that it was
environmental rather than a decode error - a wrong decoder would still have let
commands through to the simulator. The fix was a clean rebuild; the lesson is
that a mutation test needs an explicit rebuild on the way back out.

Working out how to run the tests from the command line also took longer than it
should have. These are `packages.config` projects, so the MSTest adapter is never
copied to `bin\Debug`, and `vstest.console` then reports "No test is available"
rather than an error - which reads like a broken test project. It needs
`/TestAdapterPath` pointed at `packages\MSTest.TestAdapter.2.2.10\build\_common`.

---

## What I'd Do With More Time

*What's genuinely still open, not what's already landed.*

**The three on-screen checks I never got to run.** Each is recorded in its
own section above; I'm collecting them here because together they are the
real boundary of what this exercise proves. #30's lap timer has unit tests
on the crossing and direction logic, but I never watched a rover complete a
lap and saw the count and the elapsed time change on screen against a live
simulator. #31 publishes a genuine self-contained apphost and I launched it
with .NET stripped from `PATH`, but this machine has three SDKs installed
and I could not provision a box with no .NET footprint at all, which is the
literal claim the issue makes. #32's exporter is tested directly against the
real shipped `session-cache.bin`, but the button, the `SaveFileDialog` and
the message box in front of it were verified by reading the code, not by
clicking them and opening the result in actual Excel. None of the three is a
suspected defect. They are the places where this log deliberately stops
short of the word "proven", and I would rather they stay listed than be
quietly absorbed into a green test run.

~~**No coverage of the WPF layer at all.** The suite is 169 tests and deep
exactly where I went hunting for bugs - the codec, `DriveController`,
`Rover`, `GeofenceMonitor`, `LapTimer`, the CSV exporter - and entirely
absent above them. `RoverRally.Tests` references only `RoverRally.Core`, so
`MainWindow`, `TrackView` and `FleetView` have never been executed by a
test. Nothing structural blocks fixing that any more: both projects have
targeted `net8.0-windows` since #15, so the `ProjectReference` would simply
compile. What it needs is test infrastructure this suite has never required -
an STA thread and a dispatcher - and a decision about how much of a
code-behind layer is worth pinning down versus continuing to push logic out
of it into Core, which is what #20, #35 and #36 each did the moment they
needed something tested. That deserves to be a deliberate pass rather than
another side effect of fixing a bug.~~

**Landed in #73** - marked "extremely optional" in the issue I raised for
exactly this gap, and I did it anyway once I actually sat down with it. It
turned out to need neither an STA thread nor a dispatcher in the end: the
whole point of pushing the command layer into a constructor-injected
`StationViewModel` is that a test can construct one directly and call its
commands on the test's own thread, the same way any other object under test
works - no window, no message pump. See "Finishing the MVVM pattern so the
WPF layer becomes testable" under Extra Credit below.

**The fleet-wide command question #35 left open.** #35 deliberately answered
"one vehicle under command" rather than a transmit loop over the whole
roster, and said so as the owner's explicit call, not a default. If a real
site ever runs unattended vehicles that need their stop reasserted while off
screen, that decision needs revisiting - it's a materially bigger claim about
what this station does, not a follow-on bug fix.

---

## Extra Credit

### Nullable reference types across the solution (#28)

*Enable `<Nullable>enable</Nullable>` and get the solution clean under it.*

**What I found.** Going file by file rather than trusting the three examples
the issue names, the codebase turned out to be unusually disciplined about
null already: almost every place a value can genuinely be absent -
`_vm.SelectedRover` when nothing is selected, `StationSettings.ReadProfileValue`
when a registry value is missing, the `out` parameters on `FrameCodec.TryDecode`
and `DriveController.TryToggleArm` on their false path - was already checked
before use at every call site I could find. That meant this issue was
overwhelmingly an annotation exercise, not a bug hunt: flipping the switch on
each project one at a time (Core, then App, then Tests) and working through
whatever the compiler actually flagged surfaced 90-odd warnings across the
solution, and all but a handful resolved by just writing down the nullability
the surrounding code already assumed.

The handful that weren't mechanical: `StationOptions`'s five string properties
looked like a clean `required`-modifier case (populated only through one
object-initializer in `Load()`) until I found a second, narrower construction
site in `LogTests.cs` that only ever sets `LogLevel`/`LogDirectory` on purpose
- `required` would have forced that test to fill in properties it has no
reason to care about, so those five properties got `= string.Empty;` defaults
instead, and the test stayed untouched. And two tests
(`DriveControllerTests.DisarmsWithoutComplainingAboutTheThrottle`,
`.ReArmClearsAnAdoptedLatchWhenThrottleIsCentred`) stored `TryToggleArm`'s
`bool` return in a local before asserting on it, then dereferenced the `out`
command afterward - the compiler can't correlate a stored `bool` back to the
`out` parameter's `[NotNullWhen(true)]` state the way it can when the call is
inlined directly as the assertion's own argument, so those two calls moved
inline (same assertion, same message, no behavior change) rather than reaching
for a null-forgiving `!`.

**What I decided.** `Rover.Name`/`ChassisType`/`RadioSerial` became genuinely
nullable (`string?`), matching what `JsonSerializer.Deserialize` can actually
hand back from a rovers.json missing a field, propagated into
`FleetView.NameFor`'s one read site with the same `"Rover " + roverId` fallback
it already used for "no match at all." `MainWindow`'s four link/track fields
(`_telemetry`, `_commands`, `_driveTimer`, `_geofence`) are the one place I used
`null!` rather than a real annotation: all four are assigned by
`LoadTrack()`/`StartLink()`, both called from `MainWindow_Loaded` before any
handler that reads them can run, but not every read site defensively
null-checks them (`_geofence`'s one use in `ApplyFrame` doesn't), so forcing
`?` onto them would have meant inventing a null branch that can never actually
execute. One grouped comment above the four fields records the invariant that
makes `null!` safe there instead of scattering a justification per field.
`StationSettings._options` and `Log`'s three pipeline fields needed no
suppression at all - a nullable backing field plus the getter's existing
`throw` (`_options ?? throw new InvalidOperationException(...)`) keeps the
public `Options` property's return type honestly non-null. Nullable warnings
are promoted to build errors (`<WarningsAsErrors>Nullable</WarningsAsErrors>`
on all three solution `.csproj`s) so this clean state can't regress silently
under a future change.

**How I verified it.** `msbuild -t:Rebuild` on the full solution, both
Debug|x64 and Release|x64 - 0 warnings, 0 errors, in both configurations, with
`WarningsAsErrors` in place so a leftover nullable warning would have failed
the build rather than merely printed. `dotnet test` - 147 of 147 passed,
nothing skipped, nothing added or changed in what any test asserts. Then the
real station against the real simulator: roster loaded (5 rovers), session
cache read (8 runs, migration correctly refused since a `.legacy` backup
already existed from an earlier run), telemetry listener started on UDP
14550 and began decoding frames, battery warnings logged exactly where a 0 mV
reading is expected to log one - the same behaviour as before this issue,
just with the compiler now able to prove most of it.

---

### GitHub Actions workflow to build and test (#29)

*A workflow that builds the solution and runs the tests on push and pull
request.*

**What I found.** `code/RoverRally.sln` only declares `Debug|x64` and
`Release|x64` - no `AnyCPU` fallback - so a solution-level `dotnet build`
needs `-p:Platform=x64` passed explicitly, the same requirement CLAUDE.md
already documents for the local MSBuild.exe incantation. `RoverRally.Tests.csproj`
pins its own `PlatformTarget` and `OutputPath` directly, so `dotnet test`
against it needs no extra flags beyond `--configuration`. `RoverRally.Simulator`
is deliberately outside the `.sln` (plain `net8.0`), so it stays out of this
workflow's build entirely - it's not "the solution" the acceptance criteria
asks for, and CLAUDE.md is explicit that it's never to be touched. There was
no `.github/workflows/` directory and nothing in `docs/` referencing CI, so
this was a clean addition with nothing stale to reconcile.

**What I decided.** `windows-latest`, since WPF won't build on Linux runners.
`actions/setup-dotnet@v4` pins `8.0.x` explicitly rather than trusting
whatever SDK the runner image happens to ship, so the workflow doesn't
silently drift when GitHub updates the image. Three steps - restore, build,
test - run plain `dotnet` CLI commands against `code/RoverRally.sln` and
`code/src/RoverRally.Tests/RoverRally.Tests.csproj` respectively, matching
what #15's retarget to `net8.0-windows` made possible (no MSBuild.exe or
.NET Framework targeting pack needed, unlike before that issue). Triggers are
`push` to `main` plus `pull_request` on any target branch, rather than `push`
on every branch, so a PR gets exactly one run instead of two. Test reporting
stays minimal: `dotnet test`'s own non-zero exit code on a failing test
already fails the job and satisfies the acceptance criterion, so I didn't
reach for a third-party check-annotation action just because the issue
mentioned it as something to "consider" - that would have added a dependency
and a permission (`checks: write`) for a cosmetic improvement over what the
job's pass/fail state already shows. The build configuration is `Release`,
not the `Debug` the local dev commands use, since CI is meant to confirm the
shippable configuration actually builds and passes, not mirror a developer's
inner loop. A badge linking to the workflow went at the top of `README.md`,
directly under the title.

**How I verified it.** Ran the exact three commands the workflow uses -
`dotnet restore code/RoverRally.sln`, then `dotnet build code/RoverRally.sln
--no-restore --configuration Release -p:Platform=x64`, then `dotnet test
code/src/RoverRally.Tests/RoverRally.Tests.csproj --no-restore --configuration
Release -p:Platform=x64` - locally before pushing: the Release|x64 build came
back with 0 warnings and 0 errors, and all 147 of 147 tests passed, nothing
skipped. The actual GitHub-hosted `windows-latest` run is what the pull
request itself now demonstrates - watch the Actions tab on the PR for the
green check the acceptance criteria asks for.

---

### Lap timer on the Track tab (#30)

*One lap per crossing of the start line.*

**What I found.** None of the geometry already in the codebase actually
encoded a start line, despite the name of one skipped-then-fixed test
suggesting otherwise. `TrackProjectionTests.PlacesTheStartLine` (fixed in
#24) turned out to just project the geometric centre of the track's
bounding box - a sanity check on the projection math, not a line. The
"START" marker drawn in `TrackView.xaml` is a hand-placed `Rectangle` at
fixed canvas pixels (`Left=298, Top=103, Width=4, Height=34`), with no
lat/lon of its own. And `GeofenceMonitor` is point-in-polygon containment,
not segment-vs-segment intersection - its own doc comment explains it was
deliberately made to hold zero per-rover state after #4, which is the
opposite of what a lap counter needs.

I read `RoverSim.cs` (never modified, only read) to ground the direction
question rather than guess at it: the simulator drives every rover around
the same 720x480 canvas path drawn in `TrackView.xaml`, converting canvas
(x, y) to lat/lon with the exact inverse of `TrackProjection`'s own formula
and the same `TrackNorth/South/West/East` constants as `appsettings.json`.
On the top straight, where the drawn marker sits, a rover moves with
increasing x - west to east, increasing longitude - at an effectively
constant latitude. That's the racing direction through the line, and it
confirmed the marker's drawn position is genuinely on the loop the
simulator drives, not just decoration near it.

**What I decided.** Rather than add a new configuration value for the
line's real-world position, `TrackProjection` gained `Unproject`, the
algebraic inverse of its existing `TryProject`, so `MainWindow.LoadTrack()`
can turn the drawn marker's own pixel coordinates - exposed as named
constants on `TrackView` - back into the lat/lon line those pixels
represent. The line and the drawing stay in sync by construction instead of
by two people remembering to update two places.

New `Control/LapTimer` (one per rover, via a new `Control/LapTimerRegistry`
copying `DriveControllerRegistry`'s get-or-create shape exactly) detects a
crossing as a genuine segment-vs-segment intersection between two
consecutive fixes and the start line - the standard four-orientation
test - rather than proximity to the line or merely crossing the infinite
line through it. I checked this distinction actually mattered rather than
assuming it: a segment that crosses the start line's longitude at a
latitude well outside the line's own span (cutting the corner off the
track surface entirely) passes an infinite-line check but correctly fails
the finite one, and I have a test pinning that down
(`ACrossingOutsideTheLinesSpanIsNotCounted`). A crossing only counts when
its direction matches the line's fixed orientation (west to east, verified
against `RoverSim.cs` above), so a rover reversing through the line, or
oscillating without net progress, cannot inflate the count - directly
answering the issue's own warning about a rover parked on the line lapping
forever. The very first forward crossing a `LapTimer` ever sees only arms
its clock rather than completing a lap: a rover's position when the
station starts listening is arbitrary, so treating that moment as a lap
start is real, but treating it as a lap *finish* would report a bogus
first time with no meaningful start.

Lap count and last lap time reach the Track tab the same way `LinkState`
already does: two new plain `StationViewModel` properties (`LapCount`,
`LastLapDisplay`) that `MainWindow` computes and pushes in, rather than the
view model owning or knowing about `LapTimerRegistry` itself. I considered
imperative `TextBlock.Text` updates instead, matching how the drive-state
readout works, but the lap readout sits with the other passive telemetry
(Speed, Heading, Signal) in the panel, not with the drive controls, so
binding it the way its neighbours are bound kept that section internally
consistent.

**How I verified it.** `LapTimerTests` (9 cases) and `LapTimerRegistryTests`
(4 cases) cover the crossing/direction/arming logic and per-rover isolation
independently of the UI, using simple round-number fixtures the same way
`GeofenceMonitorTests` does, plus an interleaved-rovers test proving one
rover's crossings never touch another's count. `TrackProjectionTests`
gained 2 cases for `Unproject`, including a project-then-unproject
round trip. `dotnet test` - 162 of 162 passed, 0 skipped, up from 147 by
exactly the 15 new tests, nothing existing changed. `MSBuild.exe
-t:Rebuild -p:Configuration=Debug -p:Platform=x64` came back clean, which
matters here specifically because nullable reference types are
warnings-as-errors solution-wide (#28) and this issue touches a
struct-returning method (`Unproject`) and several nullable local variables.

I was not able to drive the WPF station visually in this environment to
eyeball the readout on screen against the running simulator, so that last,
most literal check - watch a rover complete a lap and see the count and
time actually update - is still owed as a manual pass before this is
called fully done, the same way #16 didn't stop at "the exe builds as x64"
and went on to drive the real UI against the real simulator.

Copilot's review on the pull request caught a real gap in the crossing
test: `CrossesForward`'s side checks are strict (`< 0`/`> 0`), so a fix
landing exactly on the line - `Cross(...) == 0` - resolves to neither side,
and the review pointed out this isn't the contrived edge case it might look
like, since the line's own lat/lon is derived from the same linear
projection as the rover's own telemetry, which is itself quantized to 1e-7
degrees. A `West -> OnLine -> East` sequence would silently lose that
crossing entirely under the old code, because the segment `OnLine -> East`
starts from an already-ambiguous point and can never satisfy a strict side
check regardless of where it ends. I reproduced it first - a test walking
exactly that three-fix sequence went red, the crossing never registering -
before changing anything. The fix follows the review's own suggested
shape: `LapTimer` now tracks the last fix that fell unambiguously on one
side, `_lastDefinitePosition`, rather than simply the last fix seen. A fix
that lands exactly on the line doesn't advance it, so it's held pending -
the segment being tested for a crossing implicitly spans across any number
of on-line fixes to whichever side the next unambiguous one resolves to,
rather than being evaluated fix-by-fix. `AFixExactlyOnTheLineDoesNotLoseTheCrossingOnceTheNextFixClearsIt`
pins down the fixed scenario, including a second full pass through the same
on-line point to confirm the pending state isn't left "used up" after the
first crossing; `RetreatingFromTheLineBackToTheSameSideIsNotACrossing`
confirms sitting on the line and then withdrawing to the side it came from
still doesn't register. `dotnet test` - 164 of 164 passed, 0 skipped, up
from 162 by the 2 new cases.

---

### Publish as a self-contained single file (#31)

*So the station can be copied to a laptop with nothing installed.*

**What I found.** Nothing in the repo mentioned `RuntimeIdentifier`,
`SelfContained`, or `PublishSingleFile` anywhere - a repo-wide grep came back
empty across every `.csproj`, workflow, and doc. More usefully, every path
the app actually touches was already anchored the right way for this to work
with no source changes at all: `App.xaml.cs` loads `appsettings.json` off
`AppDomain.CurrentDomain.BaseDirectory`, `MainWindow.xaml.cs` resolves
`RosterPath`/`SessionCachePath` the same way, and `StationOptions.ReadLogDirectory`
(#22) roots a relative `LogDirectory` there too - never
`Environment.CurrentDirectory`. .NET's own single-file docs say
`AppContext.BaseDirectory` for a published single-file app is the directory
containing the one exe, not a temp extraction folder, so the issue's own
open question - will the roster, config, and log paths survive a laptop
copy - was already answered by decisions earlier issues made for unrelated
reasons. `appsettings.json`, `Data\rovers.json`, and `Data\session-cache.bin`
are plain `<None Update>` items with `CopyToOutputDirectory=PreserveNewest`,
which `dotnet publish` copies to the publish folder exactly the way `dotnet
build` copies them to `bin\`, so keeping the roster external needed no
decision either - it already was. The one thing I didn't take on faith:
whether publishing `RoverRally.App.csproj` directly still needs
`-p:Platform=x64` the way `msbuild RoverRally.sln` does. It doesn't -
`PlatformTarget=x64` is already unconditioned in that csproj, the same fix
#13/#14 already applied to the Debug/Release property groups - and I
confirmed it by actually running the publish without the flag rather than
reasoning it through on paper.

**What I decided.** One new `PropertyGroup` in `RoverRally.App.csproj`,
conditioned on `'$(RuntimeIdentifier)' != ''`, setting `SelfContained`,
`PublishSingleFile`, and `IncludeNativeLibrariesForSelfExtract` to `true`.
The condition means the block only ever activates when someone actually
passes `-r win-x64` on a publish command line - `RuntimeIdentifier` is never
predeclared unconditionally in the project - so plain `dotnet build`/`dotnet
test`/`msbuild RoverRally.sln` stay completely untouched and the documented
`bin\Debug|Release\RoverRally.Station.exe` build path doesn't move.
`IncludeNativeLibrariesForSelfExtract` specifically because WPF still needs a
handful of native interop DLLs at startup (`PresentationNative_cor3.dll`,
`wpfgfx_cor3.dll`, `vcruntime140_cor3.dll`) even under `PublishSingleFile` -
without it they land as loose files next to the exe, which defeats "single
executable" in the way that actually matters to someone copying a folder.
`PublishTrimmed` stays unset on purpose - WPF doesn't reliably support
trimming, and this issue isn't the place to find out how unreliably. No
`-o`/`PublishDir` override: the SDK default, `bin\Release\win-x64\publish\`,
sits physically apart from the plain build's `bin\Release\`, so a publish and
a build can never clobber each other's output. Documented the command in
both `README.md` (a new Publish section) and `CLAUDE.md`'s Commands section,
rather than only one of the two, since the acceptance criteria name the
README specifically but future work on this repo reads CLAUDE.md first.

**How I verified it.** Rebuilt `Debug|x64` via the documented MSBuild
command and ran `dotnet test` first, before touching the publish path at all
- 164 of 164 passed, `bin\Debug\` unchanged - confirming the new property
group really is inert for every command that doesn't pass a RID. Then
`dotnet publish code/src/RoverRally.App/RoverRally.App.csproj -c Release -r
win-x64 --self-contained true`, no `-p:Platform=x64`, no warnings. The
output at `bin\Release\win-x64\publish\` is exactly `RoverRally.Station.exe`
(a genuine PE32+ apphost, ~155 MB), `RoverRally.Station.pdb` and
`RoverRally.Core.pdb`, `appsettings.json`, and `Data\` - no loose
`PresentationNative_cor3.dll`/`wpfgfx_cor3.dll`/`vcruntime140_cor3.dll`, no
loose `.deps.json`/`.runtimeconfig.json`. I copied that whole folder outside
the repo, started the real simulator, and launched the copied exe directly
- not `dotnet run`, and with `dotnet`/`Program Files\dotnet` stripped from
the launching shell's `PATH` - and it started clean: loaded the roster,
read the session cache (no legacy bytes left to migrate, same as any other
second run against this fixture), began decoding telemetry on UDP 14550, and
logged to `%LocalAppData%\RoverLink\Station\Logs\station-<date>.log` - not
inside the copied folder, confirming #22's log-path default survives a
laptop-style copy exactly as the issue asks. I then edited
`Data\rovers.json` in the copied folder, adding a sixth rover, and relaunched
- the log read `Loaded 6 rover(s) from the roster.`, confirming the roster
stays editable by site staff with no republish.

This machine already has three .NET SDKs and their matching runtimes
installed, so I could not provision a genuinely clean machine inside this
session to fully satisfy "runs on a machine with no .NET installed." The
PATH-stripped launch of a real apphost above is the closest practical
substitute I could run here, and I'm recording that as a known gap rather
than claiming the literal clean-machine check - true verification on
hardware or a clean VM with no .NET footprint at all is still owed before
this is called fully proven.

---

### Export run telemetry to CSV from the Fleet tab (#32)

*Let a marshal save what the Fleet tab shows to a file.*

**What I found.** The Fleet tab shows two genuinely different datasets, and
the issue itself flagged the ambiguity rather than picking one: the live
`FleetGrid` (current per-rover readings, redrawn on every telemetry tick,
nothing retained once redrawn) and the `HistoryGrid` (completed runs, read
from `session-cache.bin` via `SessionCacheRecord` - rover id, start/end UTC
ticks, total distance, peak speed; no per-lap or per-frame breakdown is
persisted anywhere, so "run telemetry" can't mean anything finer-grained
than that). Neither `SaveFileDialog` nor any other file-picker had ever been
used anywhere in this app, and there's no `ICommand`/`RelayCommand`
infrastructure at all - every existing control is a plain `Click` handler in
code-behind, which is what any new button here needed to match. I asked
rather than guessed which dataset to export, since the issue explicitly
called the choice out as open: completed run history, not the live
snapshot, because it's genuine retained data rather than "whatever happens
to be on screen right now," and it's what actually persists across a
restart.

**What I decided.** A new `RunHistoryCsvExporter` in `RoverRally.Core.Export`
holds all the formatting logic and takes a rover-name-resolving delegate
rather than a `Rover`/`StationViewModel` reference, specifically so it stays
directly testable from `RoverRally.Tests` with no WPF in the loop - the same
principle #20's `DriveController` extraction and #36's `Rover.ApplyFrame`
extraction already established for this codebase. Every number and
timestamp is formatted with `CultureInfo.InvariantCulture` explicitly at
each call site rather than by swapping the thread's culture globally, so the
output can never depend on the station's own locale - directly answering
the issue's own warning that a comma decimal separator would corrupt the
file for a customer visiting from a country that uses one. Timestamps are
ISO-8601 UTC (`yyyy-MM-ddTHH:mm:ssZ`); rover names are always double-quoted
and internally quote-escaped per RFC 4180, since they're the one free-text
field and could contain a comma or a quote. `FleetView.xaml` gained an
"Export Run History (CSV)" button next to "Recent runs," with a tooltip
stating plainly that it exports the completed-run history below, not the
live grid above - that's what "say so in the UI" meant in practice.
`FleetView.xaml.cs` wires it to a `SaveFileDialog` defaulting to the
Documents folder with a timestamped filename, writes the file as UTF-8 with
a BOM (without the BOM, Excel on Windows guesses ANSI and can mangle a
non-ASCII rover name - directly relevant given the international customer
visits the issue itself mentions), and shows a message box instead of
opening the dialog at all when there are no completed runs yet, rather than
producing a header-only file silently. File I/O errors (locked file, bad
path, no permission) are caught, logged through the existing `Log.Error`
facade, and reported in a message box rather than left to crash the
station.

**How I verified it.** Five new cases in `RunHistoryCsvExporterTests`: an
empty run list produces exactly the header line; a normal record formats
its numbers and ISO-8601 timestamps exactly as expected; a rover name
containing both a comma and a quote is quoted and escaped correctly; the
output stays invariant even when the running thread's culture is forced to
`de-DE`, a comma-decimal locale, which is the direct regression test for the
issue's own stated concern; and a record with out-of-range
(`long.MaxValue`) ticks - which `SessionCacheRecord` already clamps to
`DateTime.MinValue` - formats without throwing, covering "export of an
absent run" at the data layer. `dotnet test` - 169 of 169 passed, 0 skipped,
up from 164 by exactly the 5 new cases, nothing existing changed.
`MSBuild.exe -t:Rebuild -p:Configuration=Debug -p:Platform=x64` came back
clean. I didn't stop at the unit tests: I wrote a throwaway console harness
referencing `RoverRally.Core` directly and ran `RunHistoryCsvExporter` against
the real, shipped `Data/session-cache.bin` (the same 8 real runs #10/#11/#16
have been verifying against all along), then read the resulting file back -
correct header, correct RFC 4180 quoting, correct invariant decimal points
and ISO-8601 timestamps, for real production data rather than hand-built
fixtures.

I was not able to drive the actual `SaveFileDialog`/message-box UI on screen
in this environment: the available computer-use tooling can only target
applications already registered in the Start Menu, and this is an ad-hoc
local build with no such registration, so it couldn't be brought under that
tool's control the way #16's UI pass could. The button wiring itself is
therefore verified by code review and the passing build/tests rather than
an on-screen click-through - the same category of gap #30 recorded for its
own UI readout and #31 recorded for its clean-machine check, not a claim
that this is fully proven end to end. A manual pass - click the button,
confirm the dialog opens in Documents with a sensible name, open the result
in actual Excel - is still owed before I'd call this issue completely done.

---

### Tag-triggered release workflow that publishes the station (#74)

*Extremely optional - closes the gap between #31's publish command and an
actual, reproducible, versioned artifact.*

**What I found.** `.github/workflows/build-and-test.yml` (#29) was, and
still is, the only workflow, and it builds nothing installable - it exists
to prove the tree compiles and the suite passes, not to produce a release.
#31's publish command is real and already verified, but running it depends
on whoever's machine it's typed on and whatever their working tree happens
to contain at the time; there is no way to answer "which commit is this
build from" for anything produced that way. A `v1.0` tag already exists on
this repo, pointing at the current `HEAD`, but with no GitHub Release
attached to it anywhere - so this is the first release-*workflow*, not the
first tag, and pushing this change doesn't retroactively trigger anything
against that existing tag.

**What I decided.** Two jobs, not one. `build-and-test` reuses #29's job
shape verbatim - same `windows-latest` runner, same
`actions/setup-dotnet@v4` pinned to `8.0.x`, same three commands including
`-p:Platform=x64` for the `.sln` - and carries no elevated permissions at
all, inheriting the workflow-level `contents: read`. `release` declares
`needs: build-and-test`, so it never starts unless that job's restore,
build, and test all succeeded - which is what "a red tree cannot produce a
release" actually needs, enforced by the job graph rather than by hoping a
single job's steps happen to stop in the right order. Only `release`
carries `permissions: contents: write`, scoped to exactly the one job that
calls `gh release create` - checkout/build/test never run with write access
to the repository, which is stricter than my first pass at this (one job,
write permissions on the whole thing) and was Copilot's own review comment
on the PR, not something I'd considered before it was pointed out. The
publish step is #31's exact command against `RoverRally.App.csproj`
directly, not the `.sln` - no `-p:Platform=x64`, per CLAUDE.md's note that
`PlatformTarget=x64` is already unconditioned there - and deliberately
keeps its own implicit restore rather than reusing `--no-restore`, since a
`win-x64` publish needs its own runtime-package restore that the
solution-level restore step in the other job doesn't cover. Packaged the
whole publish folder into one zip with PowerShell's built-in
`Compress-Archive` - no third-party action needed for that - rather than
attaching the `.exe` and `Data\` as separate loose assets, since a GitHub
Release can only attach files, not raw folders, and `Data\rovers.json`
needs to stay inside a real `Data\` subfolder next to the exe for
`appsettings.json`'s `RosterPath` to resolve after someone unzips it -
exactly #31's own "copy the whole folder" framing, just zipped for
distribution instead of copied by hand. `gh release create` (GitHub CLI,
preinstalled on hosted runners) creates the Release rather than a
third-party marketplace action, matching #29's own stated preference not to
add a dependency for something the built-in tooling already covers. The
Release is created as a `--draft` rather than published live, so a
versioned public artifact gets a human glance before anyone outside this
repo can see it.

**How I verified it.** Ran the workflow's own commands locally, in the same
order and with the same flags: `dotnet restore code/RoverRally.sln`, then
`dotnet build code/RoverRally.sln --no-restore --configuration Release
-p:Platform=x64` (0 warnings, 0 errors), then `dotnet test
code/src/RoverRally.Tests/RoverRally.Tests.csproj --no-restore --no-build
--configuration Release -p:Platform=x64` - 169 of 169 passed, 0 skipped,
unchanged from #32. Then `dotnet publish
code/src/RoverRally.App/RoverRally.App.csproj -c Release -r win-x64
--self-contained true` - output at
`code/src/RoverRally.App/bin/Release/win-x64/publish/` is exactly
`RoverRally.Station.exe` (~155 MB), `RoverRally.Station.pdb`,
`RoverRally.Core.pdb`, `appsettings.json`, and `Data\` (`rovers.json`,
`session-cache.bin`) - the identical shape #31 already verified, nothing
different. I then ran the zip step's own `Compress-Archive` command against
that real publish folder and inspected the resulting archive's entries
directly rather than assuming the folder structure survives: `Data/rovers.json`
and `Data/session-cache.bin` both come back nested under `Data/`, not
flattened alongside the exe, confirming the roster path stays resolvable
after someone unzips this on a target machine.

I have not pushed a real tag to watch this workflow run on GitHub for real;
that is still owed, and needs a genuinely new tag (`v1.0` already exists
with no Release attached, so reusing it wouldn't exercise anything). That
was a deliberate choice - pushing a tag creates public, hard-to-fully-undo
CI state, so I asked before doing it rather than assuming it was fine, and
it's still pending at the time of writing.

Copilot's review on the pull request caught two real gaps in the first
version of this workflow, both now fixed. First, the one already folded
into "What I decided" above: `contents: write` sat on the single job's
entire step list, not just the release step, which is more write access
than checkout/build/test ever need - fixed by splitting into the two jobs
described there. Second, the `gh release create` step used PowerShell
backtick line continuations without an explicit `shell: pwsh`, which works
today only because `pwsh` happens to be this workflow's default shell on
`windows-latest` - a real fragility, since changing the default shell later
(a repo-wide `defaults.run.shell`, or moving the job to a different runner)
would silently break that step's syntax with no obvious error pointing back
here. Both steps that use PowerShell-specific syntax (`Compress-Archive`,
the backtick-continued `gh release create`) now declare `shell: pwsh`
explicitly rather than relying on the runner's current default.

---

### Finishing the MVVM pattern so the WPF layer becomes testable (#73)

*Extremely optional, by the issue's own words - recorded so the reasoning
isn't lost, not because it was expected.*

**What I found.** The gap was exactly what I'd already written down under
"What I'd Do With More Time": `StationViewModel` was a read-model only, not
one `ICommand` implementation existed anywhere in `RoverRally.App`, and
every behaviour entry point - ARM, EMERGENCY STOP, the speed-unit radio
buttons, the fleet filter, the CSV export - was a `Click`/`Checked`/
`TextChanged` handler in code-behind, unreachable from
`RoverRally.Tests` for the structural reason I'd already named: it had no
`ProjectReference` to `RoverRally.App` at all. `MainWindow.xaml.cs` was 461
lines against the view model's 110, and directly owned `TelemetryClient`,
`CommandSender`, the drive `DispatcherTimer`, `GeofenceMonitor`,
`DriveControllerRegistry`, and `LapTimerRegistry`.

**What I decided.** I went through this in three passes inside the same
issue, each one a real correction from the previous, not a smooth plan
executed in order - worth recording honestly rather than tidying into
"decided X, did X."

*Pass one* added a `Services/IStationService` (the four link-layer fields
above, previously on `MainWindow`) that the view model took by constructor
injection, plus hand-wired `RelayCommand`s from `CommunityToolkit.Mvvm` for
arm/e-stop/speed-unit - but left `MainWindow.xaml.cs`'s `Click` handlers in
place, calling through to the new commands, so the app stayed green
mid-refactor. I reported this as done.

*Pass two* was a direct correction: View and ViewModel must not reference
each other at all, and View code-behind must be completely empty - not
"thin," empty. That's structurally more than "add commands to the existing
view model" - it meant a composition root, and a way for a `Window` to get
its content without either side naming the other's type. I chose
`Microsoft.Extensions.DependencyInjection` for the container and a
`DataTemplate` (registered in `App.xaml` against `StationViewModel`'s type)
for the View/ViewModel association, over the alternatives I raised and had
confirmed before touching code: a `ViewModelLocator` resource, or the
composition root setting `DataContext` directly on a View it already
constructed. The `DataTemplate` is the one that makes the "don't know about
each other" property actually load-bearing rather than aspirational -
`MainWindow.xaml.cs` never constructs or even names `StationViewModel`, and
neither does the new `StationView` (which absorbed the old `MainWindow.xaml`'s
actual content) name it back. `TrackView` was the one deliberate exception
I asked about rather than assumed: canvas drawing is inherently imperative
in WPF, so it stayed a reactive custom control - `TrackNorth/South/West/East`,
`Geofence`, `Rovers` as Dependency Properties, redrawing itself from bound
rovers' own `PropertyChanged` - rather than gaining a view model with
nothing to do but shuttle values to a canvas. `Views/MapMetrics.cs` (new)
holds the one thing both `StationViewModel` and `TrackView` still needed to
agree on - the canvas's pixel geometry, for the start-line projection - as
a small neutral file neither references the other to reach.

*Pass three* came from a direct question, not a request for more scope: why
didn't `StationViewModel` inherit from `CommunityToolkit.Mvvm`'s own
`ObservableObject`, given it already depended on the toolkit for
`RelayCommand`? There was no good answer - it was an inconsistency, not a
choice. I checked the `dotnet-wpf-modern` skill's own reference material
before touching anything, since the two CommunityToolkit property syntaxes
(C# 12 annotated-field vs. C# 13 partial-property) are not interchangeable
and this project floats to C# 12 on `net8.0-windows` with `LangVersion`
left unpinned since #15 - picking the wrong one either doesn't compile or
doesn't generate. `StationViewModel` became `partial class StationViewModel
: ObservableObject`, and I asked one more question before converting
anything: several properties are `private set` today specifically because
only the view model should ever write them, and the C# 12 syntax can only
generate public setters. Rather than accept that as a blanket trade-off,
I split the properties by their *existing* `set`/`private set` today -
public ones became `[ObservableProperty]`-annotated fields, private ones
stayed hand-written but shrank to a single inherited `SetProperty(ref
field, value)` call - preserving the exact encapsulation that was already
there. All six commands became `[RelayCommand]`-attributed methods; the
generated command *property* names match the ones they replace exactly, so
nothing downstream - XAML or tests - needed to change.

Writing the actual tests (the whole point of the issue) caught something
none of the three passes above had: `ExecuteEmergencyStopCommand` and
`ExecuteExportHistoryCommand` called `MessageBox.Show`/`SaveFileDialog`
directly. A real Windows modal dialog blocks until a human clicks it -
which means a test that called either command's success path would not
fail, it would *hang*, indefinitely, taking the whole test run down with
it. I caught this before it shipped a hung CI run, not after: extracted
`Services/IDialogService` and gave `StationViewModel` a third
constructor-injected dependency, letting a test supply a fake that answers
immediately. It's exactly the "manager, taken by constructor injection of
its contract" shape the issue itself asked for, so fixing it didn't expand
scope - it completed the pattern the issue was already asking for, one
dependency I'd missed the first time through.

**How I verified it.** `dotnet build` on the full solution - 0 warnings, 0
errors - after every one of the three passes, not just at the end; each
pass had to leave the app green before I moved to the next, per the
project's own rule. `dotnet test` - 178 of 178 passed (169 pre-existing
plus 9 new `StationViewModelTests`), with **zero changes needed to the test
file** across the base-class/generator conversion in pass three - the
generated property and command names are identical to the hand-written
ones they replaced, which is the whole reason that conversion was safe to
do after the tests already existed rather than before. `RoverRally.Tests.csproj`
needed one new thing beyond the `ProjectReference` the issue itself
predicted: `<UseWPF>true</UseWPF>`, since `StationViewModel.RoversView` is
typed `ICollectionView` (from `PresentationFramework`), which a bare
`ProjectReference` to `RoverRally.App` doesn't pull in transitively - I
found this the same way I find most build-config gaps in this repo, by
letting the compiler tell me rather than guessing.

Then the real station against the real simulator, run after each of the
three passes: roster loaded, session history loaded, telemetry listener
started, frames decoding and driving the Track tab, ARM/EMERGENCY STOP
working through the DI-resolved, DataTemplate-rendered, zero-code-behind
UI exactly as they did before any of this started - which is the actual
point of the issue. It was never about changing what the station does.

---

## Additional Notes

This is the closing entry, written when the tracking epic (#33) was the last
issue left open on the board.

**Where the solution ended up.** All three projects are SDK-style and target
`net8.0-windows`, building and running as x64 with no AnyCPU configuration
left anywhere. The vendor's 32-bit `RoverLink.Telemetry.dll` is gone,
replaced by a managed codec in `RoverRally.Core.Telemetry` that is checked
three independent ways - against a mirror of the simulator's writer, against
golden byte arrays generated by running the real simulator code, and against
the protocol notes' own worked CRC example. `Newtonsoft.Json` 6.0.8 and its
advisory are gone, as are AppDomain, .NET Remoting and `BinaryFormatter`.
Site configuration lives in `appsettings.json`, and logs roll to disk with
bounded retention. The three bugs Dana reported are fixed with a regression
test each, and six more that nobody reported are fixed alongside them.

**What is green, and how I know.** `MSBuild -t:Rebuild -p:Configuration=Debug
-p:Platform=x64` completes with no warnings - a real signal here rather than
a formality, because #28 made nullable warnings build errors solution-wide,
so "no warnings" and "it compiles" are the same statement now. `dotnet test`
reports 169 passed, 0 failed, 0 skipped. The baseline this started from was
4 tests: 3 passed, 1 skipped. Since #29 that same build and test run on
`windows-latest` in `Release|x64` on every push to `main` and every pull
request, so none of this rests on my machine alone.

**What this log does not claim.** The three items under "What I'd Do With
More Time" above are the honest edge of it - the lap readout on screen, the
CSV export dialog, and the launch on a machine with no .NET at all were each
verified as far as this environment allowed and no further. Where I could not
run a check, I said so in the section belonging to the issue that raised it
rather than rounding up to "done". The same goes for the review findings I
filed instead of fixing: each is recorded with its reason, because "I decided
not to, and here is why" is a different statement from "I didn't notice".

**Time Tracking, at the top of this document, is a reconstruction, not the
live log I said I'd keep.** I didn't keep a running per-phase stopwatch as I
went, so filling it in "for real" at submission time would have meant typing
in numbers I no longer had. What's there instead is derived from two real,
timestamped sources rather than guessed:

- `Getting the existing application built and running` and `Reading the code`
  predate any GitHub issue, so they're bounded by the three commits that
  received and prepped the repo and the moment I started filing issues
  (`2026-08-31 23:03`) - the gap between the last setup commit and the first
  issue is the closest thing to a timestamped "done reading, started
  planning" marker this history has.
- Every phase after that is built from each issue's pull request: its
  `created_at` to `merged_at` window, plus the gap back to the previous PR's
  `merged_at` when that gap is under 200 minutes, since I work one issue at a
  time and a short gap after merging one is normally me starting the next,
  not a break. Eight gaps came out longer than that (all overnight, or a
  multi-hour stretch between sessions) and are excluded rather than counted
  as work. `Extra credit / follow-on work` has no row in the original
  template; I added one rather than folding six real extra-credit issues
  (#28-#32, #74) into `Cleanup`, which would have hidden real time under the
  wrong label.

This is still a floor, not the real total, for exactly the reason I originally
gave for not reconstructing it this way: neither a commit timestamp nor a PR
timestamp sees the reading, false starts, or verification that never produced
one. The Start/End columns are more trustworthy than Duration for that reason
- a Duration understates real effort most on the issues that were quick to
implement but took real thought to get right (the geofence and no-fix-frame
bugs among them).
