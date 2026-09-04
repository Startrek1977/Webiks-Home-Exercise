using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Control;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    /// <summary>
    /// The registry's whole job is get-or-create-and-remember, so these tests
    /// are about the identity of what it hands back, not about drive
    /// behaviour itself - DriveControllerTests already covers that per
    /// instance.
    /// </summary>
    [TestClass]
    public class DriveControllerRegistryTests
    {
        private const byte FalafelId = 1;
        private const byte SandstormId = 2;

        [TestMethod]
        public void ReturnsTheSameInstanceForTheSameRoverIdOnRepeatedCalls()
        {
            DriveControllerRegistry registry = new DriveControllerRegistry();

            DriveController first = registry.For(FalafelId);
            DriveController second = registry.For(FalafelId);

            Assert.AreSame(first, second, "Two calls for the same rover returned different controllers.");
        }

        [TestMethod]
        public void ReturnsDifferentInstancesForDifferentRoverIds()
        {
            DriveControllerRegistry registry = new DriveControllerRegistry();

            DriveController falafel = registry.For(FalafelId);
            DriveController sandstorm = registry.For(SandstormId);

            Assert.AreNotSame(falafel, sandstorm, "Two different rovers were handed the same controller.");
        }

        [TestMethod]
        public void AControllerForANeverSeenRoverStartsUnarmedAndUnlatched()
        {
            DriveControllerRegistry registry = new DriveControllerRegistry();

            DriveController controller = registry.For(FalafelId);

            Assert.IsFalse(controller.IsArmed);
            Assert.IsFalse(controller.IsEmergencyStopLatched);
        }

        /// <summary>
        /// The regression case this whole issue is about, expressed through
        /// the registry rather than by holding two DriveController instances
        /// by hand: commanding one rover through the registry must never
        /// reach a different rover's controller.
        /// </summary>
        [TestMethod]
        public void ArmingOneRoverThroughTheRegistryDoesNotArmAnother()
        {
            DriveControllerRegistry registry = new DriveControllerRegistry();

            StationCommand command;
            Assert.IsTrue(registry.For(FalafelId).TryToggleArm(FalafelId, false, Now, Now, 0, out command));

            Assert.IsTrue(registry.For(FalafelId).IsArmed);
            Assert.IsFalse(registry.For(SandstormId).IsArmed,
                           "Arming Falafel through the registry armed Sandstorm's controller too.");
        }

        private static DateTime Now
        {
            get { return DateTime.UtcNow; }
        }
    }
}
