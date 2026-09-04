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
msbuild RoverRally.sln -t:restore
msbuild RoverRally.sln
```

Requires the .NET 8 SDK (as of #15). Visual Studio 2022 handles the restore
step on its own when you open the solution.

The station builds to `src/RoverRally.App/bin/Debug/RoverRally.Station.exe`.

## Tests

```bash
dotnet test src/RoverRally.Tests/RoverRally.Tests.csproj
```

Add `--filter "FullyQualifiedName~Crc8Tests"` to run one class, or
`--filter "FullyQualifiedName~Crc8Tests.SomeTestMethod"` for a single test.

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
