# Solution Log

> Please fill in this document as you work through the task. Be honest and specific — there are no wrong answers here. We use this to understand your thought process, not to judge your speed.

---

## Time Tracking

| Phase | Start Time | End Time | Duration |
|---|---|---|---|
| Getting the existing application built and running | | | |
| Reading the code / working out what it does | | | |
| Retargeting to .NET 8 | | | |
| Moving to 64-bit | | | |
| The telemetry SDK | | | |
| Bug fixes | | | |
| Logging | | | |
| Tests | | | |
| Cleanup | | | |
| Documentation | | | |
| **Total** | | | |

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
| `RoverRally.Core` | |
| `RoverRally.App` | |
| `RoverRally.Tests` | |

### Moving to 64-bit

*What did 64-bit break that .NET 8 on its own did not? How did you find it?*



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

---

## What You Removed

*Dana asked you to use your judgement about what still belongs. What did you take out, and how did you satisfy yourself it was safe to remove?*

**`AnalyzerHost`, `StationMonitorService`, `RunSnapshot`.** Both are hard
blockers for the .NET 8 move on their own terms - `AnalyzerHost` loads
"post-run analyzers" into a second `AppDomain`, and `AppDomain.CreateDomain`
does not exist outside .NET Framework; `StationMonitorService` publishes the
fleet over `System.Runtime.Remoting` with `RunSnapshot` serialized through
`BinaryFormatter`, and neither survives to .NET 8 at all. Before deciding
whether to port them or drop them, I spiked (#1) whether anything actually
uses them, because a working office-overview client is a different problem
from a dead one.

Nothing does. I traced every reference to both types across the App, Core,
and Tests projects, the `.csproj` compile lists, `App.config`, and every doc
in the repo. Both classes compile into `RoverRally.Core` - they're in the
`<Compile Include>` lists - but neither is ever constructed anywhere. No
`Analyzers` folder and no `*.Analyzer.dll` exist anywhere in the repo, source
or build output, so `AnalyzerHost.Discover()` has nothing to find even in
principle. `App.config` does define `StationMonitorEndpoint` and
`StationMonitorPort`, but the only code that reads them is
`SettingsView.xaml.cs`, which concatenates the two values into a read-only
`TextBlock` for display - neither value is ever passed to
`StationMonitorService.Publish`, and nothing calls `Publish` at all. The
"office overview client" is not a running feature with dead config left over
from decommissioning it; it's a Settings-tab label pointing at a service that
never starts.

`docs/operations-guide.md` documents both as working - "the overview client
in the office can attach to a running station," "post-run analyzers... are
picked up automatically." I did not take that as evidence of use, for the
same reason "trust the code over the documents" mattered on the telemetry SDK
and cut the other way on the emergency stop: a document tells you what's
*supposed* to happen, not what does. Here the code, the build output, and the
absence of a single caller all agree with each other and disagree with the
document. `docs/architecture.md` dates the office overview client to 2019 and
notes nobody has owned the station full time since 2021, which is consistent
with docs describing a feature that quietly stopped being used and was never
un-written. `INSTRUCTIONS.md` - Dana's brief, which I'm treating as the actual
source of requirements - never mentions monitoring, remote overview, or
analyzers in any form.

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
two `App.config` keys, the `MonitorEndpointText` line in `SettingsView`, and
the stale "Remote monitoring" / "Post-run analyzers" sections of
`docs/operations-guide.md`. Full evidence trace is on #1.

---

## Logging

*What did you build, and what decisions did you make about it?*



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

---

## Tools Used

*What AI tools did you use, and how? Where did they help, and where did they get in your way or get it wrong?*

| Tool | How I used it |
|---|---|
| GitHub Copilot code review | Automatic review on the pull request. Earned its place twice. On the codec it caught a genuine integer-overflow hole in the CRC bounds check that my own tests had walked past, and a documentation/implementation mismatch on `TelemetryFrame`. On the emergency stop it caught an unguarded null dereference in both drive handlers, and an indicator that refreshed on telemetry but not on selection. Its file-by-file summaries are noise; the substantive findings have all been real. I reproduce each one before accepting it rather than taking the diagnosis on trust, which is also how I found that its "stale indicator" report was worse than described - not a brief lag, but permanent when the newly selected vehicle is silent |
| Codex code review | Also automatic on the pull request. Raised the one finding I decided *not* to act on: with a single station-wide latch, stopping rover A and then selecting rover B stops B instead, and re-arming B clears A's latch. It is correct - I reproduced both halves against the simulator - but the fix is per-rover state, which the repository owner had explicitly deferred out of this issue, and binding the latch to its rover silently answers a design question (whether the station commands vehicles it is not showing) that belongs to the owner rather than to a reviewer or to me. Filed rather than fixed. Worth recording that the useful output of a review is not always a diff |
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



---

## Additional Notes
