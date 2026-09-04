using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoverRally.Core.Configuration;
using RoverRally.Core.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Log = RoverRally.Core.Logging.Log;

namespace RoverRally.Tests
{
    [TestClass]
    public class LogTests
    {
        /// <summary>
        /// Demonstrates the worst-case footprint claimed in SOLUTION_LOG.md
        /// (Log.FileSizeLimitBytes x Log.RetainedFileCountLimit) rather than
        /// just asserting it in prose (#22, AC "demonstrated rather than
        /// assumed"). Uses its own small size/retention limits - not the
        /// production 5 MB x 10 constants - so the test forces many rolls
        /// without writing tens of megabytes; the bound being tested is the
        /// same mechanism (Serilog's rolling file retention) either way.
        /// </summary>
        [TestMethod]
        public void WritingManyLogEntriesNeverExceedsTheConfiguredRetentionFootprint()
        {
            string directory = Path.Combine(Path.GetTempPath(), "roverrally-log-footprint-" + Guid.NewGuid().ToString("N"));
            const long sizeLimitBytes = 2048;
            const int retainedFileCountLimit = 3;
            long bound = sizeLimitBytes * retainedFileCountLimit;

            try
            {
                Serilog.Core.Logger logger = new Serilog.LoggerConfiguration()
                    .MinimumLevel.Is(Serilog.Events.LogEventLevel.Debug)
                    .WriteTo.File(
                        Path.Combine(directory, "test-.log"),
                        rollingInterval: Serilog.RollingInterval.Day,
                        fileSizeLimitBytes: sizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCountLimit)
                    .CreateLogger();

                try
                {
                    // 5000 lines at roughly 80 bytes each is on the order of
                    // 400 KB written - about two orders of magnitude past the
                    // 6 KB retained bound below - so the size assertion only
                    // passes if retention actually deleted the older rolled
                    // files, not because too little was written to matter.
                    for (int i = 0; i < 5000; i++)
                    {
                        logger.Information("Log line {Index} padded to force several rolls of a deliberately tiny file.", i);
                    }
                }
                finally
                {
                    logger.Dispose();
                }

                string[] survivingFiles = Directory.GetFiles(directory);
                long totalBytesOnDisk = 0;
                foreach (string file in survivingFiles)
                {
                    totalBytesOnDisk += new FileInfo(file).Length;
                }

                Assert.IsTrue(survivingFiles.Length <= retainedFileCountLimit,
                    "Expected at most " + retainedFileCountLimit + " retained files, found " + survivingFiles.Length + ".");
                Assert.IsTrue(totalBytesOnDisk <= bound,
                    "Total on-disk footprint (" + totalBytesOnDisk + " bytes across " + survivingFiles.Length +
                    " files) exceeded the configured " + sizeLimitBytes + " x " + retainedFileCountLimit + " = " + bound + " byte bound.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// A file sitting where a directory is expected makes the File sink's
        /// own Directory.CreateDirectory fail during Configure - exactly the
        /// "failure to open the log file" case the AC requires the station to
        /// survive.
        /// </summary>
        [TestMethod]
        public void ConfigureDoesNotThrowWhenTheLogDirectoryCannotBeCreated()
        {
            string blockingFilePath = Path.Combine(Path.GetTempPath(), "roverrally-log-blocker-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blockingFilePath, "not a directory");
            string unusableLogDirectory = Path.Combine(blockingFilePath, "logs");

            StationOptions options = new StationOptions
            {
                LogLevel = RoverRally.Core.Logging.LogLevel.Info,
                LogDirectory = unusableLogDirectory
            };

            try
            {
                Log.Configure(options);
                Log.Info("Still logging after a failed file sink.");
            }
            finally
            {
                Log.Shutdown();
                File.Delete(blockingFilePath);
            }
        }

        /// <summary>
        /// Reproduces the pre-#22 Console-only facade behaviour for any
        /// caller of CreateLogger that runs without Configure having been
        /// called first - which is almost every other test in this project.
        /// </summary>
        [TestMethod]
        public void CreateLoggerFallsBackToTheConsoleFacadeWhenConfigureWasNotCalled()
        {
            TextWriter originalOut = Console.Out;
            StringWriter capturedOut = new StringWriter();
            Console.SetOut(capturedOut);

            try
            {
                ILogger logger = Log.CreateLogger<LogTests>();
                logger.LogInformation("Sentinel message for the unconfigured fallback path.");
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            StringAssert.Contains(capturedOut.ToString(), "Sentinel message for the unconfigured fallback path.");
        }
    }
}
