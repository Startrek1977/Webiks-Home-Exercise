using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Models;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    /// <summary>
    /// Rover.ApplyFrame is where a no-fix frame either does or doesn't get to
    /// move the vehicle on the map (#36). The RL-100 zeroes latitude and
    /// longitude when the receiver has no fix, so every test here checks that
    /// Position only moves on a frame that actually has one, while everything
    /// else the frame carries - speed, heading, battery, status - still lands
    /// regardless of whether this frame has a fix.
    /// </summary>
    [TestClass]
    public class RoverTests
    {
        private const byte ArmedFlag = 0x01;
        private const byte GpsFixFlag = 0x08;

        private static readonly DateTime ReceivedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void AppliesPositionFromAFrameWithAFix()
        {
            Rover rover = new Rover { Id = 1 };

            rover.ApplyFrame(Frame(322800000, 349200000, hasFix: true), ReceivedUtc);

            Assert.AreEqual(32.28, rover.Position.Latitude, 0.0000001);
            Assert.AreEqual(34.92, rover.Position.Longitude, 0.0000001);
        }

        [TestMethod]
        public void LeavesThePositionUnchangedWhenTheFrameHasNoFix()
        {
            Rover rover = new Rover { Id = 1 };
            rover.ApplyFrame(Frame(322800000, 349200000, hasFix: true), ReceivedUtc);

            rover.ApplyFrame(Frame(0, 0, hasFix: false), ReceivedUtc);

            Assert.AreEqual(32.28, rover.Position.Latitude, 0.0000001,
                            "A no-fix frame's zeroed coordinates are not a position and must not move the rover.");
            Assert.AreEqual(34.92, rover.Position.Longitude, 0.0000001);
        }

        /// <summary>
        /// The exact scenario the issue asks for: a no-fix frame arriving
        /// between two good ones must be transparent to position tracking -
        /// the rover holds its last known spot through the gap and picks up
        /// normally on the next fix.
        /// </summary>
        [TestMethod]
        public void ANoFixFrameBetweenTwoGoodFramesDoesNotMoveTheRover()
        {
            Rover rover = new Rover { Id = 1 };

            rover.ApplyFrame(Frame(322800000, 349200000, hasFix: true), ReceivedUtc);
            TrackPoint afterFirstFix = rover.Position;

            rover.ApplyFrame(Frame(0, 0, hasFix: false), ReceivedUtc);

            Assert.AreEqual(afterFirstFix.Latitude, rover.Position.Latitude, 0.0000001,
                            "The no-fix frame moved the rover.");
            Assert.AreEqual(afterFirstFix.Longitude, rover.Position.Longitude, 0.0000001,
                            "The no-fix frame moved the rover.");

            rover.ApplyFrame(Frame(322810000, 349210000, hasFix: true), ReceivedUtc);

            Assert.AreEqual(32.281, rover.Position.Latitude, 0.0000001,
                            "The next good frame should resume moving the rover.");
            Assert.AreEqual(34.921, rover.Position.Longitude, 0.0000001,
                            "The next good frame should resume moving the rover.");
        }

        [TestMethod]
        public void StillAppliesNonPositionalFieldsFromANoFixFrame()
        {
            Rover rover = new Rover { Id = 1 };

            TelemetryFrame frame = Frame(0, 0, hasFix: false, speedCmS: 150, headingDeci: 900,
                                          batteryMilliVolts: 11800, signalPercent: 77,
                                          motorTempDeciC: 320, tiltDeciDeg: 15, armed: true);

            rover.ApplyFrame(frame, ReceivedUtc);

            Assert.AreEqual(150, rover.SpeedCmS);
            Assert.AreEqual(90.0, rover.Heading, 0.0001);
            Assert.AreEqual(11800, rover.BatteryMilliVolts);
            Assert.AreEqual(77, rover.SignalPercent);
            Assert.AreEqual(32.0, rover.MotorTemperatureC, 0.0001);
            Assert.AreEqual(1.5, rover.TiltDegrees, 0.0001);
            Assert.IsTrue(rover.IsArmed);
            Assert.AreEqual(ReceivedUtc, rover.LastFrameUtc);
            Assert.AreEqual(RoverStatus.Driving, rover.Status);
        }

        private static TelemetryFrame Frame(int latitudeE7, int longitudeE7, bool hasFix,
                                             ushort speedCmS = 0, ushort headingDeci = 0,
                                             ushort batteryMilliVolts = 12000, byte signalPercent = 100,
                                             short motorTempDeciC = 0, short tiltDeciDeg = 0, bool armed = false)
        {
            byte flags = 0;
            if (hasFix) flags |= GpsFixFlag;
            if (armed) flags |= ArmedFlag;

            return new TelemetryFrame(1, 1, 0, latitudeE7, longitudeE7, headingDeci, speedCmS,
                                       batteryMilliVolts, signalPercent, motorTempDeciC, tiltDeciDeg, flags);
        }
    }
}
