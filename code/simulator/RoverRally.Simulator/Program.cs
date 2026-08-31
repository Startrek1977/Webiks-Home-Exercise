using System.Net;
using System.Net.Sockets;
using RoverRally.Simulator;

// Stands in for the RL-100 base station and the vehicles behind it. Telemetry
// goes out on one UDP port, drive commands come back on another.

int telemetryPort = ArgOr("--telemetry-port", 14550);
int commandPort = ArgOr("--command-port", 14551);
string host = ArgOr("--host", "127.0.0.1");
int rateHz = ArgOr("--rate", 5);

RoverSim[] fleet =
[
    new RoverSim(1, "Falafel",        cruiseSpeedCmS: 380, startOffset: 0,    takesWideLines: false, seed: 11),
    new RoverSim(2, "Dune Goblin",    cruiseSpeedCmS: 520, startOffset: 260,  takesWideLines: false, seed: 22),
    new RoverSim(3, "Turbo Tortoise", cruiseSpeedCmS: 240, startOffset: 620,  takesWideLines: false, seed: 33),
    new RoverSim(4, "Sandstorm",      cruiseSpeedCmS: 560, startOffset: 900,  takesWideLines: true,  seed: 44),
    new RoverSim(5, "Mishmish",       cruiseSpeedCmS: 300, startOffset: 1180, takesWideLines: false, seed: 55)
];

using UdpClient telemetry = new();
using UdpClient commands = new(commandPort);

Console.WriteLine($"RoverRally simulator: {fleet.Length} rovers");
Console.WriteLine($"  telemetry -> {host}:{telemetryPort} at {rateHz} Hz");
Console.WriteLine($"  commands  <- udp/{commandPort}");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

CancellationTokenSource stopping = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };

_ = Task.Run(async () =>
{
    while (!stopping.IsCancellationRequested)
    {
        try
        {
            UdpReceiveResult received = await commands.ReceiveAsync(stopping.Token);

            if (!FrameWriter.TryReadCommand(received.Buffer, out DriveCommand command))
            {
                continue;
            }

            RoverSim? target = fleet.FirstOrDefault(r => r.Id == command.RoverId);
            if (target is null) continue;

            if (target.Apply(command))
            {
                Console.WriteLine($"  !! emergency stop received for {target.Name} — brakes on");
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  command channel: {ex.Message}");
        }
    }
});

TimeSpan interval = TimeSpan.FromSeconds(1.0 / rateHz);
PeriodicTimer ticker = new(interval);

uint sequence = 0;
int tick = 0;
DateTime lastReport = DateTime.UtcNow;

while (await ticker.WaitForNextTickAsync(CancellationToken.None))
{
    if (stopping.IsCancellationRequested) break;

    tick++;
    sequence++;

    foreach (RoverSim rover in fleet)
    {
        rover.Advance(interval.TotalSeconds);

        // The receiver drops its fix every so often, and the pack voltage
        // sense on the older chassis occasionally fails to read.
        bool noFix = tick % 40 == rover.Id;
        bool batterySenseFailed = tick % 55 == rover.Id;

        byte[] frame = FrameWriter.WriteTelemetry(rover.Read(sequence, noFix, batterySenseFailed));
        telemetry.Send(frame, frame.Length, host, telemetryPort);
    }

    if ((DateTime.UtcNow - lastReport).TotalSeconds >= 5)
    {
        lastReport = DateTime.UtcNow;

        string summary = string.Join("  ", fleet.Select(r =>
            $"{r.Name}={r.SpeedCmS:0} cm/s{(r.IsEmergencyStopped ? " (STOPPED)" : r.IsOutsideFence ? " (wide)" : string.Empty)}"));

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {summary}");
    }
}

Console.WriteLine("Simulator stopped.");
return;

T ArgOr<T>(string name, T fallback)
{
    string[] args = Environment.GetCommandLineArgs();

    for (int i = 1; i < args.Length - 1; i++)
    {
        if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;

        try
        {
            return (T)Convert.ChangeType(args[i + 1], typeof(T));
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    return fallback;
}
