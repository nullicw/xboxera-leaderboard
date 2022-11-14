using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XboxeraLeaderboard;

/// <summary>
/// encapsulates writing the rankings to the Github Pages format and directory structure.
/// </summary>
internal class GithubPagesWriter
{
    private static readonly string[] DiscoursePeriodHeader  = new string[] {
                                                                  "|#|User|Gamertag|Initial GS|Final GS|Gains|Points|",
                                                                  "| ---: | --- | --- | ---: | ---: | ---: | ---: |"
                                                              };
    private static readonly string[] DiscourseSummaryHeader  = new string[] {
                                                                   "|#|User|Gamertag|Points|",
                                                                   "| ---: | --- | --- | ---: |"
                                                               };

    protected string RootDir { get; private set; }
    protected string CurrentDir { get; private set; }

    public GithubPagesWriter(string rootDir, string currentDir)
    {
        RootDir    = rootDir;
        CurrentDir = currentDir;
    }

    public void WriteWeeklyGithubPage(int weekNumber, IEnumerable<Ranking> weeklyRanking, IEnumerable<Ranking> globalRanking)
    {
        var currentMonth = Path.GetFileName(CurrentDir);
        var filename     = Path.Combine(Directory.GetParent(RootDir).FullName,
                                        "_posts",
                                        $"{DateTime.UtcNow:yyyy-MM-dd}-scan-week-{weekNumber}.md");

        var markdown = CreateDiscourseMarkdown(weeklyRanking, globalRanking);
        File.WriteAllLines(filename,
                           BuildGithubPage("weekly", $"Week {weekNumber}", $"scores/{currentMonth}/week{weekNumber}.csv", markdown));
        Console.WriteLine($"wrote github page {filename}");
    }

    public void WriteMonthlyGithubPage(IEnumerable<Ranking> monthlyRanking, IEnumerable<Ranking> globalRanking)
    {
        var currentMonth = Path.GetFileName(CurrentDir);
        var filename     = Path.Combine(Directory.GetParent(RootDir).FullName,
                                        "_posts",
                                        $"{DateTime.UtcNow:yyyy-MM-dd}-scan-month-{currentMonth}.md");

        var markdown = CreateDiscourseMarkdown(monthlyRanking, globalRanking);
        File.WriteAllLines(filename,
                           BuildGithubPage("monthly", $"Month {currentMonth}", $"scores/{currentMonth}/month.csv", markdown));
        Console.WriteLine($"wrote github page {filename}");
    }

    public void WriteMonthlyGameGithubPage(string gameName, string gameFilename, IEnumerable<Ranking> gameRanking, IEnumerable<Ranking> globalRanking)
    {
        var currentMonth = Path.GetFileName(CurrentDir);
        var filename     = Path.Combine(Directory.GetParent(RootDir).FullName,
                                        "_posts",
                                        $"{DateTime.UtcNow:yyyy-MM-dd}-scan-game-{gameFilename}.md");

        var markdown = CreateDiscourseMarkdown(gameRanking, globalRanking);
        File.WriteAllLines(filename,
                           BuildGithubPage("monthly", $"Game {gameName} for {currentMonth}", $"scores/{currentMonth}/{gameFilename}.csv", markdown));
        Console.WriteLine($"wrote github page {filename}");
    }

    /// <summary>
    /// Builds the standard markdown with all necessary properties like layout and tags (month/week) for a simple Github Page containng a
    /// link to the csv Excel file and the Discourse tables (as embedded code).
    /// </summary>
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

    /// <summary>
    /// Builds the Discourse compatible markdown for both the new ranking table and the updated global ranking table.
    /// </summary>
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
