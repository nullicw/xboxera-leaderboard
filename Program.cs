using Newtonsoft.Json.Linq;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace XboxeraLeaderboard
{
    /// <summary>
    /// for parsing the open XBL api JSON response
    /// </summary>
    internal record OpenXblSettings(string Id, string Value);

    /// <summary>
    /// Takes a | separated list of gamertags (with their xuid and last weeks gamerscore)
    /// polls the open XBL api for the current gamerscore and outputs the (weekly) leaderboard
    /// in a table format Discourse understands.
    /// the programm writes 2 files:
    /// - the first includes XUIDs and is to be used as the input for the next weekly run. it is
    ///   also in a csv-format Excel understands.
    /// - the second omits XUIDs and is Discourse table format. this file is optional, if its name
    ///   is missing on the commandline the program writes the output to stdout.
    /// </summary>
    /// <remarks>
    /// get xuids for gamertag: https://www.cxkes.me/xbox/xuid
    /// how to use openXBL api https://xbl.io/getting-started
    /// </remarks>
    public class Program
    {
        private const char               CsvSeparator       = ';';
        private static readonly string[] CsvHeader          = new string[] { "#;User;Gamertag;XUID;Initial GS;Final GS;Gains;Points" };
        private static readonly string[] DiscourseHeader    = new string[] {
                                                                 "|#|User|Gamertag|XUID|Initial GS|Final GS|Gains|Points|",
                                                                 "| --- | --- | --- | --- | --- | --- | --- | --- |"
                                                              };

        /// <summary>
        /// the API key for openXBL needed to call their REST services (a new can be generated on their
        /// site on demand at no cost)
        /// </summary>
        private const string OpenXBLKey = "koso444og0w8w8ckw8s44sgk0gkwwos4g8s";

        public static async Task Main(string[] args)
        {
            if(args.Length < 2 || args.Length > 3)
            {
                Console.WriteLine("usage:");
                Console.WriteLine("XboxeraLeaderboard.exe $lastweek-filename $nextweek-filename ($output-for-xboxera)");
                Console.WriteLine("i.e. XboxeraLeaderboard.exe week31.csv week32.csv");
            }
            else
            {
                var input = await File.ReadAllLinesAsync(args[0]);

                // parsing input file

                var users = input.Where(l => !string.IsNullOrWhiteSpace(l))
                                 .Skip(1) // headline
                                 .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                 .Select(l => new { user = l[1], gamerTag = l[2], xuid = long.Parse(l[3]), lastScore = long.Parse(l[5]) });

                // getting current gamerscores from xbox

                var newScores = users.Select(g => new { g, newScore = ReadCurrentGamerScore(g.gamerTag, g.xuid).Result })
                                     .Select(n => new { n.g.user, n.g.gamerTag, n.g.xuid, n.g.lastScore, n.newScore, gains = Gains(n.g.lastScore, n.newScore) })
                                     .OrderByDescending(n => n.gains);

                // ranking (users with same gains have to be ranked the same!)

                var ranked = newScores.OrderByDescending(s => s.gains)
                                      .Select((s, i) => new { rank = i + 1, points = WeeklyPoints(i), s.gains, s.user, s.gamerTag, s.xuid, s.lastScore, s.newScore });

                var grouped = ranked.GroupBy(s => s.gains)
                                    .OrderByDescending(g => g.Key)
                                    .Select((s, i) => new { grouprank = s.Min(ss => ss.rank), grouppoints = s.Max(ss => ss.points), group = s.Select(i => i) })
                                    .SelectMany(g => g.group.Select(u => new { rank = g.grouprank, u, points = g.grouppoints }))
                                    .ToArray();

                // writing output
                // 1. first one in input csv format for the scanner itself or for excel 

                var toNextWeek = grouped.Select(g => string.Join(CsvSeparator,
                                                                 g.rank, g.u.user, g.u.gamerTag, g.u.xuid, g.u.lastScore, g.u.newScore, g.u.gains, g.points));
                await File.WriteAllLinesAsync(args[1], CsvHeader.Concat(toNextWeek));

                // 2. second one in Discourse table format for copy/pasting it to a forum post

                var toDiscourse = grouped.Select(g => $"|{g.rank}.|{(g.rank < 11 ? '@': ' ')}{g.u.user}|{g.u.gamerTag}|{g.u.lastScore}|{g.u.newScore}|{g.u.gains}|{g.points}|");

                if(args.Length == 3)
                {
                    await File.WriteAllLinesAsync(args[2], DiscourseHeader.Concat(toDiscourse));
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine();
                    foreach(var line in DiscourseHeader.Concat(toDiscourse))
                    {
                        Console.WriteLine(line);
                    }
                }
            }
        }

        private static long Gains(long gamerscoreBefore, long gamerscoreNow) => Math.Max(0, gamerscoreNow - gamerscoreBefore);

        private static int WeeklyPoints(int rank) => Math.Max(0, 10 - rank);

        /// <summary>
        /// calls open XBL api to get the gamerscore for a Xbox User Id (XUID)
        /// </summary>
        private static async Task<long> ReadCurrentGamerScore(string gamerTag, long xuid)
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

                    return long.Parse(gamerscoreSettings.Value);
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
