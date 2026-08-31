using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using RoverRally.Core.Logging;

namespace RoverRally.Core.Monitoring
{
    /// <summary>
    /// Publishes the live session so the overview client in the office can
    /// attach to a running station. The endpoint is configured per site with
    /// the StationMonitorEndpoint setting.
    /// </summary>
    public class StationMonitorService : MarshalByRefObject
    {
        private static TcpChannel _channel;
        private static readonly object ChannelLock = new object();

        private readonly List<RunSnapshot> _published = new List<RunSnapshot>();

        public string StationName { get; set; }

        public static StationMonitorService Publish(string endpoint, int port)
        {
            lock (ChannelLock)
            {
                if (_channel == null)
                {
                    IDictionary properties = new Hashtable();
                    properties["port"] = port;
                    properties["name"] = "RoverRallyStationMonitor";
                    properties["secure"] = false;

                    _channel = new TcpChannel(properties, null, null);
                    ChannelServices.RegisterChannel(_channel, false);
                }
            }

            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(StationMonitorService),
                endpoint,
                WellKnownObjectMode.Singleton);

            Log.Info("Station monitor published at " + endpoint + " on port " + port + ".");

            return new StationMonitorService();
        }

        public static void Unpublish()
        {
            lock (ChannelLock)
            {
                if (_channel == null) return;

                ChannelServices.UnregisterChannel(_channel);
                _channel = null;
            }

            Log.Info("Station monitor withdrawn.");
        }

        public void Push(RunSnapshot snapshot)
        {
            lock (_published)
            {
                _published.Add(snapshot);

                while (_published.Count > 240)
                {
                    _published.RemoveAt(0);
                }
            }
        }

        public byte[] GetLatest()
        {
            lock (_published)
            {
                if (_published.Count == 0) return null;
                return _published[_published.Count - 1].Serialize();
            }
        }

        public byte[][] GetSince(DateTime capturedAfterUtc)
        {
            lock (_published)
            {
                List<byte[]> matches = new List<byte[]>();

                foreach (RunSnapshot snapshot in _published)
                {
                    if (snapshot.CapturedUtc > capturedAfterUtc)
                    {
                        matches.Add(snapshot.Serialize());
                    }
                }

                return matches.ToArray();
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
