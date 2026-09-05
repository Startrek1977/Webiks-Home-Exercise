using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RoverRally.Core.Logging;
using RoverRally.Core.Models;

namespace RoverRally.Core.Roster
{
    /// <summary>
    /// The fleet as configured for the site. Vehicles are added and retired
    /// often enough that the list lives in a file the site lead can edit.
    /// </summary>
    public static class RoverRoster
    {
        public static IList<Rover> Load(string path)
        {
            if (!File.Exists(path))
            {
                Log.Error("Roster file not found at " + path + ".");
                return new List<Rover>();
            }

            try
            {
                string json = File.ReadAllText(path);
                List<Rover>? rovers = JsonSerializer.Deserialize<List<Rover>>(json);

                if (rovers == null) rovers = new List<Rover>();

                Log.Info(string.Format("Loaded {0} rover(s) from the roster.", rovers.Count));
                return rovers;
            }
            catch (JsonException ex)
            {
                Log.Error("Could not parse the roster at " + path + " as JSON", ex);
                return new List<Rover>();
            }
            catch (Exception ex)
            {
                Log.Error("Could not read the roster at " + path, ex);
                return new List<Rover>();
            }
        }
    }
}
