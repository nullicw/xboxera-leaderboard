using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XboxeraLeaderboard;

/// <summary>
/// Manages the CSV files for archiving the weekly and monthly rankings.
/// </summary>
/// <remarks>
/// directory structure: ./doc/scoring/lastscanstats.txt
///                                   /YYYY-MM/month.csv
///                                            week1.csv
///                                            week2.csv
///                                            week3.csv
///                                            week4.csv
///                                            monthly-game.csv
///                                            ...
/// ./doc/scoring == rootDir
/// </remarks>
internal class ScoresArchive
{
    private const char CsvSeparator = ';';

    private static readonly string[] CsvHeader = new string[] { "#;User;Gamertag;XUID;Initial GS;Final GS;Gains;Points;Initial Global Points;New Global Points" };

    public string RootDir { get; private set; }
    public string CurrentDir { get; private set; }

    public ScoresArchive(string rootDir)
    {
        RootDir    = rootDir;
        CurrentDir = LatestDir(rootDir);
    }

    /// <summary>
    /// returns a dictionary mapping XUID to their global leaderboard points.
    /// </summary>
    public IDictionary<long, int> GetPreviousGlobalPoints()
    {
        var filesWithPoints = Directory.GetFiles(CurrentDir, "*.csv");

        if (!filesWithPoints.Any())
        {
            filesWithPoints = Directory.GetFiles(LatestDir(RootDir, 1), "*.csv");
        }

        return filesWithPoints.SelectMany(f => Read(f))
                              .GroupBy(r => r.Xuid)
                              .ToDictionary(g => g.Key, g => g.Max(r => r.NewPoints));
    }

    public IEnumerable<Ranking> InitRankingWithLastWeeklyScore(int weekNr)
    {
        var latestWeeklyCsv = Path.Combine(CurrentDir, $"week{weekNr}.csv");

        if (!File.Exists(latestWeeklyCsv))
        {
            latestWeeklyCsv = Path.Combine(LatestDir(RootDir, 1), $"week{weekNr}.csv");
        }

        return Read(latestWeeklyCsv)
               .Select(u => u with { InitialGs = u.FinalGs })
               .ToArray();
    }

    public IEnumerable<Ranking> InitRankingWithLastMonthlyScore()
    {
        var latestMonthlyCsv = Path.Combine(LatestDir(RootDir, 1), "month.csv");

        return Read(latestMonthlyCsv)
               .Select(u => u with { InitialGs = u.FinalGs })
               .ToArray();
    }

    public void Write(string filename, IEnumerable<Ranking> ranking)
    {
        var fullPathAndFilename = Path.Combine(CurrentDir, filename);

        var csvData = ranking.Select(w => string.Join(CsvSeparator,
                                                      w.Rank, w.User, w.Gamertag, w.Xuid, w.InitialGs, w.FinalGs, w.Gains, w.Points, w.InitialPoints, w.NewPoints));
        File.WriteAllLines(fullPathAndFilename, CsvHeader.Concat(csvData));

        Console.WriteLine($"wrote csv file {filename}");
    }

    public string CreateDirectoryForNextMonth()
    {
        var nextMonthDir = Path.Combine(RootDir, $"{DateTime.Today:yyyy-MM}");
        Directory.CreateDirectory(nextMonthDir);

        File.WriteAllText(Path.Combine(nextMonthDir, ".gitignore"), "#empty");

        return nextMonthDir;
    }

    private static IEnumerable<Ranking> Read(string filename)
    {
        var file = File.ReadAllLines(filename);

        return file.Where(l => !string.IsNullOrWhiteSpace(l))
                   .Skip(1) // header
                   .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                   .Select(l => new Ranking(User: l[1], Gamertag: l[2], Xuid: long.Parse(l[3]),
                                            InitialGs: int.Parse(l[4]), FinalGs: int.Parse(l[5]), Gains: int.Parse(l[6]),
                                            Points: int.Parse(l[7]), InitialPoints: int.Parse(l[8]), NewPoints: int.Parse(l[9])))
                   .ToArray();
    }

    private static string LatestDir(string path, int skip = 0) => Path.Combine(path,
                                                                               Directory.EnumerateDirectories(path)
                                                                                        .OrderByDescending(s => s)
                                                                                        .Skip(skip)
                                                                                        .First());
}
