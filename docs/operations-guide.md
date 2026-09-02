# RoverRally Station — Operations Guide

**Audience:** track marshals and test engineers
**Owner:** Kadima Proving Ground, operations
**Last updated:** September 2026

---

## Starting a session

1. Power the base station and wait for the green link LED.
2. Launch **RoverRally Station** from the desktop shortcut.
3. The Track tab should start drawing vehicles within a few seconds. If the
   frame counter along the bottom stays at zero, the base station is not
   relaying — power cycle it before calling anyone.

The station reads the fleet list from `Data\rovers.json`. Vehicles that are on
site but not in that file will not appear, even though their telemetry is
arriving. Ask the site lead to add them.

---

## Track tab

The map is drawn from the site survey, so a vehicle shown on the asphalt really
is on the asphalt. The dashed amber outline is the fenced area. Anything outside
it is either the run-off or the car park.

Each vehicle shows as a coloured dot with a short line pointing the way it is
facing, and drags a breadcrumb trail behind it for the last few minutes.

The panel on the right follows whichever vehicle is selected. Select a different
one from the Fleet tab.

---

## Driving

Arm the vehicle first — nothing on the throttle or steering slider has any
effect until it is armed. The station keeps sending the current slider positions
for as long as the vehicle is armed, so a slider left off centre means a vehicle
that keeps moving.

### Emergency stop

The big red button cuts the vehicle immediately.

**The stop latches.** Once it is pressed the vehicle stays stopped and ignores
throttle input until it is explicitly re-armed. This is deliberate: a marshal who
hits the button should be able to walk onto the track without watching the
screen. It does not matter where the sliders are left, and it does not matter how
long you take.

Pressing the stop also disarms the vehicle, so the ARM button is what releases
it. Nothing else does.

**If the station cannot send, it tells you.** When the command link is not
running the button reports that the stop was **not** sent, and asks you to stop
the vehicle by hand. A stop that failed and a stop that worked do not look alike
on screen — read the dialog rather than assuming the vehicle is slowing.

**Re-arming needs the throttle centred.** If the throttle slider is still up when
you press ARM, the station refuses and says so, rather than releasing the vehicle
into a raised slider while you are still standing in front of it. Centre the
slider and press ARM again.

The line under the button reads the station's state and the vehicle's own
reported state side by side:

```
STATION: STOP LATCHED  -  VEHICLE: STOPPED
```

The left half is what the station is asserting; the right half is what the
vehicle is reporting back in its telemetry. They should agree within a frame or
two. If the left says the stop is latched and the right does not say `STOPPED`,
the vehicle is not hearing the station — treat that as a vehicle you cannot rely
on, and clear the track by hand.

The right half always describes the vehicle currently selected and follows the
selection straight away, so it never shows you one vehicle's state under
another's name. It reads `VEHICLE: NO DATA` for a vehicle that has not reported
since the station started.

If the link drops while a vehicle is moving, the vehicle stops on its own after
two seconds without a command frame.

---

## Fleet tab

Every vehicle on the roster with its current readings, filterable by status and
searchable by name or chassis. Selecting a row makes that vehicle the one the
Track tab panel follows.

The lower grid is the recent run history, read from the session cache. It shows
the last few completed runs with duration, distance covered and peak speed.

---

## Settings tab

Speed units are per operator — set it once and the station remembers it for your
Windows account on that machine, including which vehicle you had selected. The
rest of the values on the tab are site configuration and are read from the
station's configuration file; they are shown so you can read them out over the
radio, not so you can change them.

---

## Remote monitoring

The station publishes the live session so the overview client in the office can
watch a run without anyone standing at the station PC. The endpoint is set with
the `StationMonitorEndpoint` and `StationMonitorPort` values in the station
configuration; the office client needs both, plus the station PC's hostname.

Post-run analyzers dropped into the `Analyzers` folder are picked up
automatically and run against the captured session at the end of each run.

---

## Logs

The station writes a rolling log to `C:\ProgramData\RoverRally\station.log`.
Attach it to any fault report — it is the first thing anyone will ask for.

---

## Common problems

| Symptom | What to do |
|---|---|
| Frame counter stuck at zero | Base station not relaying; power cycle it |
| A vehicle is missing from both tabs | Not in `rovers.json`; ask the site lead |
| Vehicle shown but readings frozen | Check the signal column; usually the vehicle is behind the embankment |
| Station will not start | Check that `Data\rovers.json` is present and valid |
