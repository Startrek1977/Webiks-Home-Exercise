using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Telemetry;
using RoverRally.Tests.TestSupport;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RoverRally.Tests
{
    [TestClass]
    public class TelemetryClientTests
    {
        /// <summary>
        /// First-ever coverage of TelemetryClient (#22). Start(0) asks the OS
        /// for an ephemeral port, so this can't collide with anything else
        /// running on the machine. The "listener started" log happens
        /// synchronously in Start() itself, before the background listener
        /// thread's socket is even opened, so this is deterministic without
        /// waiting on that thread.
        /// </summary>
        [TestMethod]
        public void StartLogsThatTheListenerStarted()
        {
            CapturingLogger logger = new CapturingLogger();
            TelemetryClient client = new TelemetryClient(logger);

            try
            {
                client.Start(0);

                Assert.IsTrue(logger.HasEntry(LogLevel.Information, "Telemetry listener started"));
            }
            finally
            {
                client.Stop();
            }
        }

        [TestMethod]
        public void ASubscriberExceptionIsLoggedAndDoesNotKillTheListener()
        {
            CapturingLogger logger = new CapturingLogger();
            TelemetryClient client = new TelemetryClient(logger);
            int port = ReserveUdpPort();
            int receivedCount = 0;

            client.FrameReceived += delegate
            {
                Interlocked.Increment(ref receivedCount);
                throw new InvalidOperationException("boom");
            };

            try
            {
                client.Start(port);
                Thread.Sleep(150);

                using (UdpClient sender = new UdpClient())
                {
                    byte[] first = SimulatorFrameWriter.WriteTelemetry(new TelemetryReading
                    {
                        RoverId = 7,
                        Sequence = 1,
                        TimestampMs = 1,
                        LatitudeE7 = 322800000,
                        LongitudeE7 = 349200000,
                        HeadingDeci = 900,
                        SpeedCmS = 250,
                        BatteryMilliVolts = 12000,
                        SignalPercent = 80,
                        MotorTempDeciC = 200,
                        TiltDeciDeg = 0,
                        StatusFlags = 0x08
                    });

                    byte[] second = SimulatorFrameWriter.WriteTelemetry(new TelemetryReading
                    {
                        RoverId = 7,
                        Sequence = 2,
                        TimestampMs = 2,
                        LatitudeE7 = 322800000,
                        LongitudeE7 = 349200000,
                        HeadingDeci = 900,
                        SpeedCmS = 250,
                        BatteryMilliVolts = 12000,
                        SignalPercent = 80,
                        MotorTempDeciC = 200,
                        TiltDeciDeg = 0,
                        StatusFlags = 0x08
                    });

                    sender.Send(first, first.Length, new IPEndPoint(IPAddress.Loopback, port));
                    sender.Send(second, second.Length, new IPEndPoint(IPAddress.Loopback, port));
                }

                SpinWait.SpinUntil(() => Volatile.Read(ref receivedCount) >= 2, TimeSpan.FromSeconds(3));

                Assert.AreEqual(2, receivedCount);
                Assert.IsTrue(logger.HasEntry(LogLevel.Error, "Telemetry subscriber threw; frame dropped."));
            }
            finally
            {
                client.Stop();
            }
        }

        private static int ReserveUdpPort()
        {
            using (UdpClient socket = new UdpClient(0))
            {
                return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
            }
        }
    }
}
