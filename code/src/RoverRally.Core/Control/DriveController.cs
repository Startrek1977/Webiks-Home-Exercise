using System;

namespace RoverRally.Core.Control
{
    /// <summary>
    /// Holds the station's drive state - armed, and whether an emergency stop
    /// is latched - and decides what every outgoing command frame contains.
    ///
    /// The latch has to live here because the vehicle has no memory of it. A
    /// rover acts on the frame it most recently received and nothing else
    /// (docs/rover-link-protocol.md, "Firmware behaviour worth knowing"), so
    /// the stop it was told about 200ms ago is already forgotten. The
    /// operations guide nevertheless promises the operator a stop that latches
    /// until re-armed. The only way to present latching behaviour with a
    /// level-triggered vehicle is for the transmitter to keep asserting it,
    /// which is what NextDriveCommand does on every tick.
    ///
    /// Invariant: a latched stop implies not armed. That is what keeps the
    /// station's own idea of being armed identical to what it is transmitting,
    /// rather than merely intended to be.
    ///
    /// This controller is global to the station, not per rover (see #35, the
    /// still-pending root fix) - it protects whichever vehicle is currently
    /// selected. Selecting a vehicle whose own telemetry reports a stop this
    /// latch does not know about adopts it (see NextDriveCommand and
    /// TryToggleArm) rather than transmitting the false-clear that used to
    /// reach it. Because the latch is shared, that adoption also holds back
    /// the next vehicle selected, until someone re-arms - a known, accepted
    /// consequence of not having per-rover state yet, not a new one; the same
    /// station-wide bleed already applies to a stop engaged through the
    /// button (see #37).
    /// </summary>
    public class DriveController
    {
        /// <summary>
        /// Mirrors the vehicle's own two-second command-loss failsafe
        /// (docs/architecture.md). A vehicle that hasn't reported within this
        /// window is treated as unknown rather than as confirmed clear.
        /// </summary>
        private static readonly TimeSpan CommandLossWindow = TimeSpan.FromSeconds(2);

        private bool _armed;
        private bool _emergencyStopLatched;

        /// <summary>
        /// When this controller last decided what to transmit, and to whom.
        /// Guards against a race that live simulator testing caught: telemetry
        /// lags a tick or two behind whatever the station just sent, so the
        /// frame available right after an explicit re-arm can still be the
        /// pre-clear one. Using it anyway would make the guard immediately
        /// relatch the stop it was just told to clear - see
        /// ShouldAdoptAVehicleReportedStop. The rover id matters as much as
        /// the timestamp: this controller addresses one rover at a time but
        /// is shared across selections (see the class remarks), so a
        /// timestamp from commanding rover B says nothing about whether a
        /// frame from rover A is stale - only a comparison against B could
        /// ever be stale relative to it, and that is a different rover.
        /// </summary>
        private DateTime _lastCommandUtc = DateTime.MinValue;
        private byte? _lastCommandRoverId;

        /// <summary>
        /// Whether the given vehicle should be treated as holding a stop, for
        /// callers that just want the honest current picture (logging, the
        /// drive-state readout) rather than the guard's own, timing-aware
        /// decision - see ShouldAdoptAVehicleReportedStop for that one. True if
        /// its telemetry says so, or if it hasn't reported recently enough to
        /// say anything at all - including a vehicle that has never reported.
        /// An unknown vehicle cannot confirm it is clear, so it is not treated
        /// as clear.
        /// </summary>
        public static bool VehicleReportsStopped(bool isEmergencyStopped, DateTime lastFrameUtc, DateTime nowUtc)
        {
            return isEmergencyStopped || IsSilent(lastFrameUtc, nowUtc);
        }

        private static bool IsSilent(DateTime lastFrameUtc, DateTime nowUtc)
        {
            if (lastFrameUtc == DateTime.MinValue) return true;

            return (nowUtc - lastFrameUtc) >= CommandLossWindow;
        }

        /// <summary>
        /// Whether a vehicle-reported stop justifies adopting it right now -
        /// the #37 guard, made safe against the race above. A silent vehicle
        /// always qualifies, no matter how long ago this controller last
        /// transmitted: a vehicle that has stopped talking to the station
        /// entirely must be treated as stopped regardless of timing. A vehicle
        /// that IS reporting only has its freshness questioned when it is the
        /// SAME vehicle this controller most recently addressed - only then
        /// can a frame possibly predate our own last transmission to it. A
        /// different vehicle was not the recipient of that transmission, so
        /// there is nothing of ours for its telemetry to be stale relative to;
        /// gating it on a timestamp that belongs to some other rover's tick is
        /// exactly what let selecting rover A right after commanding rover B
        /// suppress a stop A was genuinely reporting.
        /// </summary>
        private bool ShouldAdoptAVehicleReportedStop(byte roverId, bool isEmergencyStopped, DateTime lastFrameUtc, DateTime nowUtc)
        {
            if (IsSilent(lastFrameUtc, nowUtc)) return true;
            if (!isEmergencyStopped) return false;

            if (_lastCommandRoverId != roverId) return true;

            return lastFrameUtc >= _lastCommandUtc;
        }

        /// <summary>Armed state as transmitted. Never true while a stop is latched.</summary>
        public bool IsArmed
        {
            get { return _armed; }
        }

        /// <summary>True from the moment the stop is engaged until an explicit re-arm.</summary>
        public bool IsEmergencyStopLatched
        {
            get { return _emergencyStopLatched; }
        }

        /// <summary>
        /// The frame the drive timer sends on this tick. While the stop is
        /// latched the slider positions are discarded rather than passed on:
        /// the vehicle would ignore them anyway, but a frame that carries a
        /// live control input alongside a stop is not worth putting on the
        /// wire at all.
        ///
        /// <paramref name="roverId"/> identifies which vehicle this tick is
        /// about to command. <paramref name="isEmergencyStopped"/> and
        /// <paramref name="lastFrameUtc"/> are that vehicle's own latest
        /// telemetry - the guard against #37. If it reports a stop this latch
        /// doesn't know about, adopt it before deciding what to send, rather
        /// than transmitting the emergencyStop:false that used to clear a
        /// stop nobody asked to clear. <paramref name="nowUtc"/> is the
        /// caller's clock, passed in rather than read here so the decision
        /// stays a pure function of its inputs and testable without a real
        /// clock.
        /// </summary>
        public StationCommand NextDriveCommand(byte roverId, bool isEmergencyStopped, DateTime lastFrameUtc, DateTime nowUtc,
                                               short throttle, short steering)
        {
            if (!_emergencyStopLatched && ShouldAdoptAVehicleReportedStop(roverId, isEmergencyStopped, lastFrameUtc, nowUtc))
            {
                _emergencyStopLatched = true;
                _armed = false;
            }

            _lastCommandUtc = nowUtc;
            _lastCommandRoverId = roverId;

            if (_emergencyStopLatched)
            {
                return new StationCommand(0, 0, true, false);
            }

            return new StationCommand(throttle, steering, false, _armed);
        }

        /// <summary>
        /// The big red button. Disarming here is not decoration - it is the
        /// half of the fault that made the failure intermittent, because the
        /// stale armed flag was what the next tick went on to transmit.
        /// </summary>
        public StationCommand EngageEmergencyStop()
        {
            _emergencyStopLatched = true;
            _armed = false;

            return new StationCommand(0, 0, true, false);
        }

        /// <summary>
        /// The ARM/DISARM press. Arming is the explicit re-arm that clears a
        /// latched stop, and it is the only thing that clears one.
        ///
        /// Returns false, changing nothing, when clearing a latch would hand
        /// the vehicle a throttle that is not centred: the marshal who pressed
        /// the button is standing on the track, and releasing the stop into a
        /// raised slider drives the vehicle straight at them.
        ///
        /// <paramref name="roverId"/> identifies the vehicle being armed.
        /// <paramref name="isEmergencyStopped"/> and <paramref name="lastFrameUtc"/>
        /// adopt a stop the vehicle is holding but this latch does not know
        /// about (a station restart, or a vehicle selected for the first
        /// time) before deciding whether to arm, so that case goes through
        /// the same throttle-centred gate as any other re-arm rather than
        /// being waved through silently.
        /// </summary>
        public bool TryToggleArm(byte roverId, bool isEmergencyStopped, DateTime lastFrameUtc, DateTime nowUtc,
                                 short throttle, out StationCommand command)
        {
            if (!_emergencyStopLatched && ShouldAdoptAVehicleReportedStop(roverId, isEmergencyStopped, lastFrameUtc, nowUtc))
            {
                _emergencyStopLatched = true;
                _armed = false;
            }

            _lastCommandUtc = nowUtc;
            _lastCommandRoverId = roverId;

            bool arming = !_armed;

            if (arming && _emergencyStopLatched && throttle != 0)
            {
                command = null;
                return false;
            }

            if (arming) _emergencyStopLatched = false;
            _armed = arming;

            command = new StationCommand(0, 0, false, _armed);
            return true;
        }
    }
}
