using System;
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
        private const byte FalafelId = 1;
        private const byte SandstormId = 2;

        /// <summary>
        /// Stands in for "the vehicle just reported, right now, and it isn't
        /// stopped" wherever a test's own timing isn't the thing under test -
        /// most of the pre-existing suite, which predates #37 and its
        /// telemetry timestamps entirely. Every test below that is not
        /// specifically about cross-rover or stale-frame timing addresses
        /// FalafelId throughout, so the freshness gate's same-rover branch is
        /// what applies - the cross-rover bypass has its own dedicated test.
        /// </summary>
        private static DateTime Now
        {
            get { return DateTime.UtcNow; }
        }

        [TestMethod]
        public void HoldsTheStopFlagOnEveryTickUntilReArmed()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            for (int tick = 1; tick <= 50; tick++)
            {
                StationCommand command = controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, FullThrottle);

                Assert.IsTrue(command.EmergencyStop,
                              "The stop was released on tick " + tick + ".");
            }
        }

        [TestMethod]
        public void IgnoresTheThrottleWhileTheStopIsLatched()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            StationCommand command = controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, -750);

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
            controller.TryToggleArm(FalafelId, false, Now, Now, FullThrottle, out refused);
            AssertInvariant(controller, "after a refused re-arm");

            controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, 0);
            AssertInvariant(controller, "after a tick at full throttle");

            Assert.IsTrue(controller.IsEmergencyStopLatched, "The latch was lost along the way.");
        }

        private static void AssertInvariant(DriveController controller, string when)
        {
            if (!controller.IsEmergencyStopLatched) return;

            Assert.IsFalse(controller.IsArmed,
                           "The station reported itself armed while a stop was latched, " + when + ".");
            Assert.IsFalse(controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, FullThrottle).Armed,
                           "The station transmitted an armed flag while a stop was latched, " + when + ".");
        }

        [TestMethod]
        public void ClearsTheLatchOnlyWhenTheOperatorReArms()
        {
            DriveController controller = ArmedController();
            controller.EngageEmergencyStop();

            for (int tick = 0; tick < 10; tick++)
            {
                Assert.IsTrue(controller.NextDriveCommand(FalafelId, false, Now, Now, 0, 0).EmergencyStop);
            }

            StationCommand rearm;
            Assert.IsTrue(controller.TryToggleArm(FalafelId, false, Now, Now, 0, out rearm));

            Assert.IsFalse(rearm.EmergencyStop);
            Assert.IsTrue(rearm.Armed);
            Assert.IsFalse(controller.IsEmergencyStopLatched);

            StationCommand next = controller.NextDriveCommand(FalafelId, false, Now, Now, 400, -200);

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
            bool rearmed = controller.TryToggleArm(FalafelId, false, Now, Now, FullThrottle, out command);

            Assert.IsFalse(rearmed);
            Assert.IsNull(command);
            Assert.IsTrue(controller.IsEmergencyStopLatched, "The refused press cleared the latch anyway.");
            Assert.IsFalse(controller.IsArmed);
            Assert.IsTrue(controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, 0).EmergencyStop);
        }

        [TestMethod]
        public void DisarmsWithoutComplainingAboutTheThrottle()
        {
            DriveController controller = ArmedController();

            StationCommand command;
            bool toggled = controller.TryToggleArm(FalafelId, false, Now, Now, FullThrottle, out command);

            Assert.IsTrue(toggled, "Disarming is never the unsafe direction.");
            Assert.IsFalse(controller.IsArmed);
            Assert.IsFalse(command.Armed);
        }

        [TestMethod]
        public void PassesTheSliderPositionsThroughWhileArmed()
        {
            DriveController controller = ArmedController();

            StationCommand command = controller.NextDriveCommand(FalafelId, false, Now, Now, -450, 875);

            Assert.AreEqual((short)-450, command.Throttle);
            Assert.AreEqual((short)875, command.Steering);
            Assert.IsFalse(command.EmergencyStop);
            Assert.IsTrue(command.Armed);
        }

        [TestMethod]
        public void SendsNothingArmedBeforeTheOperatorArms()
        {
            DriveController controller = new DriveController();

            StationCommand command = controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, 0);

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

            StationCommand held = controller.NextDriveCommand(FalafelId, false, Now, Now, FullThrottle, FullThrottle);
            byte[] datagram = FrameCodec.EncodeCommand(4, held.Throttle, held.Steering,
                                                      held.EmergencyStop, held.Armed);

            Assert.AreEqual(0x01, datagram[8] & 0x01, "Bit 0 of the flags byte is not set.");

            DriveCommand command;
            Assert.IsTrue(SimulatorFrameWriter.TryReadCommand(datagram, out command));

            Assert.IsTrue(command.EmergencyStop, "The rover-side reader did not see the stop.");
            Assert.IsFalse(command.Armed);
            Assert.AreEqual((short)0, command.Throttle);
        }

        /// <summary>
        /// The #37 reproduction: a vehicle the station never latched, still
        /// reporting a stop it held from earlier, must not have that stop
        /// cleared just because it is now the one being commanded - armed or
        /// not, since the vehicle clears its own held stop on the flag alone.
        /// </summary>
        [TestMethod]
        public void RefusesToClearAStopTheVehicleIsStillReportingOnAnUnlatchedStation()
        {
            DriveController disarmed = new DriveController();
            StationCommand disarmedCommand = disarmed.NextDriveCommand(FalafelId, true, Now, Now, FullThrottle, FullThrottle);
            Assert.IsTrue(disarmedCommand.EmergencyStop,
                          "A disarmed station still transmitted the false-clear that let the vehicle free-run.");
            Assert.AreEqual((short)0, disarmedCommand.Throttle);

            DriveController armed = ArmedController();
            StationCommand armedCommand = armed.NextDriveCommand(FalafelId, true, Now, Now, FullThrottle, FullThrottle);
            Assert.IsTrue(armedCommand.EmergencyStop);
            Assert.IsFalse(armedCommand.Armed);
        }

        [TestMethod]
        public void AdoptsAVehicleReportedStopIntoTheStationLatch()
        {
            DriveController controller = ArmedController();

            controller.NextDriveCommand(FalafelId, true, Now, Now, FullThrottle, FullThrottle);

            Assert.IsTrue(controller.IsEmergencyStopLatched,
                          "The station did not adopt the stop the vehicle reported.");
            AssertInvariant(controller, "after adopting a vehicle-reported stop");
        }

        [TestMethod]
        public void RequiresACentredThrottleToClearAnAdoptedStop()
        {
            DriveController controller = new DriveController();

            StationCommand command;
            bool rearmed = controller.TryToggleArm(FalafelId, true, Now, Now, FullThrottle, out command);

            Assert.IsFalse(rearmed, "An adopted stop was cleared without a centred throttle.");
            Assert.IsNull(command);
            Assert.IsTrue(controller.IsEmergencyStopLatched);
            Assert.IsFalse(controller.IsArmed);
        }

        [TestMethod]
        public void ReArmClearsAnAdoptedLatchWhenThrottleIsCentred()
        {
            DriveController controller = new DriveController();

            StationCommand command;
            bool rearmed = controller.TryToggleArm(FalafelId, true, Now, Now, 0, out command);

            Assert.IsTrue(rearmed);
            Assert.IsFalse(command.EmergencyStop);
            Assert.IsTrue(command.Armed);
            Assert.IsFalse(controller.IsEmergencyStopLatched);
            Assert.IsTrue(controller.IsArmed);
        }

        /// <summary>
        /// Caught by running the fix against the live simulator, not by any
        /// unit test: a re-arm and the next drive-timer tick can be only
        /// milliseconds apart, while telemetry arrives at 5Hz. The frame the
        /// tick has on hand can still be the one from *before* the re-arm,
        /// still reporting the old stop - and a guard that trusted it blindly
        /// would relatch the very thing it was just told to clear. This is
        /// what makes ShouldAdoptAVehicleReportedStop's freshness check
        /// necessary rather than decorative. Both calls address the same
        /// rover throughout, so the same-rover branch of the gate is what is
        /// under test here.
        /// </summary>
        [TestMethod]
        public void DoesNotRelatchOnAStaleFrameFromBeforeAnExplicitReArm()
        {
            DateTime beforeRearm = Now;
            DateTime rearmMoment = beforeRearm.AddMilliseconds(50);
            DateTime nextTick = rearmMoment.AddMilliseconds(20);

            DriveController controller = new DriveController();
            controller.NextDriveCommand(FalafelId, true, beforeRearm, beforeRearm, FullThrottle, FullThrottle);
            Assert.IsTrue(controller.IsEmergencyStopLatched, "Setup: the stop should have been adopted first.");

            StationCommand rearm;
            Assert.IsTrue(controller.TryToggleArm(FalafelId, true, beforeRearm, rearmMoment, 0, out rearm));
            Assert.IsFalse(controller.IsEmergencyStopLatched, "Setup: the re-arm should have cleared the latch.");

            // The next tick fires before any fresher telemetry has arrived -
            // frame timestamp is still the pre-rearm one.
            StationCommand afterRearm = controller.NextDriveCommand(FalafelId, true, beforeRearm, nextTick, 400, 0);

            Assert.IsFalse(controller.IsEmergencyStopLatched,
                           "A stale, pre-rearm frame relatched a stop that had just been cleared.");
            Assert.IsFalse(afterRearm.EmergencyStop);
            Assert.IsTrue(afterRearm.Armed);
            Assert.AreEqual((short)400, afterRearm.Throttle);
        }

        /// <summary>
        /// The other half of the same fix: once telemetry actually catches up
        /// and confirms - after the re-arm - that the vehicle is still (or
        /// again) reporting a stop, that is new information and the guard
        /// must act on it. The freshness check is there to reject stale
        /// reads, not to make the controller deaf.
        /// </summary>
        [TestMethod]
        public void StillAdoptsAStopConfirmedByAFrameNewerThanTheReArm()
        {
            DateTime beforeRearm = Now;
            DateTime rearmMoment = beforeRearm.AddMilliseconds(50);
            DateTime confirmingFrame = rearmMoment.AddMilliseconds(150);
            DateTime nextTick = confirmingFrame.AddMilliseconds(20);

            DriveController controller = new DriveController();
            controller.NextDriveCommand(FalafelId, true, beforeRearm, beforeRearm, FullThrottle, FullThrottle);

            StationCommand rearm;
            Assert.IsTrue(controller.TryToggleArm(FalafelId, true, beforeRearm, rearmMoment, 0, out rearm));

            StationCommand afterConfirmation =
                controller.NextDriveCommand(FalafelId, true, confirmingFrame, nextTick, 400, 0);

            Assert.IsTrue(controller.IsEmergencyStopLatched,
                          "A confirmed, post-rearm stop was not adopted.");
            Assert.IsTrue(afterConfirmation.EmergencyStop);
        }

        /// <summary>
        /// Flagged by review on the pull request, and reproduced here: the
        /// freshness gate is one field shared by the whole controller, but
        /// the controller addresses one rover at a time. Ticking for
        /// Sandstorm advances that field to "now" regardless of Sandstorm's
        /// own telemetry age; switching straight to Falafel, who is reporting
        /// a genuine stop from a frame that merely happens to be a little
        /// older than Sandstorm's tick, must not let that unrelated timestamp
        /// suppress the adoption - Falafel was never the recipient of
        /// anything this controller sent, so there is nothing for its
        /// telemetry to be stale relative to.
        /// </summary>
        [TestMethod]
        public void DoesNotSuppressAdoptionForADifferentRoverJustBecauseAnotherRoverWasCommandedMoreRecently()
        {
            DateTime falafelReportedStopped = Now;
            DateTime sandstormTick = falafelReportedStopped.AddMilliseconds(80);
            DateTime selectFalafelTick = sandstormTick.AddMilliseconds(20);

            DriveController controller = new DriveController();

            // Sandstorm is selected first, driving normally - not stopped,
            // reporting fresh. This is what advances _lastCommandUtc past
            // Falafel's already-genuine stop report.
            StationCommand sandstormCommand = controller.NextDriveCommand(
                SandstormId, false, sandstormTick, sandstormTick, FullThrottle, 0);
            Assert.IsFalse(sandstormCommand.EmergencyStop);

            // The operator now selects Falafel, which has been reporting a
            // stop since before Sandstorm's own tick above.
            StationCommand falafelCommand = controller.NextDriveCommand(
                FalafelId, true, falafelReportedStopped, selectFalafelTick, FullThrottle, FullThrottle);

            Assert.IsTrue(falafelCommand.EmergencyStop,
                          "Falafel's genuinely-reported stop was suppressed by Sandstorm's unrelated tick.");
            Assert.IsTrue(controller.IsEmergencyStopLatched);
        }

        [TestMethod]
        public void VehicleReportsStopped_TrueWhenTheVehicleItselfReportsStopped()
        {
            Assert.IsTrue(DriveController.VehicleReportsStopped(true, Now, Now));
        }

        [TestMethod]
        public void VehicleReportsStopped_TrueWhenTheVehicleHasNeverReported()
        {
            Assert.IsTrue(DriveController.VehicleReportsStopped(false, DateTime.MinValue, Now));
        }

        [TestMethod]
        public void VehicleReportsStopped_TrueWhenTheLastFrameIsAtLeastTwoSecondsOld()
        {
            DateTime now = Now;
            DateTime lastFrame = now - TimeSpan.FromSeconds(2);

            Assert.IsTrue(DriveController.VehicleReportsStopped(false, lastFrame, now));
        }

        [TestMethod]
        public void VehicleReportsStopped_FalseForARecentFrameThatDoesNotReportAStop()
        {
            DateTime now = Now;
            DateTime lastFrame = now - TimeSpan.FromMilliseconds(200);

            Assert.IsFalse(DriveController.VehicleReportsStopped(false, lastFrame, now));
        }

        private static DriveController ArmedController()
        {
            DriveController controller = new DriveController();

            StationCommand command;
            Assert.IsTrue(controller.TryToggleArm(FalafelId, false, Now, Now, 0, out command));
            Assert.IsTrue(controller.IsArmed);

            return controller;
        }
    }
}
