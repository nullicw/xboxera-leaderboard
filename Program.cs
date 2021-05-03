using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using static System.Math;

namespace XboxeraLeaderboard
{
    /// <summary>
    /// for parsing the open XBL api JSON response
    /// </summary>
    internal record OpenXblSettings(string Id, string Value);

    internal record Ranking(string User, string Gamertag, long Xuid, int Rank = 0, int InitialGs = 0, int FinalGs = 0, int Gains = 0, int Points = 0, int InitialPoints = 0, int NewPoints = 0);

    public class Program
    {
        private const char               CsvSeparator           = ';';
        private const string             StatsFilename          = "lastscanstats.txt";

        private static readonly string[] CsvHeader              = new string[] { "#;User;Gamertag;XUID;Initial GS;Final GS;Gains;Points;Initial Global Points;New Global Points" };
        private static readonly string[] DiscoursePeriodHeader  = new string[] {
                                                                      "|#|User|Gamertag|Initial GS|Final GS|Gains|Points|",
                                                                      "| --- | --- | --- | --- | --- | --- | --- |"
                                                                  };
        private static readonly string[] DiscourseSummaryHeader  = new string[] {
                                                                       "|#|User|Gamertag|Points|",
                                                                       "| --- | --- | --- | --- |"
                                                                   };

        /// <summary>
        /// the API key for openXBL needed to call their REST services (a new can be generated on their
        /// site on demand at no cost)
        /// </summary>
        private const string OpenXBLKey = "koso444og0w8w8ckw8s44sgk0gkwwos4g8s";

        /// <summary>
        /// Takes a list of gamertags in csv format (with their xuid and last weeks gamerscore),
        /// polls the open XBL api for their current gamerscore and outputs the (weekly) leaderboard
        /// in a table format Discourse understands.
        /// 
        /// operates in 3 different modes:
        /// - if the caller provides 2 csv-files the programm reads the first csv,
        ///   computes the weekly diff and writes it to the second specified csv file. additionaly
        ///   the program writes the rankings for the time period and the global ranking in Discourse table
        ///   format to stdout
        /// - if the caller specifies option --weekly the programm scans the path for the last
        ///   weekly file, calculates the weekly diff and writes the new weekly and global scores
        ///   to a new file in the path. it also writes a second text file in Discourse format.
        /// - if the caller specifies option --monthly the programm scans the path for the last
        ///   monthly file, calculates the monthly diff and writes the new monthly and global scores
        ///   to a new file in the path. it also writes a second text file in Discourse format.
        /// </summary>
        /// <remarks>
        /// get xuids for gamertag: https://www.cxkes.me/xbox/xuid
        /// how to use openXBL api https://xbl.io/getting-started
        /// </remarks>
        public static void Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("XboxeraLeaderboard.exe $lastweek-filename $thisweek-filename");
                Console.WriteLine("XboxeraLeaderboard.exe --weekly $scores-path");
                Console.WriteLine("XboxeraLeaderboard.exe --monthly $scores-path");
                Console.WriteLine("i.e. XboxeraLeaderboard.exe week31.csv week32.csv");
                Console.WriteLine("  or XboxeraLeaderboard.exe --weekly ./docs/scores");
            }
            else
            {
                // directory structure: ./doc/scoring/lastscanstats.txt
                //                                   /YYYY-MM/month.csv
                //                                            week1.csv
                //                                            week1.txt
                //                                            week2.csv
                //                                            week2.txt
                //                                            ...
                // ./doc/scoring == rootDir

                var rootDir = Path.GetFullPath(args[1]);

                if(args[0] == "--weekly")
                {
                    var stats  = File.ReadAllLines(Path.Combine(rootDir, StatsFilename));
                    var weekNr = int.Parse(stats.First(l => l.StartsWith("week="))[5..]);

                    var currentMonthDir      = LatestDir(rootDir);
                    var fileWithLatestPoints = Path.Combine(currentMonthDir, $"week{weekNr}.csv");

                    IEnumerable<Ranking> users;
                    if(!File.Exists(fileWithLatestPoints))
                    {
                        var latestPoints = ReadCsv(Path.Combine(LatestDir(rootDir, 1), $"month.csv"))
                                           .ToDictionary(m => m.Xuid, m => m.InitialPoints);

                        users = ReadCsv(Path.Combine(LatestDir(rootDir, 1), $"week{weekNr}.csv"))
                                .Select(u => u with { InitialPoints = latestPoints[u.Xuid] });
                    }
                    else
                    {
                        users = ReadCsv(fileWithLatestPoints);
                    }

                    weekNr++;
                    var discourse = Weekly(users, Path.Combine(currentMonthDir, $"week{weekNr}.csv"));
                    WriteWeeklyGithubPage(rootDir, Path.GetFileName(currentMonthDir), weekNr, discourse);
                    WriteNewStatsFile(rootDir, weekNr);
                }
                else if(args[0] == "--monthly")
                {
                    var currentMonthDir = LatestDir(rootDir);
                    var lastMonthDir    = LatestDir(rootDir, 1);

                    var discourse = Monthly(rootDir, lastMonthDir, currentMonthDir);
                    WriteMonthlyGithubPage(rootDir, Path.GetFileName(currentMonthDir), discourse);
                }
                else
                {
                    var inputCsv  = ReadCsv(args[0]);
                    var discourse = Weekly(inputCsv, args[1]);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    foreach(var line in discourse)
                    {
                        Console.WriteLine(line);
                    }
                }
            }
        }

        private static string[] Weekly(IEnumerable<Ranking> users, string thisWeekFilename)
        {
            var newGamerscores = ReadAllNewGamerscores(users);

            // weekly and new global ranking (users with same gains have to be ranked the same!)
            // and directly add points to total leaderboard points

            var weeklyRanking = Rank(newGamerscores, s => s.Gains, r => WeeklyPoints(r))
                                .Select(r => r.score with { Rank = r.rank, Points = r.points, NewPoints = r.score.InitialPoints + r.points })
                                .ToArray();

            var globalRanking = Rank(weeklyRanking, s => s.NewPoints, Identity).Select(r => r.score with { Rank = r.rank });

            // writing output (csv + discourse)

            WriteCsv(thisWeekFilename, weeklyRanking);

            var toDiscourseWeekly = weeklyRanking.Select((r, i) => $"|{r.Rank}.|{(i < 10 ? '@' : ' ')}{r.User}|{r.Gamertag}|{r.InitialGs}|{r.FinalGs}|{r.Gains}|{r.Points}|");
            var toDiscourseGlobal = globalRanking.Select(r => $"|{r.Rank}.|{r.User}|{r.Gamertag}|{r.NewPoints}|");

            return DiscoursePeriodHeader.Concat(toDiscourseWeekly)
                                        .Concat(new[] { "\n" })
                                        .Concat(DiscourseSummaryHeader)
                                        .Concat(toDiscourseGlobal)
                                        .ToArray();
        }

        private static int WeeklyPoints(int rank) => Max(0, 10 - rank);

        private static string[] Monthly(string rootDir, string lastMonthDir, string currentMonthDir)
        {
            var users          = ReadCsv(Path.Combine(lastMonthDir, "month.csv"));
            var newGamerscores = ReadAllNewGamerscores(users);

            // sum all weekly points (and extra points for game of the month) for this month

            var sumWeeklyPoints = Directory.EnumerateFiles(currentMonthDir, "*.csv")
                                           .SelectMany(f => ReadCsv(f))
                                           .GroupBy(g => g.Xuid)
                                           .ToDictionary(g => g.Key, g => g.Sum(gg => gg.Points));

            // monthly ranking by gamerscore gains (users with same gains have to be ranked the same!) and
            // global ranking by new total leaderboard points
            // new_leaderboard_points = last_month_points + sum(weekly_points_of_month) + monthlyRanking

            var monthlyRanking = Rank(newGamerscores, s => s.Gains, r => MonthlyPoints(r))
                                 .Select(r => r.score with {
                                     Rank = r.rank,
                                     Points = r.points,
                                     NewPoints = r.score.InitialPoints + sumWeeklyPoints[r.score.Xuid] + r.points })
                                 .ToArray();

            var globalRanking = Rank(monthlyRanking, s => s.NewPoints, Identity)
                                .Select(r => r.score with { Rank = r.rank })
                                .ToArray();

            // create empty dir for next month

            var nextMonthDir = Path.Combine(rootDir, $"{DateTime.Today:yyyy-MM}");
            Directory.CreateDirectory(nextMonthDir);
            File.WriteAllText(Path.Combine(nextMonthDir, ".gitignore"), "#empty");

            // writing output (csv + discourse)

            WriteCsv(Path.Combine(currentMonthDir, "month.csv"), monthlyRanking);

            var toDiscourseMonthly = monthlyRanking.Select((r, i) => $"|{r.Rank}.|{(i < 10 ? '@' : ' ')}{r.User}|{r.Gamertag}|{r.InitialGs}|{r.FinalGs}|{r.Gains}|{r.Points}|");
            var toDiscourseGlobal  = globalRanking.Select(r => $"|{r.Rank}.|{r.User}|{r.Gamertag}|{r.NewPoints}|");

            return DiscoursePeriodHeader.Concat(toDiscourseMonthly)
                                        .Concat(new[] { "\n" })
                                        .Concat(DiscourseSummaryHeader)
                                        .Concat(toDiscourseGlobal)
                                        .ToArray();
        }

        private static int MonthlyPoints(int rank) => Max(0, 100 - 10 * rank);

        private static int Gains(int gamerscoreBefore, int gamerscoreNow) => Max(0, gamerscoreNow - gamerscoreBefore);

        private static IEnumerable<Ranking> ReadAllNewGamerscores(IEnumerable<Ranking> users)
        {
            return users.Select(u => u with { FinalGs = ReadCurrentGamerScore(u.Gamertag, u.Xuid)})
                        .Select(u => u with { Gains = Gains(u.InitialGs, u.FinalGs) })
                        .ToArray();
        }

        /// <summary>
        /// calls open XBL api to get the gamerscore for a Xbox User Id (XUID)
        /// </summary>
        private static int ReadCurrentGamerScore(string gamerTag, long xuid)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"calling open XBL api for {gamerTag} .... ");

            try
            {
                using var httpClient = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://xbl.io/api/v2/account/{xuid}"),
                };
                request.Headers.Add("X-Authorization", OpenXBLKey);

                var response = httpClient.Send(request);

                // response JSON looks like
                // {
                //   "profileUsers": [
                //   {
                //     "id": "2535413400000000",
                //     "hostId": "2535413400000000",
                //     "settings": [
                //       {
                //         "id": "GameDisplayPicRaw",
                //         "value": "http://images-eds.xboxlive.com/image?url=wHwbXKif8cus8csoZ03RW_ES.ojiJijNBGRVUbTnZKsoCCCkjlsEJrrMqDkYqs3MBhMLdvWFHLCswKMlApTSbzvES1cjEAVPrczatfOc0jR0Ss4zHEy6ErElLAY8rAVFRNqPmGHxiumHSE9tZRnlghsACzaoisWEww1VSUd9Sx0-&format=png"
                //         },
                //         {
                //         "id": "Gamerscore",
                //         "value": "6855"
                //         }, ...

                if(response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;

                    var settings = JObject.Parse(content)["profileUsers"].First()["settings"].Children();
                    var gamerscoreSettings = settings.Select(s => s.ToObject<OpenXblSettings>())
                                                     .First(s => s.Id == "Gamerscore");

                    // open XBL api only allows 10 requests per 15 seconds, so give it some time
                    System.Threading.Thread.Sleep(2500);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK");

                    return int.Parse(gamerscoreSettings.Value);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"error = {response.ReasonPhrase}");
                    return 0;
                }
            }
            catch(Exception exc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"error = {exc.Message}");
                return 0;
            }
        }

        /// <summary>
        /// ranks a sequence and scores it by rank (elements with same score get same rank)
        /// </summary>
        private static IEnumerable<(T score, int rank, int points)> Rank<T>(IEnumerable<T> toRank, Func<T, int> rankBy, Func<int, int> scoring)
        {
            var ranked = toRank.OrderByDescending(rankBy)
                               .Select((s, i) => (rank: i + 1, points: scoring(i), s));

            var grouped = ranked.GroupBy(r => rankBy(r.s))
                                .OrderByDescending(g => g.Key)
                                .Select((g, i) => (grouprank: g.Min(r => r.rank), grouppoints: g.Max(r => r.points), group: g.Select(i => i)))
                                .SelectMany(g => g.group.Select(s => (s.s, g.grouprank, g.grouppoints)));

            return grouped;
        }

        private static T Identity<T>(T t) => t;

        private static string LatestDir(string path, int skip = 0) => Directory.EnumerateDirectories(path)
                                                                               .OrderByDescending(s => s)
                                                                               .Skip(skip)
                                                                               .First();

        private static IEnumerable<Ranking> ReadCsv(string filename)
        {
            var file = File.ReadAllLines(filename);

            return file.Where(l => !string.IsNullOrWhiteSpace(l))
                       .Skip(1) // header
                       .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                       .Select(l => new Ranking(User: l[1], Gamertag: l[2], Xuid: long.Parse(l[3]), InitialGs: int.Parse(l[5]), Points: int.Parse(l[7]), InitialPoints: int.Parse(l[9])));
        }

        private static void WriteCsv(string filename, IEnumerable<Ranking> ranking)
        {
            var csvData = ranking.Select(w => string.Join(CsvSeparator,
                                                          w.Rank, w.User, w.Gamertag, w.Xuid, w.InitialGs, w.FinalGs, w.Gains, w.Points, w.InitialPoints, w.NewPoints));
            File.WriteAllLines(filename, CsvHeader.Concat(csvData));
        }

        private static void WriteNewStatsFile(string rootDir, int weekNumber)
        {
            File.WriteAllLines(Path.Combine(rootDir, StatsFilename),
                               new string[] {
                                   $"week={weekNumber}",
                                   $"date={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)"
                               });
        }

        private static void WriteWeeklyGithubPage(string rootDir, string currentDir, int weekNumber, string[] discourse)
        {
            File.WriteAllLines(Path.Combine(Directory.GetParent(rootDir).FullName,
                                            "_posts",
                                            $"{DateTime.UtcNow:yyyy-MM-dd}-scan-week-{weekNumber}.md"),
                               BuildGithubPage("weekly", $"Week {weekNumber}", $"scores/{currentDir}/week{weekNumber}.csv", discourse));
        }

        private static void WriteMonthlyGithubPage(string rootDir, string currentMonth, string[] discourse)
        {
            File.WriteAllLinesAsync(Path.Combine(Directory.GetParent(rootDir).FullName,
                                                 "_posts",
                                                 $"{DateTime.UtcNow:yyyy-MM-dd}-scan-month-{currentMonth}.md"),
                                    BuildGithubPage("monthly", $"Month {currentMonth}", $"scores/{currentMonth}/month.csv", discourse));
        }

        private static IEnumerable<string> BuildGithubPage(string tag, string title, string link, IEnumerable<string> discourseContent)
        {
            return new string[] { "---",
                                  "layout: post",
                                  $"tags: {tag}",
                                  $"title: \"{title}\" ",
                                  $"date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)",
                                  "---",
                                  string.Empty,
                                  "# Excel",
                                  string.Empty,
                                  $"[{title}]({{{{ site.github.url }}}}/{link})",
                                  string.Empty,
                                  "# Discourse",
                                  string.Empty,
                                  "```" }
                .Concat(discourseContent)
                .Concat(new[] { "```" });
        }
    }
}
