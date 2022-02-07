using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

using static System.Math;

namespace XboxeraLeaderboard;

/// <summary>
/// for parsing the open XBL api JSON response and local JSON Files
/// </summary>
internal record OpenXblSettings(string Id, string Value);
internal record OpenXblTitle(long TitleId, string Name);
internal record OpenXblAchievement(string ProgressState, OpenXblReward[] Rewards);
internal record OpenXblReward(int Value);

internal record ScanSettings(int Week, string Date, string MonthlyGame);

internal record Ranking(string User, string Gamertag, long Xuid, int Rank = 0, int InitialGs = 0, int FinalGs = 0, int Gains = 0, int Points = 0, int InitialPoints = 0, int NewPoints = 0);

public class Program
{
    private const int                MaxHttpRetries         = 3;
    private const char               CsvSeparator           = ';';
    private const string             StatsFilename          = "scansettings.json";

    private const string OpenXblPlayerStats  = "https://xbl.io/api/v2/account";
    private const string OpenXblPlayerTitles = "https://xbl.io/api/v2/achievements/player";

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
    private const string OpenXBLKey = "skooks048gw80ks0co8wk4ow0k0ggksoks8";

    /// <summary>
    /// Takes a list of gamertags in csv format (with their xuid and last weeks gamerscore),
    /// polls the open XBL api for their current gamerscore and outputs the (weekly) leaderboard
    /// in a table format Discourse understands.
    /// 
    /// operates in 3 different modes:
    /// - if the caller specifies option --weekly the programm scans the path for the last
    ///   weekly file, calculates the weekly diff and writes the new weekly and global scores
    ///   to a new file in the path. it also writes a second text file in Discourse format.
    /// - if the caller specifies option --monthly the programm first scans all users for
    ///   the highest score for the monthly game (this games name is either specified on the
    ///   commandline or in lastscanstats.txt). The the program searches the path for the last
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
            Console.WriteLine("XboxeraLeaderboard.exe --weekly(=\"game-title\") $scores-path");
            Console.WriteLine("XboxeraLeaderboard.exe --monthly(=\"game-title\") $scores-path");
            Console.WriteLine("  or XboxeraLeaderboard.exe --weekly ./docs/scores");

            return;
        }

        // directory structure: ./doc/scoring/lastscanstats.txt
        //                                   /YYYY-MM/month.csv
        //                                            week1.csv
        //                                            week1.txt
        //                                            week2.csv
        //                                            week2.txt
        //                                            ...
        // ./doc/scoring == rootDir

        var rootDir         = Path.GetFullPath(args[1]);
        var currentMonthDir = LatestDir(rootDir);
        var settings        = JsonConvert.DeserializeObject<ScanSettings>(File.ReadAllText(Path.Combine(rootDir, StatsFilename)));

        var prevGlobalPoints = GetPreviousGlobalPoints(rootDir, currentMonthDir);

        if(args[0] == "--weekly")
        {
            var ranking = InitRankingWithLastWeeklyScore(rootDir, currentMonthDir, settings.Week)
                          .Join(prevGlobalPoints, w => w.Xuid, m => m.Key, (r, gp) => r with { InitialPoints = gp.Value, NewPoints = 0 })
                          .ToArray();

            if(MonthlyGame(args, settings) is var monthlyGame && !string.IsNullOrWhiteSpace(monthlyGame))
            {
                var gameRanking = RankMonthlyGame(monthlyGame,
                                                  ranking.Select(u => u with { InitialGs = 0, Points = 0 }).ToArray(),
                                                  rootDir,
                                                  currentMonthDir);

                ranking = ranking.Join(gameRanking, w => w.Xuid, mg => mg.Xuid, (w, mg) => w with { InitialPoints = mg.NewPoints })
                                 .ToArray();
            }

            RankWeekly(ranking, rootDir, currentMonthDir, settings.Week + 1);

            settings = settings with { Week = settings.Week + 1 };
            UpdateSettingsFile(rootDir, settings);
        }
        else if(args[0].StartsWith("--monthly"))
        {
            var ranking = InitRankingWithLastMonthlyScore(rootDir)
                          .Join(prevGlobalPoints, w => w.Xuid, m => m.Key, (r, gp) => r with { InitialPoints = gp.Value })
                          .ToArray();

            if(MonthlyGame(args, settings) is var monthlyGame && !string.IsNullOrWhiteSpace(monthlyGame))
            {
                var gameRanking = RankMonthlyGame(monthlyGame,
                                                  ranking.Select(u => u with { InitialGs = 0, Points = 0 }).ToArray(),
                                                  rootDir,
                                                  currentMonthDir);

                ranking = ranking.Join(gameRanking, m => m.Xuid, mg => mg.Xuid, (m, mg) => m with { InitialPoints = mg.NewPoints })
                                 .ToArray();
            }

            RankMonthly(ranking, rootDir, currentMonthDir);
        }
    }

    private static string MonthlyGame(string[] args, ScanSettings settings)
        => args[0].Length > 9 ? args[0][10..].Trim('\"').ToLower() : settings.MonthlyGame.Trim().ToLower();

    private static IDictionary<long, int> GetPreviousGlobalPoints(string rootDir, string currentMonthDir)
    {
        var filesWithPoints = Directory.GetFiles(currentMonthDir, "*.csv");

        if(!filesWithPoints.Any())
        {
            filesWithPoints = Directory.GetFiles(LatestDir(rootDir, 1), "*.csv");
        }

        return filesWithPoints.SelectMany(f => ReadCsv(f))
                              .GroupBy(r => r.Xuid)
                              .ToDictionary(g => g.Key, g => g.Max(r => r.NewPoints));
    }

    private static Ranking[] InitRankingWithLastWeeklyScore(string rootDir, string currentMonthDir, int weekNr)
    {
        var latestWeeklyCsv = Path.Combine(currentMonthDir, $"week{weekNr}.csv");

        if(!File.Exists(latestWeeklyCsv))
        {
            latestWeeklyCsv = Path.Combine(LatestDir(rootDir, 1), $"week{weekNr}.csv");
        }

        return ReadCsv(latestWeeklyCsv)
               .Select(u => u with { InitialGs = u.FinalGs })
               .ToArray();
    }

    private static Ranking[] InitRankingWithLastMonthlyScore(string rootDir)
    {
        var latestMonthlyCsv = Path.Combine(LatestDir(rootDir, 1), "month.csv");

        return ReadCsv(latestMonthlyCsv)
               .Select(u => u with { InitialGs = u.FinalGs })
               .ToArray();
    }

    private static Ranking[] RankWeekly(IEnumerable<Ranking> users, string rootDir, string currentMonthDir, int weekNr)
    {
        var newGamerscores = ReadAllNewGamerscores(users);

        // weekly and new global ranking (users with same gains have to be ranked the same!)
        // and directly add points to total leaderboard points

        var weeklyRanking = Rank(newGamerscores, s => s.Gains, (r, s) => Score(r, s))
                            .Select(r => r.score with {
                                Rank = r.rank,
                                Points = r.points,
                                NewPoints = r.score.InitialPoints + r.points })
                            .ToArray();

        var globalRanking = Rank(weeklyRanking, s => s.NewPoints, Identity)
                            .Select(r => r.score with { Rank = r.rank })
                            .ToArray();

        // writing output (csv + discourse)

        var thisWeekFilename = Path.Combine(currentMonthDir, $"week{weekNr}.csv");
        WriteCsv(thisWeekFilename, weeklyRanking);

        var discouresMarkdown = CreateDiscourseMarkdown(weeklyRanking, globalRanking);
        WriteWeeklyGithubPage(rootDir, currentMonthDir, weekNr, discouresMarkdown);

        return globalRanking;
    }

    private static Ranking[] RankMonthly(IEnumerable<Ranking> users, string rootDir, string currentMonthDir)
    {
        var newGamerscores = ReadAllNewGamerscores(users);

        // monthly ranking by gamerscore gains (users with same gains have to be ranked the same!) and
        // global ranking by new total leaderboard points
        // new_leaderboard_points = last_month_points + sum(weekly_points_of_month) + monthlyRanking

        var monthlyRanking = Rank(newGamerscores, s => s.Gains, (r, s) => Score(r, s))
                             .Select(r => r.score with {
                                 Rank = r.rank,
                                 Points = r.points,
                                 NewPoints = r.score.InitialPoints + r.points })
                             .ToArray();

        var globalRanking = Rank(monthlyRanking, s => s.NewPoints, Identity)
                            .Select(r => r.score with { Rank = r.rank })
                            .ToArray();

        // writing output (csv + discourse)

        WriteCsv(Path.Combine(currentMonthDir, "month.csv"), monthlyRanking);

        var discourseMarkdown = CreateDiscourseMarkdown(monthlyRanking, globalRanking);
        WriteMonthlyGithubPage(rootDir, Path.GetFileName(currentMonthDir), discourseMarkdown);

        // create empty dir for next month

        var nextMonthDir = Path.Combine(rootDir, $"{DateTime.Today:yyyy-MM}");
        Directory.CreateDirectory(nextMonthDir);
        File.WriteAllText(Path.Combine(nextMonthDir, ".gitignore"), "#empty");

        return globalRanking;
    }

    private static int Gains(int gamerscoreBefore, int gamerscoreNow) => Max(0, gamerscoreNow - gamerscoreBefore);

    private static Ranking[] RankMonthlyGame(string gameName, IEnumerable<Ranking> users, string rootDir, string currentMonthDir)
    {
        // calls open XBL api to get the title-ID for all games a user has played
        // searches all games all users have played till the first match is found
        //
        // response JSON looks like
        // {
        //   "xuid": "2533274817036922",
        //   "titles": [
        //   {
        //     "titleId": "1997023214",
        //     "name": "FINAL FANTASY IX",
        //     "type": "Game"
        //     }, {
        //     ...

        Console.WriteLine($"searching for any user who played '{gameName}' to get its Xbox Title-ID");

        var gameTitleId = users.Select(u => CallOpenXblApi($"{OpenXblPlayerTitles}/{u.Xuid}",
                                                           j => j["titles"].Select(t => t.ToObject<OpenXblTitle>()))
                                            .FirstOrDefault(t => t.Name.ToLower() == gameName)
                                            ?.TitleId)
                               .First(t => t.HasValue).Value;

        // calls open XBL api to sum all gamerscores of a user for gameTitleId
        //
        // response JSON looks like
        // {
        // "achievements": [
        //   {
        //     "id": "1",
        //     "name": "Welcome to Britain",
        //     "progressState": "Achieved",
        //     "rewards": [
        //       {
        //         "value": "10",
        //         "type": "Gamerscore",
        //       }, ...

        Console.WriteLine($"get gamerscores of title {gameTitleId} for all users");

        var gamerscoresForTitle = users.Select(u => u with { Gains = CallOpenXblApi($"{OpenXblPlayerTitles}/{u.Xuid}/title/{gameTitleId}",
                                                                                   j => j["achievements"].Select(a => a.ToObject<OpenXblAchievement>()))
                                                                     .Where(a => a.ProgressState == "Achieved")
                                                                     .Sum(a => a.Rewards.First().Value) })
                                       .ToArray();

        // now ranking on gains for the montly game but only the best with the most gamerscore gains get points in this case

        var titleRanking = Rank(gamerscoresForTitle, s => s.Gains, (r, s) => Score(r, s))
                            .Select(r => r.score with {
                                Rank = r.rank,
                                FinalGs = r.score.Gains,
                                Points = r.points,
                                NewPoints = r.score.InitialPoints + r.points })
                            .ToArray();

        var globalRanking = Rank(titleRanking, s => s.NewPoints, Identity)
                            .Select(r => r.score with { Rank = r.rank })
                            .ToArray();

        // writing output (csv + discourse)

        WriteCsv(Path.Combine(currentMonthDir, $"{GameToFilename(gameName)}.csv"), titleRanking);

        var discourceMarkdown = CreateDiscourseMarkdown(titleRanking, globalRanking);
        WriteMonthlyGameGithubPage(rootDir, Path.GetFileName(currentMonthDir), gameName, discourceMarkdown);

        return globalRanking;
    }

    private static string GameToFilename(string title) => title.Replace(" ", "").Replace(":", "");

    private static IEnumerable<Ranking> ReadAllNewGamerscores(IEnumerable<Ranking> users)
    {
        Console.WriteLine($"get current gamerscore for all users");

        return users.Select(u => u with { FinalGs = ReadCurrentGamerScore(u.Xuid)})
                    .Select(u => u with { Gains = Gains(u.InitialGs, u.FinalGs) })
                    .ToArray();
    }

    private static int ReadCurrentGamerScore(long xuid)
    {
        // calls open XBL api to get the gamerscore for a Xbox User Id(XUID)
        //
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

        return CallOpenXblApi($"{OpenXblPlayerStats}/{xuid}",
                              j => int.Parse(j["profileUsers"].First()["settings"].Children()
                                             .Select(s => s.ToObject<OpenXblSettings>())
                                             .First(s => s.Id == "Gamerscore").Value));
    }

    /// <summary>
    /// calls open XBL api rest service and parse Json result
    /// </summary>
    private static T CallOpenXblApi<T>(string openXblUrl, Func<JObject, T> parse)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"calling open XBL api for {openXblUrl} .... ");

        int retries = 0;
        string json    = string.Empty;

        do
        {
            try
            {
                using var httpClient = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method     = HttpMethod.Get,
                    RequestUri = new Uri(openXblUrl),
                };
                request.Headers.Add("X-Authorization", OpenXBLKey);

                var response = httpClient.Send(request);

                if(response.IsSuccessStatusCode)
                {
                    json   = response.Content.ReadAsStringAsync().Result;
                    var result = parse(JObject.Parse(json));

                    // open XBL api only allows 10 requests per 15 seconds, so give it some time
                    System.Threading.Thread.Sleep(2500);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK");

                    return result;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"error = {response.ReasonPhrase}");
                }
            }
            catch(Exception exc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"error = {exc.Message}, response = '{json}'");
            }

            // request probably failed because open XBL limited the request rate, so give it some time
            System.Threading.Thread.Sleep(10000);
        }
        while(retries++ < MaxHttpRetries);

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\nMaximum number of allowed retries (={MaxHttpRetries}) exceeded. Aborting program execution!");
        throw new InvalidOperationException("retries exceeded");
    }

    /// <summary>
    /// ranks a sequence and scores it by rank (elements with same score get same rank)
    /// </summary>
    private static IEnumerable<(T score, int rank, int points)> Rank<T>(IEnumerable<T> toRank,
                                                                        Func<T, int> rankBy,
                                                                        Func<int, T, int> scoring)
    {
        var ranked = toRank.OrderByDescending(rankBy)
                           .Select((s, i) => (rank: i + 1, points: scoring(i, s), s));

        var grouped = ranked.GroupBy(r => rankBy(r.s))
                            .OrderByDescending(g => g.Key)
                            .Select((g, i) => (grouprank: g.Min(r => r.rank), grouppoints: g.Max(r => r.points), group: g.Select(i => i)))
                            .SelectMany(g => g.group.Select(s => (s.s, g.grouprank, g.grouppoints)));

        return grouped;
    }

    /// <summary>
    /// scores 
    /// </summary>
    private static int Score(int rank, Ranking r) => r.Gains > 0 ? Max(1, 50 - 2 * rank) : 0;

    private static T Identity<T>(T t) => t;
    private static T Identity<T, U>(T t, U u) => t;

    private static string LatestDir(string path, int skip = 0) => Path.Combine(path,
                                                                               Directory.EnumerateDirectories(path)
                                                                                        .OrderByDescending(s => s)
                                                                                        .Skip(skip)
                                                                                        .First());

    private static IEnumerable<Ranking> ReadCsv(string filename)
    {
        var file = File.ReadAllLines(filename);

        return file.Where(l => !string.IsNullOrWhiteSpace(l))
                   .Skip(1) // header
                   .Select(l => l.Split(CsvSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                   .Select(l => new Ranking(User: l[1], Gamertag: l[2], Xuid: long.Parse(l[3]),
                                            InitialGs: int.Parse(l[4]), FinalGs: int.Parse(l[5]), Gains: int.Parse(l[6]),
                                            Points: int.Parse(l[7]), InitialPoints: int.Parse(l[8]), NewPoints: int.Parse(l[9])));
    }

    private static void WriteCsv(string filename, IEnumerable<Ranking> ranking)
    {
        var csvData = ranking.Select(w => string.Join(CsvSeparator,
                                                      w.Rank, w.User, w.Gamertag, w.Xuid, w.InitialGs, w.FinalGs, w.Gains, w.Points, w.InitialPoints, w.NewPoints));
        File.WriteAllLines(filename, CsvHeader.Concat(csvData));
        Console.WriteLine($"wrote csv file {filename}");
    }

    private static void UpdateSettingsFile(string rootDir, ScanSettings settings)
    {
        settings = settings with { Date = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} (UTC)" };
        File.WriteAllText(Path.Combine(rootDir, StatsFilename),
                          JsonConvert.SerializeObject(settings, Formatting.Indented));

        Console.WriteLine($"updated stats file");
    }

    private static void WriteWeeklyGithubPage(string rootDir, string currentDir, int weekNumber, string[] discourse)
    {
        var currentMonth = Path.GetDirectoryName(currentDir);
        var filename = Path.Combine(Directory.GetParent(rootDir).FullName,
                                    "_posts",
                                    $"{DateTime.UtcNow:yyyy-MM-dd}-scan-week-{weekNumber}.md");

        File.WriteAllLines(filename,
                           BuildGithubPage("weekly", $"Week {weekNumber}", $"scores/{currentMonth}/week{weekNumber}.csv", discourse));
        Console.WriteLine($"wrote github page {filename}");
    }

    private static void WriteMonthlyGithubPage(string rootDir, string currentDir, string[] discourse)
    {
        var currentMonth = Path.GetDirectoryName(currentDir);
        var filename = Path.Combine(Directory.GetParent(rootDir).FullName,
                                    "_posts",
                                    $"{DateTime.UtcNow:yyyy-MM-dd}-scan-month-{currentMonth}.md");
        File.WriteAllLinesAsync(filename,
                                BuildGithubPage("monthly", $"Month {currentMonth}", $"scores/{currentMonth}/month.csv", discourse));
        Console.WriteLine($"wrote github page {filename}");
    }

    private static void WriteMonthlyGameGithubPage(string rootDir, string currentDir, string gameName, string[] discourse)
    {
        var currentMonth = Path.GetDirectoryName(currentDir);
        var filename = Path.Combine(Directory.GetParent(rootDir).FullName,
                                    "_posts",
                                    $"{DateTime.UtcNow:yyyy-MM-dd}-scan-game-{GameToFilename(gameName)}.md");
        File.WriteAllLinesAsync(filename,
                                BuildGithubPage("monthly", $"Game {gameName} for {currentMonth}", $"scores/{currentMonth}/{GameToFilename(gameName)}.csv", discourse));
        Console.WriteLine($"wrote github page {filename}");
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

    private static string[] CreateDiscourseMarkdown(IEnumerable<Ranking> ranking, IEnumerable<Ranking> globalRanking)
    {
        var markdownTableForRanking = ranking.Select((r, i) => $"|{r.Rank}.|{(i < 10 ? '@' : ' ')}{r.User}|{r.Gamertag}|{r.InitialGs}|{r.FinalGs}|{r.Gains}|{r.Points}|");
        var markdownTableForGlobal  = globalRanking.Select(r => $"|{r.Rank}.|{r.User}|{r.Gamertag}|{r.NewPoints}|");

        return DiscoursePeriodHeader.Concat(markdownTableForRanking)
                                    .Concat(new[] { "\n" })
                                    .Concat(DiscourseSummaryHeader)
                                    .Concat(markdownTableForGlobal)
                                    .ToArray();
    }
}
