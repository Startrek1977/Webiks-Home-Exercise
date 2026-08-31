using System;

namespace RoverRally.Core.Units
{
    public enum SpeedUnit
    {
        KilometresPerHour = 0,
        MilesPerHour = 1
    }

    /// <summary>
    /// The RL-100 reports ground speed in centimetres per second. The station
    /// shows km/h by default; the imperial readout was added for the visiting
    /// customer trials in 2019.
    /// </summary>
    public static class SpeedConverter
    {
        private const double CentimetresPerSecondToKmH = 0.036;
        private const double KmHToMilesPerHour = 0.621371;

        public static double ToKilometresPerHour(int centimetresPerSecond)
        {
            return centimetresPerSecond * CentimetresPerSecondToKmH;
        }

        public static double ToMilesPerHour(int centimetresPerSecond)
        {
            return ToKilometresPerHour(centimetresPerSecond) * KmHToMilesPerHour;
        }

        public static string Format(int centimetresPerSecond, SpeedUnit unit)
        {
            double kmh = ToKilometresPerHour(centimetresPerSecond);

            if (unit == SpeedUnit.MilesPerHour)
            {
                return string.Format("{0:0.0} mph", ToMilesPerHour((int)Math.Round(kmh)));
            }

            return string.Format("{0:0.0} km/h", kmh);
        }
    }
}
