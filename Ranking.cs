
using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxeraLeaderboard;

internal record Ranking(string User,
                        string Gamertag,
                        long Xuid,
                        int Rank = 0,
                        int InitialGs = 0,
                        int FinalGs = 0,
                        int Gains = 0,
                        int Points = 0,
                        int InitialPoints = 0,
                        int NewPoints = 0);

/// <summary>
/// Extension class with all the math for regular scoring and the monhtly game.
/// </summary>
internal static class RankingExtension
{
    /// <summary>
    /// Rank users by gains of Gamerscore between periods (week, month) and
    /// scores their rank with 0..50 points.
    /// </summary>
    public static IEnumerable<Ranking> RankByGamerscoreGains(this IEnumerable<Ranking> toRank)
    {
        return toRank.Rank(s => s.Gains, (r, s) => Score(r, s))
                     .Select(r => r.score with
                      {
                          Rank = r.rank,
                          Points = r.points,
                          NewPoints = r.score.InitialPoints + r.points
                      })
                     .ToArray();
    }

    /// <summary>
    /// Ranks by new global leaderboard points.
    /// </summary>
    public static IEnumerable<Ranking> RankGlobal(this IEnumerable<Ranking> toRank)
    {
        return toRank.Rank(s => s.NewPoints, Identity)
                     .Select(r => r.score with { Rank = r.rank })
                     .ToArray();
    }

    /// <summary>
    /// ranks a sequence and scores it by rank (elements with same score get same rank)
    /// </summary>
    public static IEnumerable<(Ranking score, int rank, int points)> Rank(this IEnumerable<Ranking> toRank,
                                                                          Func<Ranking, int> rankBy,
                                                                          Func<int, Ranking, int> scoring)
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
    /// Scores the points a user gets for their gamerscore gains in a period:
    /// </summary>
    /// <remarks>
    /// - the user with the most gains gets 50 points
    /// - for every rank beneath the best user a user gets 2 points less
    /// - users with the same rank get the same points
    /// - every user with at least 1 gamerscore more than in the previous period gets at least 1 point
    /// </remarks>
    private static int Score(int rank, Ranking r) => r.Gains > 0 ? Math.Max(1, 50 - 2 * rank) : 0;

    private static T Identity<T, U>(T t, U u) => t;
}
