# RoverRally Station

Ground control station for the rover fleet at Kadima Proving Ground.

```
code/
├── RoverRally.sln                  Station solution (.NET Framework 4.8, x86)
├── src/
│   ├── RoverRally.App/             WPF station: track map, readouts, drive controls
│   ├── RoverRally.Core/            Models, RL-100 codec and link, drive-control state, units, geometry, config, session cache
│   └── RoverRally.Tests/           MSTest project
└── simulator/
    └── RoverRally.Simulator/       Bench simulator (.NET 8, separate from the solution)
```

## Building

```bash
msbuild RoverRally.sln -t:restore -p:RestorePackagesConfig=true
msbuild RoverRally.sln
```

Requires the .NET Framework 4.8 targeting pack. Visual Studio 2022 handles the
restore step on its own when you open the solution.

The station builds to `src/RoverRally.App/bin/Debug/RoverRally.Station.exe`.

## Tests

Run from a Developer PowerShell, where `vstest.console.exe` is on the path:

```powershell
vstest.console.exe src\RoverRally.Tests\bin\Debug\RoverRally.Tests.dll /Platform:x86 /Framework:.NETFramework,Version=v4.8 /TestAdapterPath:packages\MSTest.TestAdapter.2.2.10\build\_common
```

`/TestAdapterPath` is not optional. This is a `packages.config` project, so the
MSTest adapter is never copied next to the test assembly, and without it
`vstest.console` reports "No test is available" rather than an error — which
looks like a broken test project. The adapter lives in `build\_common`, not
`build\net46`.

Add `/Tests:SomeTestMethod` to run a single test, or
`/TestCaseFilter:"FullyQualifiedName~Crc8Tests"` for one class.

`TrackProjectionTests.PlacesTheStartLine` is skipped and always has been.
Anything else red or skipped is a regression.

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

The station's own ports are in `src/RoverRally.App/App.config`; change both sides
together if you move them.

## Configuration

| What | Where |
|---|---|
| Ports, track extent, file paths | `src/RoverRally.App/App.config` |
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
