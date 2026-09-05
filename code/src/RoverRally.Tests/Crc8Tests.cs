using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;

namespace RoverRally.Tests
{
    [TestClass]
    public class Crc8Tests
    {
        [TestMethod]
        public void MatchesTheWorkedExampleFromTheProtocolNotes()
        {
            byte[] buffer = new byte[] { 0x01, 0x02, 0x03 };

            byte crc = Crc8.Compute(buffer, 0, buffer.Length);

            Assert.AreEqual((byte)0x48, crc);
        }

        [TestMethod]
        public void MatchesTheSimulatorForEveryByteValue()
        {
            for (int value = 0; value <= byte.MaxValue; value++)
            {
                byte[] buffer = new byte[] { (byte)value };

                Assert.AreEqual(SimulatorFrameWriter.Crc8(buffer, 0, 1), Crc8.Compute(buffer, 0, 1),
                                "CRC differs for the single byte 0x" + value.ToString("X2") + ".");
            }
        }

        [TestMethod]
        public void MatchesTheSimulatorOverBuffersOfEveryLength()
        {
            Random random = new Random(20260901);

            for (int length = 0; length <= 64; length++)
            {
                byte[] buffer = new byte[length];
                random.NextBytes(buffer);

                Assert.AreEqual(SimulatorFrameWriter.Crc8(buffer, 0, length), Crc8.Compute(buffer, 0, length),
                                "CRC differs for a buffer of " + length + " bytes.");
            }
        }

        [TestMethod]
        public void ChecksumsOnlyTheRequestedRange()
        {
            byte[] padded = new byte[] { 0xAA, 0xBB, 0x01, 0x02, 0x03, 0xCC };

            byte crc = Crc8.Compute(padded, 2, 3);

            Assert.AreEqual((byte)0x48, crc);
        }

        [TestMethod]
        public void AnEmptyRangeChecksumsToZero()
        {
            byte crc = Crc8.Compute(new byte[] { 0xFF, 0xFF }, 1, 0);

            Assert.AreEqual((byte)0x00, crc);
        }

        [TestMethod]
        public void RejectsANullBuffer()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => Crc8.Compute(null!, 0, 0));
        }

        [TestMethod]
        public void RejectsANegativeOffset()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc8.Compute(new byte[4], -1, 2));
        }

        [TestMethod]
        public void RejectsANegativeCount()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc8.Compute(new byte[4], 0, -1));
        }

        [TestMethod]
        public void RejectsARangeThatRunsPastTheEndOfTheBuffer()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc8.Compute(new byte[4], 2, 3));
        }

        [TestMethod]
        public void RejectsAnOffsetLargeEnoughToOverflowTheBoundsCheck()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc8.Compute(new byte[4], int.MaxValue, 1));
        }

        [TestMethod]
        public void RejectsACountLargeEnoughToOverflowTheBoundsCheck()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc8.Compute(new byte[4], 1, int.MaxValue));
        }
    }
}
