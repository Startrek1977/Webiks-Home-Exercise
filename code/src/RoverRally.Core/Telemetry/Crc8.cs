using System;

namespace RoverRally.Core.Telemetry
{
    /// <summary>
    /// CRC-8 checksum used by every RoverLink frame: polynomial 0x07, initial
    /// value 0x00, no final XOR, most significant bit first. The same routine
    /// covers telemetry and command frames.
    /// </summary>
    public static class Crc8
    {
        private const byte Polynomial = 0x07;
        private const byte InitialValue = 0x00;

        /// <summary>
        /// Computes the checksum over <paramref name="count"/> bytes of
        /// <paramref name="buffer"/> starting at <paramref name="offset"/>.
        /// An empty range checksums to zero.
        /// </summary>
        public static byte Compute(byte[]? buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "The offset cannot be negative.");
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "The count cannot be negative.");

            // Written as a subtraction rather than "offset + count > buffer.Length"
            // so that a large offset or count cannot overflow past the check and
            // reach the indexer below.
            if (offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(count), "The range runs past the end of the buffer.");

            byte crc = InitialValue;

            for (int i = 0; i < count; i++)
            {
                crc ^= buffer[offset + i];

                for (int bit = 0; bit < 8; bit++)
                {
                    // The cast back to byte is what keeps the register eight
                    // bits wide; the shift on its own promotes to int.
                    crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ Polynomial) : (byte)(crc << 1);
                }
            }

            return crc;
        }
    }
}
