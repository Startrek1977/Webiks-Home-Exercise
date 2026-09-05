# RoverRally Station — Architecture Notes

**Last substantially updated:** June 2020 (ports and framework notes touched up
2022; telemetry codec, emergency stop latch, per-rover drive state, no-fix
frame handling, the legacy session cache migration, the roster JSON library
swap, enabling nullable reference types, and the lap timer 2026)

---

## History

The station started in 2016 as a one-window utility for watching a single
vehicle. The fleet grid arrived in 2017, the drive controls in 2018, the session
cache in 2019, and the imperial readout in 2019 for the visiting customer
trials. It was on .NET Framework 4.8 and 32-bit from the 4.8 retarget in 2019
until the 2026 migration retargeted it to `net8.0-windows` (#15) and flipped
the platform to x64 (#16).

Nobody has owned it full time since 2021.

---

## Projects

| Project | What it is |
|---|---|
| `RoverRally.App` | The WPF station itself. Windows, views, drive controls. |
| `RoverRally.Core` | Shared library: models, the telemetry link, drive-control state, unit conversion, geometry, configuration, session cache. |
| `RoverRally.Tests` | MSTest project. Thin — it was started and not kept up. |
| `RoverRally.Simulator` | Bench simulator. Written later and on its own, so it is already on modern .NET and is not part of the station solution. |

All of the business logic lives in `RoverRally.Core`. The views are meant to be
presentation only, so that the logic can be exercised without a window open. This
is an aspiration the window has not always met — see the rough edges below.

---

## Third-party code

| Component | Version | Notes |
|---|---|---|
| `System.Configuration.ConfigurationManager` | 8.0.1 | `ConfigurationManager.AppSettings` (`StationSettings.cs`) isn't part of the net8.0 base class library the way it was on net48, so it needs an explicit package reference. |
| `MSTest.TestFramework` | 4.4.0 | Test project only. |

RoverLink Systems stopped trading and the support address bounces, so the
32-bit-only `RoverLink.Telemetry.dll` has been replaced by a managed
reimplementation in `RoverRally.Core.Telemetry`. We own the RL-100 codec now;
`docs/rover-link-protocol.md` is the specification it is written against.

`Newtonsoft.Json` 6.0.8, which read the roster file and had been pinned since
2016 with a known high-severity CVE, was replaced in 2026 (#18) with
`System.Text.Json`, which ships in the `net8.0` shared framework rather than
needing its own package reference. `System.Configuration.ConfigurationManager`
above is the one third-party runtime package still in the tree.

---

## Data the station keeps

| What | Where | Format |
|---|---|---|
| Fleet roster | `Data\rovers.json` | JSON, edited by hand by the site lead |
| Completed runs | `Data\session-cache.bin` | Fixed stride binary records |
| Site configuration | `appsettings.json` | JSON, `"Station"` section |
| Operator preferences | Registry, `HKCU\Software\RoverLink\Station` | One subkey per operator |

The session cache is a flat binary file rather than a database because the
station has to work on a laptop in a tent with nothing installed on it. The
record is a fixed 32-byte, fixed-offset layout (`SessionCacheFile.RecordSize`)
that is the same on x86 and x64 - it no longer depends on marshalling a struct,
which is what let a vendor SDK pointer field silently change the on-disk size
by platform (see "Moving to 64-bit" in `SOLUTION_LOG.md`).

On startup the station also migrates any file that still carries real,
non-zero legacy session-handle bytes in that reserved gap: `SessionCacheMigrator`
backs the original up to a `.legacy` sibling first, then zeroes the gap in
place (see "Migrating the legacy session cache" in `SOLUTION_LOG.md`).

---

## Threading

The telemetry listener runs on its own background thread and raises an event per
decoded frame. The window marshals those onto the dispatcher before touching
anything on screen. The drive command timer runs on the dispatcher.

---

## The emergency stop latch

Worth stating plainly, because the arrangement is not obvious from either side on
its own. A vehicle acts only on the frame it most recently received and keeps no
memory of an earlier one, so an emergency stop is not something the vehicle
holds — it is something the station has to keep saying. `Control/DriveController`
owns that state and every drive-timer tick asks it what to send; the stop is
re-asserted on each frame until an operator re-arms.

That makes the latch a property of the station, not of the vehicle, and it means
the drive timer going quiet is not the same as the vehicle being stopped. The
vehicle's own two-second command-loss failsafe is a second, independent net, not
the mechanism.

The station now keeps one such latch per vehicle, rather than one for the whole
fleet. Until 2026 a single `DriveController` was shared across the whole fleet,
addressing whichever vehicle happened to be selected — selecting a different one
in the Fleet tab could silently clear a stop, or hold one it never issued (#37).
`Control/DriveControllerRegistry` now hands the station back the same
`DriveController` instance for the same rover every time, so each vehicle has
its own station-side latch: it persists in memory regardless of selection and
reasserts itself the moment that vehicle is reselected, without the station
needing to keep addressing vehicles it is not displaying.

---

## The lap timer

Added in 2026 (#30). Neither `TrackProjection` nor `GeofenceMonitor` encoded a
start line before this — the "start/finish" marker drawn on the Track tab was
pixel art with no real-world position of its own, and `GeofenceMonitor` does
point-in-polygon containment, not segment-vs-segment crossing.

`TrackProjection` gained `Unproject`, the inverse of its existing lat/lon-to-
canvas mapping, so `TrackView` can turn the drawn marker's own pixel
coordinates back into the real-world line those pixels represent, rather than
the line's position being configured separately from what is actually drawn.

`Control/LapTimer` (one per rover, via `Control/LapTimerRegistry` — the same
get-or-create-and-remember shape as `DriveControllerRegistry`) watches the
segment between two consecutive fixes for a genuine crossing of that line,
not proximity to it: telemetry arrives at about 5 Hz, so a vehicle can cover a
real distance between fixes, and the crossing can fall anywhere along that
gap. Only a crossing in the racing direction counts, so a rover reversing or
oscillating across the line cannot inflate the count. The first such crossing
only starts the clock rather than completing a lap — a rover's position when
the station starts listening is arbitrary, so there is no genuine prior lap to
report yet.

`MainWindow` feeds it from the same per-frame, per-rover block that already
runs the geofence check, and pushes the selected rover's count and last lap
time into `StationViewModel` the same way it already does for `LinkState`.

---

## Known rough edges

- Configuration is spread across the config file and the registry, and it is not
  obvious from the code which value comes from where.
- The window does more than a window should. Pulling the drive logic out had
  been on the list since 2020; the armed and emergency-stop state came out in
  #20 and now lives in `Core/Control/DriveController`, one instance per
  vehicle since #35 via `Core/Control/DriveControllerRegistry`, which is what
  made it testable. Applying a decoded frame's fields to the rover model came
  out in #36 the same way, into `Core/Models/Rover.ApplyFrame`. Geofence
  alerting and the link lifecycle are still in the code-behind.
