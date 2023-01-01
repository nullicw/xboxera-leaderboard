using Newtonsoft.Json.Linq;

using System;
using System.Linq;
using System.Net.Http;

namespace XboxeraLeaderboard;

/// <summary>
/// Encapsulates all calls to the Open Xbox Live API to retrieve achivements data for users.
/// </summary>
/// <remarks>
/// Calls the Open XBL API for every user to get their achievement information. Its important
/// to point out that this API doesn't identiy users with their gamertag but instead uses
/// unique XUIDs (they stay the same even if a user renames his gamertag).
/// 
/// Open XBL API is a free community service so calls are throttled. This results in a relatively
/// long time for retrieving all information (couple minutes for 50 users).
/// 
/// get xuids for gamertag: https://www.cxkes.me/xbox/xuid
/// how to use openXBL api https://xbl.io/getting-started
/// </remarks>
internal class OpenXblApi
{
    /// <summary>
    /// for parsing the open XBL api JSON response and local JSON Files
    /// </summary>
    internal record OpenXblSettings(string Id, string Value);
    internal record OpenXblTitle(long TitleId, string Name, OpenXblAchievement Achievement);
    internal record OpenXblAchievement(long CurrentAchievements, long CurrentGamerscore);

    private const int MaxHttpRetries = 3;

    private const string OpenXblPlayerStats  = "https://xbl.io/api/v2/account";
    private const string OpenXblPlayerTitles = "https://xbl.io/api/v2/achievements/player";

    /// <summary>
    /// the API key for openXBL needed to call their REST services (a new can be generated on their
    /// site on demand at no cost)
    /// </summary>
    private const string OpenXBLKey = "skooks048gw80ks0co8wk4ow0k0ggksoks8";

    /// <summary>
    /// Returns the complete account information for a single user profile in JSON format.
    /// </summary>
    public static string DumpAccountInfo(long xuid)
    {
        return CallOpenXblApi($"{OpenXblPlayerStats}/{xuid}", (json) => json.ToString());
    }

    public static int GetCurrentGamerScore(long xuid)
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

    public static long? GetTitleId(long xuid, string gameName)
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
        //     "achievement": {
        //       "currentAchievements": 17,
        //       "totalAchievements": 0,
        //       "currentGamerscore": 435,
        //       "totalGamerscore": 1000,
        //     }, {
        //     ...

        return CallOpenXblApi($"{OpenXblPlayerTitles}/{xuid}",
                              j => j["titles"].Select(t => t.ToObject<OpenXblTitle>()))
               .FirstOrDefault(t => t.Name.ToLower() == gameName)
               ?.TitleId;
    }

    public static int GetGamerscoreForTitle(long xuid, long titleId)
    {
        // calls open XBL api to get the gamerscore for a title-ID
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

        var chievosForTitle = CallOpenXblApi($"{OpenXblPlayerTitles}/{xuid}",
                                             j => j["titles"].Select(t => t.ToObject<OpenXblTitle>()))
                              .FirstOrDefault(t => t.TitleId == titleId);
        return (int)(chievosForTitle?.Achievement.CurrentGamerscore ?? 0);
    }

    /// <summary>
    /// calls open XBL api rest service and parse Json result
    /// </summary>
    private static T CallOpenXblApi<T>(string openXblUrl, Func<JObject, T> parse)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"calling open XBL api for {openXblUrl} .... ");

        int retries = 0;
        string json = string.Empty;

        do
        {
            try
            {
                using var httpClient = new HttpClient();

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(openXblUrl),
                };
                request.Headers.Add("X-Authorization", OpenXBLKey);

                var response = httpClient.Send(request);

                if(response.IsSuccessStatusCode)
                {
                    json = response.Content.ReadAsStringAsync().Result;
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
}
