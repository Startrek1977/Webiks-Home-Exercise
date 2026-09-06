# RoverRally Station

[![Build and Test](https://github.com/Startrek1977/Webiks-Home-Exercise/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Startrek1977/Webiks-Home-Exercise/actions/workflows/build-and-test.yml)
![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Windows x64 only](https://img.shields.io/badge/platform-Windows%20x64-blue)
[![Latest Release](https://img.shields.io/github/release/Startrek1977/Webiks-Home-Exercise)](https://github.com/Startrek1977/Webiks-Home-Exercise/releases/latest)

Ground control station for the rover fleet at Kadima Proving Ground.

```
code/
├── RoverRally.sln                  Station solution (.NET 8, x64)
├── src/
│   ├── RoverRally.App/             WPF station: track map, readouts, drive controls
│   ├── RoverRally.Core/            Models, RL-100 codec and link, drive-control state, units, geometry, config, session cache
│   └── RoverRally.Tests/           MSTest project
└── simulator/
    └── RoverRally.Simulator/       Bench simulator (.NET 8, separate from the solution)
```

## Building

```bash
msbuild RoverRally.sln -t:restore
msbuild RoverRally.sln
```

Requires the .NET 8 SDK and MSBuild 17.x, i.e. Visual Studio 2022 (or the
Build Tools for Visual Studio 2022) — `msbuild` on its own is not part of the
.NET 8 SDK, that's what supplies the CLI the commands above use. Visual
Studio 2022 handles the restore step on its own when you open the solution.
Windows-only as of #15: `net8.0-windows` is required for WPF and for the
registry-backed operator profile, so this no longer builds or runs on
Linux/macOS the way a plain `net8.0` project would.

The station builds to `src/RoverRally.App/bin/Debug/RoverRally.Station.exe`.

## Tests

```bash
dotnet test src/RoverRally.Tests/RoverRally.Tests.csproj
```

Add `--filter "FullyQualifiedName~Crc8Tests"` to run one class, or
`--filter "FullyQualifiedName~Crc8Tests.SomeTestMethod"` for a single test.

No test in this suite is skipped. Anything red or skipped is a regression.

## Running

Start the simulator first, otherwise the station has nothing to show:

```bash
cd simulator/RoverRally.Simulator
dotnet run
```

Then start the station. Vehicles appear on the Track tab within a few seconds.

### Simulator options

| Option | Default |
|---|---|
| `--telemetry-port` | 14550 |
| `--command-port` | 14551 |
| `--host` | 127.0.0.1 |
| `--rate` | 5 (Hz) |

The station's own ports are in `src/RoverRally.App/appsettings.json`; change
both sides together if you move them.

## Publish

For a station laptop with nothing installed — no .NET runtime, no SDK —
publish a self-contained single-file build:

```bash
dotnet publish src/RoverRally.App/RoverRally.App.csproj -c Release -r win-x64 --self-contained true
```

This produces `src/RoverRally.App/bin/Release/win-x64/publish/RoverRally.Station.exe`
(a genuine standalone apphost, ~155 MB) alongside `appsettings.json`, `Data\`,
and two `.pdb` files kept for post-incident debugging — copy that whole
folder to the target machine and run the exe directly. `appsettings.json`
and `Data\rovers.json` remain plain, editable files after publishing (see
Configuration below); operators can still edit the roster on the station
laptop without republishing anything, and the log still lands under
`%LocalAppData%\RoverLink\Station\Logs` regardless of where the folder is
copied to.

Trimming is intentionally not used: WPF does not support it reliably.

## Configuration

| What | Where |
|---|---|
| Ports, track extent, file paths | `src/RoverRally.App/appsettings.json` |
| Fleet roster | `src/RoverRally.App/Data/rovers.json` |
| Completed runs | `src/RoverRally.App/Data/session-cache.bin` |
| Operator preferences | Registry, `HKCU\Software\RoverLink\Station` |

## Telemetry

The RL-100 wire codec lives in `src/RoverRally.Core/Telemetry`. It used to be a
vendor binary, `lib/RoverLink.Telemetry.dll`, which was a 32-bit-only build from
a company that has since stopped trading. It has been reimplemented in managed
code against `docs/rover-link-protocol.md` and the simulator's `FrameWriter`,
with the public signatures unchanged, and the binary removed.

## Documentation

See `docs/` — the RL-100 protocol notes, the operations guide, and the
architecture notes. They have not all kept up with the code.
