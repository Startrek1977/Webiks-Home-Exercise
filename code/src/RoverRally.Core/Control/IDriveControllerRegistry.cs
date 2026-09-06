namespace RoverRally.Core.Control
{
    /// <summary>
    /// Contract for <see cref="DriveControllerRegistry"/>, extracted so the App
    /// layer's ViewModels can take it by constructor injection instead of a
    /// concrete type (#73).
    /// </summary>
    public interface IDriveControllerRegistry
    {
        /// <summary>
        /// The controller for this rover, creating one on first use. The same
        /// instance is returned for the same id every time.
        /// </summary>
        DriveController For(byte roverId);
    }
}
