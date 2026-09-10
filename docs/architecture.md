# RoverRally Station — Architecture Notes

**Last substantially updated:** June 2020 (ports and framework notes touched up
2022; telemetry codec, emergency stop latch, per-rover drive state, no-fix
frame handling, the legacy session cache migration, the roster JSON library
swap, enabling nullable reference types, the lap timer, the Fleet tab's
CSV export, finishing the MVVM pattern for testability, and the Track tab
trail-continuity fix 2026)

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

All of the *domain* logic (the codec, drive-control state, geometry, unit
conversion, session cache) lives in `RoverRally.Core`. `RoverRally.App`'s
views are meant to be presentation only, with app-level orchestration
(loading the roster, wiring the telemetry link, the commands behind ARM/
EMERGENCY STOP) living in `StationViewModel` instead — reachable from a test
without a window open, as of #73 (see the rough edges below), even though
that orchestration code itself lives in `App`, not `Core`.

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

~~`MainWindow` feeds it...~~ **As of #73,** `StationViewModel` itself feeds
it from the same per-frame, per-rover block that already runs the geofence
check, setting its own `LapCount`/`LastLapDisplay` properties the same way
it already does for `LinkState` — there is no separate window to push the
values into anymore.

---

## Exporting run history to CSV

Added in 2026 (#32). The Fleet tab shows two different things - a live,
per-frame grid with nothing retained once redrawn, and a completed-run
history grid backed by the session cache - and the export button is
deliberately scoped to only the latter, since that's the one dataset that's
actually retained data rather than a momentary snapshot. The button's
tooltip says so, so an operator never has to guess which one they're about
to save.

`Core/Export/RunHistoryCsvExporter` does the formatting and takes a
rover-name-resolving delegate rather than any view-model dependency, so it
can be exercised directly by `RoverRally.Tests` the same way the codec and
`LapTimer` already are. Every number and timestamp goes through
`CultureInfo.InvariantCulture` explicitly, and rover names are RFC
4180-quoted, since a station used across international customer visits
cannot assume a period is always the decimal separator or that a name never
contains a comma. The file is written UTF-8-with-BOM so Excel does not
misread a non-ASCII rover name as ANSI.

---

## Known rough edges

- Configuration is spread across the config file and the registry, and it is not
  obvious from the code which value comes from where.
- ~~The window does more than a window should... Geofence alerting and the
  link lifecycle are still in the code-behind.~~ **Fixed in #73**, marked
  "extremely optional" in the issue that raised it but completed anyway at
  the owner's direction. `MainWindow.xaml.cs` (and every other View's
  code-behind — `StationView`, `FleetView`, `SettingsView`) is now
  `InitializeComponent()` and nothing else. Everything that used to live
  there — geofence alerting, the telemetry/drive-timer event handlers, the
  arm/e-stop/speed-unit/export commands, fleet filtering — moved into
  `StationViewModel`, which takes its collaborators
  (`Services/IStationService`, `Core/Control/IDriveControllerRegistry`,
  `Services/IDialogService`) by constructor injection, wired by a
  `Microsoft.Extensions.DependencyInjection` composition root in
  `App.xaml.cs`. The View and the ViewModel do not reference each other at
  all — `MainWindow` resolves its content purely through a `DataTemplate`
  registered against `StationViewModel`'s type. The one deliberate
  exception is `TrackView`: canvas drawing is inherently imperative in WPF,
  so it stayed a reactive custom control (Dependency Properties, redrawing
  itself from bound rovers' own `PropertyChanged`) rather than gaining a
  view model of its own. `RoverRally.Tests` now references `RoverRally.App`
  and `StationViewModelTests` exercises the whole command layer directly,
  with no window, no simulator, and no STA thread needed — see CLAUDE.md's
  "Finishing the MVVM pattern for testability (#73)" for the full shape.
- **Fixed in #80.** The Track tab's breadcrumb trail was one long-lived
  `Polyline` per rover, so a gap in reporting — most commonly a rover
  driving off the surveyed track extent and back — drew a straight line
  bridging the pre-gap and post-gap positions instead of showing the gap
  at all. `Views/RoverTrail.cs` (new) tracks the trail as a sequence of
  point segments and starts a new one whenever a fix was skipped, so
  `TrackView` now renders each segment as its own `Polyline` and the gap
  shows as a visible break. See CLAUDE.md's "Trail continuity across a
  rover gap (#80)" for the full mechanism.
