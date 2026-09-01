using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    [TestClass]
    public class FrameCodecTests
    {
        /// <summary>
        /// Written by the simulator's own FrameWriter, not by the mirror in
        /// this project, so it catches a mistake copied into both sides.
        /// </summary>
        private static readonly byte[] GoldenTelemetryFrame = new byte[]
        {
            0x52, 0x4C, 0x01, 0x07, 0xFA, 0xFF, 0xFF, 0xFF, 0x7B, 0x7C, 0x29, 0x1F,
            0x94, 0x01, 0x00, 0x00, 0x4B, 0xF2, 0xFD, 0xEB, 0xCE, 0xF9, 0x8B, 0x59,
            0x0F, 0x0E, 0xD2, 0x04, 0xE0, 0x60, 0x64, 0x65, 0xFF, 0x2E, 0xFB, 0x0B, 0x14
        };

        /// <summary>
        /// Assembled from the protocol notes, checksummed with the simulator's
        /// own CRC routine, and confirmed to be accepted by the simulator's
        /// TryReadCommand. Rover 7, throttle -450, steering 875, armed.
        /// </summary>
        private static readonly byte[] GoldenCommandFrame = new byte[]
        {
            0x52, 0x43, 0x01, 0x07, 0x3E, 0xFE, 0x6B, 0x03, 0x02, 0x5F
        };

        private static TelemetryReading AReading()
        {
            TelemetryReading reading = new TelemetryReading();

            reading.RoverId = 7;
            reading.Sequence = 4294967290;
            reading.TimestampMs = 1735689600123;
            reading.LatitudeE7 = -335678901;
            reading.LongitudeE7 = 1502345678;
            reading.HeadingDeci = 3599;
            reading.SpeedCmS = 1234;
            reading.BatteryMilliVolts = 24800;
            reading.SignalPercent = 100;
            reading.MotorTempDeciC = -155;
            reading.TiltDeciDeg = -1234;
            reading.StatusFlags = 0x0B;

            return reading;
        }

        [TestMethod]
        public void DecodesEveryFieldOfAFrameTheSimulatorWrote()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());

            TelemetryFrame frame;
            bool decoded = FrameCodec.TryDecode(datagram, datagram.Length, out frame);

            Assert.IsTrue(decoded);
            Assert.AreEqual((byte)7, frame.RoverId);
            Assert.AreEqual(4294967290u, frame.Sequence);
            Assert.AreEqual(1735689600123ul, frame.TimestampMs);
            Assert.AreEqual(-335678901, frame.LatitudeE7);
            Assert.AreEqual(1502345678, frame.LongitudeE7);
            Assert.AreEqual((ushort)3599, frame.HeadingDeci);
            Assert.AreEqual((ushort)1234, frame.SpeedCmS);
            Assert.AreEqual((ushort)24800, frame.BatteryMilliVolts);
            Assert.AreEqual((byte)100, frame.SignalPercent);
            Assert.AreEqual((short)-155, frame.MotorTempDeciC);
            Assert.AreEqual((short)-1234, frame.TiltDeciDeg);
            Assert.AreEqual((byte)0x0B, frame.StatusFlags);
        }

        [TestMethod]
        public void TheMirrorAgreesWithTheGoldenFrameCapturedFromTheSimulator()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());

            CollectionAssert.AreEqual(GoldenTelemetryFrame, datagram);
        }

        [TestMethod]
        public void DecodesTheGoldenFrameCapturedFromTheSimulator()
        {
            TelemetryFrame frame;
            bool decoded = FrameCodec.TryDecode(GoldenTelemetryFrame, GoldenTelemetryFrame.Length, out frame);

            Assert.IsTrue(decoded);
            Assert.AreEqual((byte)7, frame.RoverId);
            Assert.AreEqual((ushort)1234, frame.SpeedCmS);
            Assert.AreEqual((short)-1234, frame.TiltDeciDeg);
            Assert.IsTrue(frame.IsArmed);
            Assert.IsTrue(frame.IsEmergencyStopped);
            Assert.IsFalse(frame.IsCharging);
            Assert.IsTrue(frame.HasGpsFix);
        }

        [TestMethod]
        public void DecodesAFrameSentWithoutAGpsFix()
        {
            TelemetryReading reading = AReading();
            reading.StatusFlags = 0x01;
            reading.LatitudeE7 = 0;
            reading.LongitudeE7 = 0;

            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(reading);

            TelemetryFrame frame;
            bool decoded = FrameCodec.TryDecode(datagram, datagram.Length, out frame);

            Assert.IsTrue(decoded);
            Assert.IsFalse(frame.HasGpsFix);
            Assert.AreEqual(0, frame.LatitudeE7);
            Assert.AreEqual(0, frame.LongitudeE7);
        }

        [TestMethod]
        public void DecodesEveryCombinationOfTheStatusFlags()
        {
            for (byte flags = 0x00; flags <= 0x0F; flags++)
            {
                TelemetryReading reading = AReading();
                reading.StatusFlags = flags;

                byte[] datagram = SimulatorFrameWriter.WriteTelemetry(reading);

                TelemetryFrame frame;
                Assert.IsTrue(FrameCodec.TryDecode(datagram, datagram.Length, out frame));

                string because = " for status flags 0x" + flags.ToString("X2") + ".";
                Assert.AreEqual((flags & 0x01) != 0, frame.IsArmed, "IsArmed differs" + because);
                Assert.AreEqual((flags & 0x02) != 0, frame.IsEmergencyStopped, "IsEmergencyStopped differs" + because);
                Assert.AreEqual((flags & 0x04) != 0, frame.IsCharging, "IsCharging differs" + because);
                Assert.AreEqual((flags & 0x08) != 0, frame.HasGpsFix, "HasGpsFix differs" + because);
            }
        }

        [TestMethod]
        public void RejectsAFrameOfTheWrongLength()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());

            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(datagram, 36, out frame), "A short frame was accepted.");
            Assert.IsFalse(FrameCodec.TryDecode(datagram, 38, out frame), "An overlong frame was accepted.");
            Assert.IsFalse(FrameCodec.TryDecode(new byte[0], 0, out frame), "An empty datagram was accepted.");
        }

        [TestMethod]
        public void RejectsAFrameWithTheWrongMarker()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());
            datagram[1] = 0x43; // the command marker, not the telemetry one
            datagram[36] = SimulatorFrameWriter.Crc8(datagram, 0, 36);

            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(datagram, datagram.Length, out frame));
        }

        [TestMethod]
        public void RejectsAnUnsupportedProtocolVersion()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());
            datagram[2] = 0x02;
            datagram[36] = SimulatorFrameWriter.Crc8(datagram, 0, 36);

            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(datagram, datagram.Length, out frame));
        }

        [TestMethod]
        public void RejectsAFrameWhoseChecksumDoesNotMatch()
        {
            byte[] datagram = SimulatorFrameWriter.WriteTelemetry(AReading());
            datagram[26] ^= 0x01; // corrupt the speed and leave the checksum alone

            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(datagram, datagram.Length, out frame));
        }

        [TestMethod]
        public void RejectsADatagramShorterThanTheStatedLength()
        {
            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(new byte[10], 37, out frame));
        }

        [TestMethod]
        public void RejectsANullDatagram()
        {
            TelemetryFrame frame;

            Assert.IsFalse(FrameCodec.TryDecode(null, 37, out frame));
        }

        [TestMethod]
        public void LeavesNoFrameBehindWhenDecodingFails()
        {
            TelemetryFrame frame;

            FrameCodec.TryDecode(new byte[37], 37, out frame);

            Assert.IsNull(frame);
        }

        [TestMethod]
        public void EncodesACommandTheSimulatorAccepts()
        {
            byte[] datagram = FrameCodec.EncodeCommand(3, -450, 875, false, true);

            DriveCommand command;
            bool read = SimulatorFrameWriter.TryReadCommand(datagram, out command);

            Assert.IsTrue(read);
            Assert.AreEqual(10, datagram.Length);
            Assert.AreEqual((byte)3, command.RoverId);
            Assert.AreEqual((short)-450, command.Throttle);
            Assert.AreEqual((short)875, command.Steering);
            Assert.IsFalse(command.EmergencyStop);
            Assert.IsTrue(command.Armed);
        }

        [TestMethod]
        public void EncodesTheGoldenCommandFrameByteForByte()
        {
            byte[] datagram = FrameCodec.EncodeCommand(7, -450, 875, false, true);

            CollectionAssert.AreEqual(GoldenCommandFrame, datagram);
        }

        [TestMethod]
        public void ClampsThrottleAndSteeringToTheControllerRange()
        {
            AssertClamped(short.MaxValue, 1000);
            AssertClamped(1001, 1000);
            AssertClamped(short.MinValue, -1000);
            AssertClamped(-1001, -1000);
            AssertClamped(1000, 1000);
            AssertClamped(-1000, -1000);
            AssertClamped(0, 0);
        }

        [TestMethod]
        public void SetsTheEmergencyStopAndArmFlagBits()
        {
            AssertFlags(false, false);
            AssertFlags(true, false);
            AssertFlags(false, true);
            AssertFlags(true, true);
        }

        private static void AssertClamped(short requested, short expected)
        {
            byte[] datagram = FrameCodec.EncodeCommand(1, requested, requested, false, false);

            DriveCommand command;
            Assert.IsTrue(SimulatorFrameWriter.TryReadCommand(datagram, out command));

            Assert.AreEqual(expected, command.Throttle, "Throttle " + requested + " was not clamped.");
            Assert.AreEqual(expected, command.Steering, "Steering " + requested + " was not clamped.");
        }

        private static void AssertFlags(bool emergencyStop, bool armed)
        {
            byte[] datagram = FrameCodec.EncodeCommand(1, 0, 0, emergencyStop, armed);

            DriveCommand command;
            Assert.IsTrue(SimulatorFrameWriter.TryReadCommand(datagram, out command));

            Assert.AreEqual(emergencyStop, command.EmergencyStop);
            Assert.AreEqual(armed, command.Armed);
        }
    }
}
