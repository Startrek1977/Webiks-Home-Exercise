using Microsoft.Extensions.Logging;
using RoverRally.Core.Logging;

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
        /// Delegates to the <see cref="ILogger"/> overload (#22) using the
        /// static facade's own logger, so this call site's behaviour is
        /// unchanged and production readings still get the clamp warning.
        /// Resolved fresh on every call, not cached, so it always reflects
        /// the facade's current configuration rather than whatever it was
        /// the first time this class was touched.
        /// </summary>
        public static int ToPercent(int milliVolts)
        {
            return ToPercent(milliVolts, Log.CreateLogger(nameof(BatteryGauge)));
        }

        /// <summary>
        /// Same conversion as <see cref="ToPercent(int)"/>, but logs a
        /// Warning when the raw computed percentage falls outside 0-100
        /// before clamping - in either direction, that means the pack
        /// reading itself is implausible (a failed sensor, a miswired cell),
        /// not that the pack is simply flat or full.
        /// </summary>
        public static int ToPercent(int milliVolts, ILogger logger)
        {
            int aboveEmpty = milliVolts - EmptyMilliVolts;
            int percent = aboveEmpty * 100 / (FullMilliVolts - EmptyMilliVolts);

            if (percent < 0)
            {
                logger.LogWarning("Battery reading {MilliVolts} mV computed to {Percent}% before clamping; a sensor may be failing.", milliVolts, percent);
                percent = 0;
            }
            else if (percent > 100)
            {
                logger.LogWarning("Battery reading {MilliVolts} mV computed to {Percent}% before clamping; a sensor may be failing.", milliVolts, percent);
                percent = 100;
            }

            return percent;
        }

        public static bool IsCritical(int milliVolts)
        {
            return ToPercent(milliVolts) <= 15;
        }
    }
}
