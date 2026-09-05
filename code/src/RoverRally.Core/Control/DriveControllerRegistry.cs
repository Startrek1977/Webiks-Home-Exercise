using System.Collections.Generic;

namespace RoverRally.Core.Control
{
    /// <summary>
    /// Keys one <see cref="DriveController"/> per rover, so each vehicle's
    /// armed/latched state is independent of the others and survives the
    /// station's selection moving away and back (see #35). The station used
    /// to hold a single shared controller and address whichever rover was
    /// selected, which meant stopping one vehicle could latch or unlatch a
    /// different one depending on what was selected when (#37).
    /// </summary>
    public class DriveControllerRegistry
    {
        private readonly Dictionary<byte, DriveController> _controllers = new Dictionary<byte, DriveController>();

        /// <summary>
        /// The controller for this rover, creating one on first use. The same
        /// instance is returned for the same id every time, so nothing needs
        /// to be explicitly "restored" on a selection change - the rover's
        /// controller was never thrown away in the first place.
        /// </summary>
        public DriveController For(byte roverId)
        {
            DriveController? controller;
            if (!_controllers.TryGetValue(roverId, out controller))
            {
                controller = new DriveController();
                _controllers[roverId] = controller;
            }

            return controller;
        }
    }
}
