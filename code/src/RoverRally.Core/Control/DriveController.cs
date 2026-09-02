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
    /// </summary>
    public class DriveController
    {
        private bool _armed;
        private bool _emergencyStopLatched;

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
        /// </summary>
        public StationCommand NextDriveCommand(short throttle, short steering)
        {
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
        /// </summary>
        public bool TryToggleArm(short throttle, out StationCommand command)
        {
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
