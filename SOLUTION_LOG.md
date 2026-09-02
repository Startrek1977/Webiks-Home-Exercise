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
| Battery readout | | |
| Imperial speed | | |

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
2.

---

## What You Removed

*Dana asked you to use your judgement about what still belongs. What did you take out, and how did you satisfy yourself it was safe to remove?*



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
| GitHub Copilot code review | Automatic review on the pull request. Earned its place: it caught a genuine integer-overflow hole in the CRC bounds check that my own tests had walked past, and a documentation/implementation mismatch on `TelemetryFrame`. Its file-by-file summaries were noise, but the two substantive findings were both real and both worth fixing. I reproduced each as a failing test before accepting it rather than taking the diagnosis on trust |
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
