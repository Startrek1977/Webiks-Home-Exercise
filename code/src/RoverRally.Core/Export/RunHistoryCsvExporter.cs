using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RoverRally.Core.Session;

namespace RoverRally.Core.Export
{
    /// <summary>
    /// Formats completed-run history (<see cref="SessionCacheRecord"/>) as
    /// CSV for the Fleet tab's export feature (#32). Kept free of any WPF or
    /// view-model dependency - the caller supplies rover-name resolution -
    /// so it can be exercised directly from <c>RoverRally.Tests</c>.
    /// Every number and timestamp is formatted with
    /// <see cref="CultureInfo.InvariantCulture"/> regardless of the running
    /// thread's culture, since a comma decimal separator would corrupt the
    /// file for a locale that uses one.
    /// </summary>
    public static class RunHistoryCsvExporter
    {
        private const string Header = "RoverId,RoverName,StartedUtc,EndedUtc,DurationSeconds,DistanceMeters,PeakSpeedKmh";

        public static string BuildCsv(IEnumerable<SessionCacheRecord> records, Func<int, string> roverNameResolver)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (roverNameResolver == null) throw new ArgumentNullException(nameof(roverNameResolver));

            StringBuilder csv = new StringBuilder();
            csv.Append(Header).Append("\r\n");

            foreach (SessionCacheRecord record in records)
            {
                csv.Append(BuildRow(record, roverNameResolver)).Append("\r\n");
            }

            return csv.ToString();
        }

        private static string BuildRow(SessionCacheRecord record, Func<int, string> roverNameResolver)
        {
            string roverName = roverNameResolver(record.RoverId) ?? string.Empty;

            string[] fields = new[]
            {
                record.RoverId.ToString(CultureInfo.InvariantCulture),
                QuoteField(roverName),
                record.StartedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                record.EndedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                record.Duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                (record.DistanceCm / 100.0).ToString("0.0", CultureInfo.InvariantCulture),
                (record.PeakSpeedCmS * 0.036).ToString("0.0", CultureInfo.InvariantCulture)
            };

            return string.Join(",", fields);
        }

        private static string QuoteField(string value)
        {
            if (!string.IsNullOrEmpty(value) && StartsWithFormulaTrigger(value[0]))
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static bool StartsWithFormulaTrigger(char firstCharacter)
        {
            return firstCharacter == '='
                || firstCharacter == '+'
                || firstCharacter == '-'
                || firstCharacter == '@'
                || firstCharacter == '\t'
                || firstCharacter == '\n'
                || firstCharacter == '\r';
        }
    }
}
