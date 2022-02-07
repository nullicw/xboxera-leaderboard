using Newtonsoft.Json;

using System;
using System.IO;

namespace XboxeraLeaderboard;

internal record ScanSettings(int Week, string Date, string MonthlyGame)
{
    private const string StatsFilename = "scansettings.json";

    public static ScanSettings Read(string rootDir)
    {
        return JsonConvert.DeserializeObject<ScanSettings>(File.ReadAllText(Path.Combine(rootDir, StatsFilename)));

    }

    public ScanSettings UpdateForNextWeek(string rootDir)
    {
        var nextSettings = this with
        {
            Date = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)",
            Week = this.Week + 1
        };

        File.WriteAllText(Path.Combine(rootDir, StatsFilename),
                          JsonConvert.SerializeObject(nextSettings, Formatting.Indented));

        return nextSettings;
    }
}
