using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Control;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    /// <summary>
    /// The emergency stop is the one control on this station where being
    /// subtly wrong hurts somebody. The vehicle keeps no memory of a stop, so
    /// every one of these tests is really asking the same question: does the
    /// station keep saying it?
    /// </summary>
    [TestClass]
    public class DriveControllerTests
    {
        private const short FullThrottle = 1000;

        [TestMethod]
        public void HoldsTheStopFlagOnEveryTickUntilReArmed()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            for (int tick = 1; tick <= 50; tick++)
            {
                StationCommand command = controller.NextDriveCommand(FullThrottle, FullThrottle);

                Assert.IsTrue(command.EmergencyStop,
                              "The stop was released on tick " + tick + ".");
            }
        }

        [TestMethod]
        public void IgnoresTheThrottleWhileTheStopIsLatched()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            StationCommand command = controller.NextDriveCommand(FullThrottle, -750);

            Assert.IsTrue(command.EmergencyStop);
            Assert.IsFalse(command.Armed);
            Assert.AreEqual((short)0, command.Throttle);
            Assert.AreEqual((short)0, command.Steering);
        }

        [TestMethod]
        public void DisarmsLocallyWhenTheStopIsEngaged()
        {
            DriveController controller = ArmedController();

            StationCommand command = controller.EngageEmergencyStop();

            Assert.IsFalse(controller.IsArmed, "The station still believes it is armed.");
            Assert.IsFalse(command.Armed, "The station transmitted an armed flag it does not hold.");
            Assert.IsTrue(controller.IsEmergencyStopLatched);
        }

        /// <summary>
        /// The invariant, checked after every step rather than only at the
        /// end. Checking it once at the end is not enough: a controller that
        /// forgot to disarm on the stop and then disarmed on the next button
        /// press lands in the same final state by a route that would have
        /// driven the vehicle away in between.
        /// </summary>
        [TestMethod]
        public void NeverReportsItselfArmedWhileTheStopIsLatched()
        {
            DriveController controller = ArmedController();

            controller.EngageEmergencyStop();
            AssertInvariant(controller, "after the stop");

            StationCommand refused;
            controller.TryToggleArm(FullThrottle, out refused);
            AssertInvariant(controller, "after a refused re-arm");

            controller.NextDriveCommand(FullThrottle, 0);
            AssertInvariant(controller, "after a tick at full throttle");

            Assert.IsTrue(controller.IsEmergencyStopLatched, "The latch was lost along the way.");
        }

        private static void AssertInvariant(DriveController controller, string when)
        {
            if (!controller.IsEmergencyStopLatched) return;

            Assert.IsFalse(controller.IsArmed,
                           "The station reported itself armed while a stop was latched, " + when + ".");
            Assert.IsFalse(controller.NextDriveCommand(FullThrottle, FullThrottle).Armed,
                           "The station transmitted an armed flag while a stop was latched, " + when + ".");
        }

        [TestMethod]
        public void ClearsTheLatchOnlyWhenTheOperatorReArms()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            for (int tick = 0; tick < 10; tick++)
            {
                Assert.IsTrue(controller.NextDriveCommand(0, 0).EmergencyStop);
            }

            StationCommand rearm;
            Assert.IsTrue(controller.TryToggleArm(0, out rearm));

            Assert.IsFalse(rearm.EmergencyStop);
            Assert.IsTrue(rearm.Armed);
            Assert.IsFalse(controller.IsEmergencyStopLatched);

            StationCommand next = controller.NextDriveCommand(400, -200);

            Assert.IsFalse(next.EmergencyStop);
            Assert.IsTrue(next.Armed);
            Assert.AreEqual((short)400, next.Throttle);
            Assert.AreEqual((short)-200, next.Steering);
        }

        [TestMethod]
        public void RefusesToReArmWhileTheThrottleIsOffCentre()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            StationCommand command;
            bool rearmed = controller.TryToggleArm(FullThrottle, out command);

            Assert.IsFalse(rearmed);
            Assert.IsNull(command);
            Assert.IsTrue(controller.IsEmergencyStopLatched, "The refused press cleared the latch anyway.");
            Assert.IsFalse(controller.IsArmed);
            Assert.IsTrue(controller.NextDriveCommand(FullThrottle, 0).EmergencyStop);
        }

        [TestMethod]
        public void DisarmsWithoutComplainingAboutTheThrottle()
        {
            DriveController controller = ArmedController();

            StationCommand command;
            bool toggled = controller.TryToggleArm(FullThrottle, out command);

            Assert.IsTrue(toggled, "Disarming is never the unsafe direction.");
            Assert.IsFalse(controller.IsArmed);
            Assert.IsFalse(command.Armed);
        }

        [TestMethod]
        public void PassesTheSliderPositionsThroughWhileArmed()
        {
            DriveController controller = ArmedController();

            StationCommand command = controller.NextDriveCommand(-450, 875);

            Assert.AreEqual((short)-450, command.Throttle);
            Assert.AreEqual((short)875, command.Steering);
            Assert.IsFalse(command.EmergencyStop);
            Assert.IsTrue(command.Armed);
        }

        [TestMethod]
        public void SendsNothingArmedBeforeTheOperatorArms()
        {
            DriveController controller = new DriveController();

            StationCommand command = controller.NextDriveCommand(FullThrottle, 0);

            Assert.IsFalse(controller.IsArmed);
            Assert.IsFalse(command.Armed);
            Assert.IsFalse(command.EmergencyStop);
        }

        /// <summary>
        /// Ties the state machine to the wire. The controller agreeing with
        /// itself proves nothing until the frame it produces reaches the
        /// rover-side reader with bit 0 of the flags byte set.
        /// </summary>
        [TestMethod]
        public void EncodesTheLatchedStopAsBitZeroOfTheCommandFlags()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            StationCommand held = controller.NextDriveCommand(FullThrottle, FullThrottle);
            byte[] datagram = FrameCodec.EncodeCommand(4, held.Throttle, held.Steering,
                                                      held.EmergencyStop, held.Armed);

            Assert.AreEqual(0x01, datagram[8] & 0x01, "Bit 0 of the flags byte is not set.");

            DriveCommand command;
            Assert.IsTrue(SimulatorFrameWriter.TryReadCommand(datagram, out command));

            Assert.IsTrue(command.EmergencyStop, "The rover-side reader did not see the stop.");
            Assert.IsFalse(command.Armed);
            Assert.AreEqual((short)0, command.Throttle);
        }

        private static DriveController ArmedController()
        {
            DriveController controller = new DriveController();

            StationCommand command;
            Assert.IsTrue(controller.TryToggleArm(0, out command));
            Assert.IsTrue(controller.IsArmed);

            return controller;
        }
    }
}
