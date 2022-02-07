using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XboxeraLeaderboard;

public class Program
{
    /// <summary>
    /// Takes a list of gamertags in csv format (with their xuid and last weeks gamerscore),
    /// polls the open XBL api for their current gamerscore and outputs the (weekly) leaderboard
    /// in a table format Discourse understands.
    /// 
    /// operates in 2 different modes:
    /// - if the caller specifies option --weekly the programm scans the path for the last
    ///   weekly file, calculates the weekly diff and writes the new weekly and global scores
    ///   to a new file in the path. it also writes a second text file in Discourse format.
    /// - if the caller specifies option --monthly the programm first scans all users for
    ///   the highest score for the monthly game (this games name is either specified on the
    ///   commandline or in lastscanstats.txt). The the program searches the path for the last
    ///   monthly file, calculates the monthly diff and writes the new monthly and global scores
    ///   to a new file in the path. it also writes a second text file in Discourse format.
    /// </summary>
    public static void Main(string[] args)
    {
        if(args.Length != 2)
        {
            Console.WriteLine("usage:");
            Console.WriteLine("XboxeraLeaderboard.exe --weekly(=\"game-title\") $scores-path");
            Console.WriteLine("XboxeraLeaderboard.exe --monthly(=\"game-title\") $scores-path");
            Console.WriteLine("  or XboxeraLeaderboard.exe --weekly ./docs/scores");

            return;
        }

        var rootDir  = Path.GetFullPath(args[1]);
        var scoresDb = new ScoresArchive(rootDir);
        var settings = ScanSettings.Read(rootDir);

        var prevGlobalPoints = scoresDb.GetPreviousGlobalPoints();

        if(args[0] == "--weekly")
        {
            var ranking = scoresDb.InitRankingWithLastWeeklyScore(settings.Week)
                          .Join(prevGlobalPoints, w => w.Xuid, m => m.Key, (r, gp) => r with { InitialPoints = gp.Value, NewPoints = 0 })
                          .ToArray();

            //if(MonthlyGame(args, settings) is var monthlyGame && !string.IsNullOrWhiteSpace(monthlyGame))
            //{
            //    var gameRanking = RankMonthlyGame(monthlyGame,
            //                                      ranking.Select(u => u with { InitialGs = 0, Points = 0 }).ToArray(),
            //                                      scoresDb);

            //    ranking = ranking.Join(gameRanking, w => w.Xuid, mg => mg.Xuid, (w, mg) => w with { InitialPoints = mg.NewPoints })
            //                     .ToArray();
            //}

            RankWeekly(ranking, scoresDb, settings.Week + 1);

            settings.UpdateForNextWeek(rootDir);
        }
        else if(args[0].StartsWith("--monthly"))
        {
            var ranking = scoresDb.InitRankingWithLastMonthlyScore()
                          .Join(prevGlobalPoints, w => w.Xuid, m => m.Key, (r, gp) => r with { InitialPoints = gp.Value })
                          .ToArray();

            if(MonthlyGame(args, settings) is var monthlyGame && !string.IsNullOrWhiteSpace(monthlyGame))
            {
                var gameRanking = RankMonthlyGame(monthlyGame,
                                                  ranking.Select(u => u with { InitialGs = 0, Points = 0 }).ToArray(),
                                                  scoresDb);

                ranking = ranking.Join(gameRanking, m => m.Xuid, mg => mg.Xuid, (m, mg) => m with { InitialPoints = mg.NewPoints })
                                 .ToArray();
            }

            RankMonthly(ranking, scoresDb);
        }
    }

    private static string MonthlyGame(string[] args, ScanSettings settings)
        => args[0].Length > 9 ? args[0][10..].Trim('\"').ToLower() : settings.MonthlyGame.Trim().ToLower();

    private static IEnumerable<Ranking> RankWeekly(IEnumerable<Ranking> users, ScoresArchive scoresDb, int weekNr)
    {
        // weekly and new global ranking (users with same gains have to be ranked the same!)
        // and directly add points to total leaderboard points

        var newGamerscores = ReadAllNewGamerscores(users);

        var weeklyRanking = newGamerscores.RankByGamerscoreGains();
        var globalRanking = weeklyRanking.RankGlobal();

        // write output (csv + discourse)

        scoresDb.Write($"week{weekNr}.csv", weeklyRanking);

        var githubWriter = new GithubPagesWriter(scoresDb.RootDir, scoresDb.CurrentDir);
        githubWriter.WriteWeeklyGithubPage(weekNr, weeklyRanking, globalRanking);

        return globalRanking;
    }

    private static IEnumerable<Ranking> RankMonthly(IEnumerable<Ranking> users, ScoresArchive scoresDb)
    {
        // monthly ranking by gamerscore gains (users with same gains have to be ranked the same!) and
        // global ranking by new total leaderboard points

        var newGamerscores = ReadAllNewGamerscores(users);

        var monthlyRanking = newGamerscores.RankByGamerscoreGains();
        var globalRanking  = monthlyRanking.RankGlobal();

        // write output (csv + discourse) and
        // create empty dir for next month

        scoresDb.Write("month.csv", monthlyRanking);

        var githubWriter = new GithubPagesWriter(scoresDb.RootDir, scoresDb.CurrentDir);
        githubWriter.WriteMonthlyGithubPage(monthlyRanking, globalRanking);

        scoresDb.CreateDirectoryForNextMonth();

        return globalRanking;
    }


    private static IEnumerable<Ranking> RankMonthlyGame(string gameName, IEnumerable<Ranking> users, ScoresArchive scoresDb)
    {
        // search for title id and get gamerscores of all users for this title, then
        // rank on gains for the montly game

        Console.WriteLine($"searching for any user who played '{gameName}' to get its Xbox Title-ID");

        var gameTitleId = users.Select(u => OpenXblApi.GetTitleId(u.Xuid, gameName))
                               .First(t => t.HasValue).Value;

        Console.WriteLine($"get gamerscores of title {gameTitleId} for all users");

        IEnumerable<Ranking> gamerscoresForTitle = users.Select(u => u with { Gains = OpenXblApi.GetGamerscoreForTitle(u.Xuid, gameTitleId) })
                                                        .Select(u => u with { FinalGs = u.Gains })
                                                        .ToArray();

        var titleRanking = gamerscoresForTitle.RankByGamerscoreGains();
        var globalRanking = titleRanking.RankGlobal();

        // writing output (csv + discourse)

        var filename = GameToFilename(gameName);
        scoresDb.Write($"{filename}.csv", titleRanking);

        var githubWriter = new GithubPagesWriter(scoresDb.RootDir, scoresDb.CurrentDir);
        githubWriter.WriteMonthlyGameGithubPage(gameName, filename, titleRanking, globalRanking);

        return globalRanking;
    }

    private static string GameToFilename(string title) => title.Replace(" ", "").Replace(":", "");

    private static IEnumerable<Ranking> ReadAllNewGamerscores(IEnumerable<Ranking> users)
    {
        Console.WriteLine($"get current gamerscore for all users");

        return users.Select(u => u with { FinalGs = OpenXblApi.GetCurrentGamerScore(u.Xuid)})
                    .Select(u => u with { Gains = Gains(u.InitialGs, u.FinalGs) })
                    .ToArray();
    }
    private static int Gains(int gamerscoreBefore, int gamerscoreNow) => Math.Max(0, gamerscoreNow - gamerscoreBefore);
}
