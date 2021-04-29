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

    public class Program
    {
        private const char               CsvSeparator           = ';';
        private const string             StatsFilename          = "lastscanstats.txt";

        private static readonly string[] CsvHeader              = new string[] { "#;User;Gamertag;XUID;Initial GS;Final GS;Gains;Points;Initial Global Points;New Global Points" };
        private static readonly string[] DiscourseWeeklyHeader  = new string[] {
                                                                      "|#|User|Gamertag|Initial GS|Final GS|Gains|Points|",
                                                                      "| --- | --- | --- | --- | --- | --- | --- |"
                                                                  };
        private static readonly string[] DiscourseGlobalHeader  = new string[] {
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
        ///   the program writes the weekly and global ranking in Discourse table format to stdout
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
        public static async Task Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("XboxeraLeaderboard.exe $lastweek-filename $thisweek-filename");
                Console.WriteLine("XboxeraLeaderboard.exe --weekly|monthly $pages-path");
                Console.WriteLine("XboxeraLeaderboard.exe --monthly $pages-path");
                Console.WriteLine("i.e. XboxeraLeaderboard.exe week31.csv week32.csv");
                Console.WriteLine("  or XboxeraLeaderboard.exe --weekly ./docs/scores/");
            }
            else
            {
                // directory structure: ./doc/scoring/lastscanstats.txt
                //                                   /YYYY-MM/week1.csv
                //                                            week1.txt
                //                                            week2.csv
                //                                            week2.txt
                //                                            ...

                if(args[0] == "--weekly")
                {
                    var rootScoringDir = Path.GetFullPath(args[1]);

                    var stats  = await File.ReadAllLinesAsync(Path.Combine(rootScoringDir, StatsFilename));
                    var weekNr = int.Parse(stats.First(l => l.StartsWith("week="))[5..]);

                    var dirForLatestMonth  = LatestDir(rootScoringDir);
                    var lastWeekCsvFile    = Path.Combine(dirForLatestMonth, $"week{weekNr}.csv");
                    if(!File.Exists(lastWeekCsvFile))
                    {
                        lastWeekCsvFile = Path.Combine(LatestDir(args[1], 1), $"week{weekNr}.csv");
                    }

                    weekNr++;
                    var discourse = await Weekly(lastWeekCsvFile, Path.Combine(dirForLatestMonth, $"week{weekNr}.csv"));
                    await WriteNewGithubPage(rootScoringDir, dirForLatestMonth, weekNr, discourse);
                    await WriteNewStatsFile(rootScoringDir, weekNr);
                }
                else
                {
                    var discourse = await Weekly(args[0], args[1]);

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    foreach(var line in discourse)
                    {
                        Console.WriteLine(line);
                    }
                }
            }
        }

        private static string LatestDir(string path, int skip = 0) => Directory.EnumerateDirectories(path)
                                                                               .OrderByDescending(s => s)
                                                                               .Skip(skip)
                                                                               .First();

        private static async Task WriteNewStatsFile(string rootScoringDir, int weekNumber)
        {
            await File.WriteAllLinesAsync(Path.Combine(rootScoringDir, StatsFilename),
                                          new string[] {
                                              $"week={weekNumber}",
                                              $"date={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)"
                                          });
        }

        private static async Task WriteNewGithubPage(string rootScoringDir, string currentDir, int weekNumber, string[] discourse)
        {
            await File.WriteAllLinesAsync(Path.Combine(Directory.GetParent(rootScoringDir).FullName,
                                                       "_posts",
                                                       $"{DateTime.UtcNow:yyyy-MM-dd}-scan-week-{weekNumber}.md"),
                                          new string[] {
                                              "---",
                                              $"title: \"Week {weekNumber}\" ",
                                              $"date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)",
                                              "layout: post",
                                              "tags: weekly",
                                              "---",
                                              string.Empty,
                                              "# Excel",
                                              $"[Week {weekNumber}]({{{{ site.github.url }}}}/{currentDir}/week{weekNumber}.csv)",
                                              string.Empty,
                                              "# Discourse",
                                              "```",
                                          }.Concat(discourse)
                                           .Concat(new[] { "```" }));
        }

        private static async Task<string[]> Weekly(string lastWeekFilename, string nextWeekFilename)
        {
            // parsing input file

            var input = await File.ReadAllLinesAsync(lastWeekFilename);

            var users = input.Where(l => !string.IsNullOrWhiteSpace(l))
                             .Skip(1) // headline
                             .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                             .Select(l => (user: l[1], gamerTag: l[2], xuid: long.Parse(l[3]), lastScore: int.Parse(l[5]), lastPoints: int.Parse(l[9])));

            // getting current gamerscores from xbox and calculate gains

            var newScores = users.Select(u => (u, newScore: ReadCurrentGamerScore(u.gamerTag, u.xuid).Result))
                                 .Select(s => (s.u, s.newScore, gains: Gains(s.u.lastScore, s.newScore)));

            // weekly ranking (users with same gains have to be ranked the same!)
            // and directly add points to total leaderboard points

            var weeklyRanking = Rank(newScores, s => s.gains, r => WeeklyPoints(r)).Select(r => (
                r.rank,
                r.score.u,
                r.score.newScore,
                r.score.gains,
                r.points,
                totalPoints: r.score.u.lastPoints + r.points
            )).ToArray();

            // global ranking by new total leaderboard points

            var globalRanking = Rank(weeklyRanking, s => s.totalPoints, r => r).Select(r => (
                r.rank,
                r.score.u,
                r.score.totalPoints
            )).ToArray();

            // writing output
            // 1. first one in input csv format for the scanner itself or for excel 

            var thisWeek = weeklyRanking.Select(w => string.Join(CsvSeparator,
                                                                    w.rank, w.u.user, w.u.gamerTag, w.u.xuid, w.u.lastScore, w.newScore, w.gains, w.points, w.u.lastPoints, w.totalPoints));
            await File.WriteAllLinesAsync(nextWeekFilename, CsvHeader.Concat(thisWeek));

            // 2. second one in Discourse table format for copy/pasting it to a forum post
            //    two tables are written here

            var toDiscourseWeekly = weeklyRanking.Select((g, i) => $"|{g.rank}.|{(i < 10 ? '@' : ' ')}{g.u.user}|{g.u.gamerTag}|{g.u.lastScore}|{g.newScore}|{g.gains}|{g.points}|");
            var toDiscourseGlobal = globalRanking.Select(g => $"|{g.rank}.|{g.u.user}|{g.u.gamerTag}|{g.totalPoints}|");

            return DiscourseWeeklyHeader.Concat(toDiscourseWeekly)
                                        .Concat(new[] { "\n" })
                                        .Concat(DiscourseGlobalHeader)
                                        .Concat(toDiscourseGlobal)
                                        .ToArray();
        }

        /// <summary>
        /// ranks a sequence and scores it by rank
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

        private static int Gains(int gamerscoreBefore, int gamerscoreNow) => Max(0, gamerscoreNow - gamerscoreBefore);

        private static int WeeklyPoints(int rank) => Max(0, 10 - rank);

        /// <summary>
        /// calls open XBL api to get the gamerscore for a Xbox User Id (XUID)
        /// </summary>
        private static async Task<int> ReadCurrentGamerScore(string gamerTag, long xuid)
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

                var response = await httpClient.SendAsync(request);

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
                    var content = await response.Content.ReadAsStringAsync();

                    var settings = JObject.Parse(content)["profileUsers"].First()["settings"].Children();
                    var gamerscoreSettings = settings.Select(s => s.ToObject<OpenXblSettings>())
                                                     .First(s => s.Id == "Gamerscore");

                    // open XBL api only allows 10 requests per 15 seconds, so give it some time
                    await Task.Delay(2500);

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
    }
}
