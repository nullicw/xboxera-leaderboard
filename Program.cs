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
        /// the programm writes 2 outputs:
        /// - weekly and global ranking including XUIDs in csv format: this is to be used as the input for the next weekly run
        /// - weekly and global ranking in Discourse table format on stdout or a file (optional)
        /// </summary>
        /// <remarks>
        /// get xuids for gamertag: https://www.cxkes.me/xbox/xuid
        /// how to use openXBL api https://xbl.io/getting-started
        /// </remarks>
        public static async Task Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("XboxeraLeaderboard.exe $lastweek-filename $thisweek-filename ($output-for-xboxera)");
                Console.WriteLine("i.e. XboxeraLeaderboard.exe week31.csv week32.csv");
            }
            else
            {
                // parsing input file

                var input = await File.ReadAllLinesAsync(args[0]);

                var users = input.Where(l => !string.IsNullOrWhiteSpace(l))
                                 .Skip(1) // headline
                                 .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                 .Select(l => new { user = l[1], gamerTag = l[2], xuid = long.Parse(l[3]), lastScore = int.Parse(l[5]), lastPoints = int.Parse(l[9]) });

                // getting current gamerscores from xbox and calculate gains

                var newScores = users.Select(u => new { u, newScore = ReadCurrentGamerScore(u.gamerTag, u.xuid).Result })
                                     .Select(s => new { s.u, s.newScore, gains = Gains(s.u.lastScore, s.newScore) });

                // weekly ranking (users with same gains have to be ranked the same!)
                // and directly add points to total leaderboard points

                var weeklyRanking = Rank(newScores, s => s.gains, r => WeeklyPoints(r)).Select(r => new {
                    rank = r.Item2,
                    r.Item1.u,
                    r.Item1.newScore,
                    r.Item1.gains,
                    points = r.Item3,
                    totalPoints = r.Item1.u.lastPoints + r.Item3
                }).ToArray();

                // global ranking by new total leaderboard points

                var globalRanking = Rank(weeklyRanking, s => s.totalPoints, r => r).Select(r => new {
                    rank = r.Item2,
                    r.Item1.u,
                    r.Item1.totalPoints
                }).ToArray();

                // writing output
                // 1. first one in input csv format for the scanner itself or for excel 

                var thisWeek = weeklyRanking.Select(w => string.Join(CsvSeparator,
                                                                     w.rank, w.u.user, w.u.gamerTag, w.u.xuid, w.u.lastScore, w.newScore, w.gains, w.points, w.u.lastPoints, w.totalPoints));
                await File.WriteAllLinesAsync(args[1], CsvHeader.Concat(thisWeek));

                // 2. second one in Discourse table format for copy/pasting it to a forum post
                //    two tables are written here

                var toDiscourseWeekly = weeklyRanking.Select(g => $"|{g.rank}.|{(g.rank < 11 ? '@' : ' ')}{g.u.user}|{g.u.gamerTag}|{g.u.lastScore}|{g.newScore}|{g.gains}|{g.points}|");
                var toDiscourseGlobal = globalRanking.Select(g => $"|{g.rank}.|{(g.rank < 11 ? '@' : ' ')}{g.u.user}|{g.u.gamerTag}|{g.totalPoints}|");

                var toDiscourse = DiscourseWeeklyHeader.Concat(toDiscourseWeekly)
                                                       .Concat(new[] { "\n" })
                                                       .Concat(DiscourseGlobalHeader)
                                                       .Concat(toDiscourseGlobal);

                if(args.Length == 3)
                {
                    await File.WriteAllLinesAsync(args[2], toDiscourse);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    foreach(var line in toDiscourse)
                    {
                        Console.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// ranks a sequence and scores it by rank
        /// </summary>
        private static IEnumerable<Tuple<T, int, int>> Rank<T>(IEnumerable<T> toRank, Func<T, int> rankBy, Func<int, int> scoring)
        {
            var ranked = toRank.OrderByDescending(rankBy)
                               .Select((s, i) => new { rank = i + 1, points = scoring(i), s });

            var grouped = ranked.GroupBy(r => rankBy(r.s))
                                .OrderByDescending(g => g.Key)
                                .Select((g, i) => new { grouprank = g.Min(r => r.rank), grouppoints = g.Max(r => r.points), group = g.Select(i => i) })
                                .SelectMany(g => g.group.Select(s => new Tuple<T, int, int>(s.s, g.grouprank, g.grouppoints)));

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
                    await Task.Delay(5000);

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
