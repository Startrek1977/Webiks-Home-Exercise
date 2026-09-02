# RoverRally Station — Architecture Notes

**Last substantially updated:** June 2020 (ports and framework notes touched up
2022; telemetry codec and emergency stop latch 2026)

---

## History

The station started in 2016 as a one-window utility for watching a single
vehicle. The fleet grid arrived in 2017, the drive controls in 2018, the session
cache and the office overview client in 2019, and the imperial readout in 2019
for the visiting customer trials. It has been on .NET Framework 4.8 and 32-bit
since the 4.8 retarget in 2019.

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
| `Newtonsoft.Json` | 6.0.8 | Reads the roster file. Pinned at this version since 2016. |
| `MSTest.TestFramework` | 2.2.10 | Test project only. |

RoverLink Systems stopped trading and the support address bounces, so the
32-bit-only `RoverLink.Telemetry.dll` has been replaced by a managed
reimplementation in `RoverRally.Core.Telemetry`. We own the RL-100 codec now;
`docs/rover-link-protocol.md` is the specification it is written against.

---

## Data the station keeps

| What | Where | Format |
|---|---|---|
| Fleet roster | `Data\rovers.json` | JSON, edited by hand by the site lead |
| Completed runs | `Data\session-cache.bin` | Fixed stride binary records |
| Site configuration | `App.config` | appSettings |
| Operator preferences | Registry, `HKCU\Software\RoverLink\Station` | One subkey per operator |

The session cache is a flat binary file rather than a database because the
station has to work on a laptop in a tent with nothing installed on it. The
record layout mirrors the vendor's session block so the two stay interchangeable.

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

---

## Known rough edges

- Configuration is spread across the config file and the registry, and it is not
  obvious from the code which value comes from where.
- The window does more than a window should. Pulling the drive logic out had
  been on the list since 2020; the armed and emergency-stop state came out in
  #20 and now lives in `Core/Control/DriveController`, which is what made it
  testable. Frame handling, geofence alerting and the link lifecycle are still
  in the code-behind.
- There is no logging to disk from the application itself.
