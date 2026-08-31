# RoverRally Station — Architecture Notes

**Last substantially updated:** June 2020 (ports and framework notes touched up 2022)

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
| `RoverRally.Core` | Shared library: models, the telemetry link, unit conversion, geometry, configuration, session cache. |
| `RoverRally.Tests` | MSTest project. Thin — it was started and not kept up. |
| `RoverRally.Simulator` | Bench simulator. Written later and on its own, so it is already on modern .NET and is not part of the station solution. |

All of the business logic lives in `RoverRally.Core`. The views are meant to be
presentation only, so that the logic can be exercised without a window open.

---

## Third-party code

| Component | Version | Notes |
|---|---|---|
| `RoverLink.Telemetry` | 1.4.2 | Vendor SDK for the RL-100 link. Binary only, we never had source. 32-bit build. |
| `Newtonsoft.Json` | 6.0.8 | Reads the roster file. Pinned at this version since 2016. |
| `MSTest.TestFramework` | 2.2.10 | Test project only. |

RoverLink Systems stopped trading and the support address bounces. Whatever we
need from that SDK, we own now.

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

## Known rough edges

- Configuration is spread across the config file and the registry, and it is not
  obvious from the code which value comes from where.
- The window does more than a window should. Pulling the drive logic out has
  been on the list since 2020.
- There is no logging to disk from the application itself.
