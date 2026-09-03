namespace RoverRally.Core.Units
{
    /// <summary>
    /// Converts pack voltage to a charge percentage. The rovers run a 3S
    /// lithium pack: 12.6V straight off the charger, 9.0V is the cutoff the
    /// controller enforces before it shuts the motors down.
    /// </summary>
    public static class BatteryGauge
    {
        public const int EmptyMilliVolts = 9000;
        public const int FullMilliVolts = 12600;

        /// <summary>
        /// A pack at or below <see cref="EmptyMilliVolts"/> - including a
        /// failed sensor reporting 0 mV - clamps to 0, not a wrapped-around
        /// positive number. Never let this report a flat pack as healthy.
        /// </summary>
        public static int ToPercent(int milliVolts)
        {
            int aboveEmpty = milliVolts - EmptyMilliVolts;
            int percent = aboveEmpty * 100 / (FullMilliVolts - EmptyMilliVolts);

            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            return percent;
        }

        public static bool IsCritical(int milliVolts)
        {
            return ToPercent(milliVolts) <= 15;
        }
    }
}
