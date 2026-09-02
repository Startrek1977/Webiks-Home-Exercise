namespace RoverRally.Core.Control
{
    /// <summary>
    /// One drive command, decided but not yet encoded. It exists so the
    /// decision of what to transmit can be made - and tested - without a
    /// socket, a window or a timer anywhere near it.
    ///
    /// Deliberately not called DriveCommand: the test project already owns
    /// that name for its transcription of the simulator's reader, and the
    /// two meet in the same test file.
    /// </summary>
    public class StationCommand
    {
        public StationCommand(short throttle, short steering, bool emergencyStop, bool armed)
        {
            Throttle = throttle;
            Steering = steering;
            EmergencyStop = emergencyStop;
            Armed = armed;
        }

        public short Throttle { get; }
        public short Steering { get; }

        /// <summary>Bit 0 of the command flags byte.</summary>
        public bool EmergencyStop { get; }

        /// <summary>Bit 1 of the command flags byte.</summary>
        public bool Armed { get; }
    }
}
