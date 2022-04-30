[![.NET build](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/dotnet-build.yml/badge.svg?branch=main)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/dotnet-build.yml) [![stats](https://img.shields.io/badge/stats-archived-brightgreen)](https://nullicw.github.io/xboxera-leaderboard/)

# XboxEra Leaderboard

gamerscore scanner for XboxEra's achievement leaderboard

see https://www.trueachievements.com/leaderboard.aspx?leaderboardid=8223

## How do ranking and points work?

Distribution of points for weekly and monthly ranking and for the featured monthly game work all the same. The 25 best achievement hunters in the specific time frame will be awarded points based on their position. Whoever earns the most gamerscore will gain 50 points, the second place will gain 48 points, and so on. Everybody with at least 1 gamerscore gain will also get 1 point. If two or more users have the same gamerscore gains they get the same place and points.

The weekly tracking will happen around 06:20AM on Monday UTC and cover the last 7 days.

## Usage

Clone the repository, compile the Code with VSCode or VS2019 or use the compiled windows executable (see https://github.com/nullicw/xboxera-leaderboard/releases) and run it on the cmdline with one of the following options:

### Calculate weekly ranking

This scans Xbox for the current gamerscore, calculates the weekly gamerscore gains of each user and ranks them accordingly. It also adds the points to the global leaderboard. Both files are in csv format with ; delimeters.

`XboxeraLeaderboard.exe weekly $scores-subdir`

|Parameter|Description|
|---------|-----------|
|weekly|Indicates to calculate the weekly gamerscore gains and write them to the file structure in a new weekXYZ.csv file. Updates $scores-subdir/lastscanstats.txt.|
|$scores-subdir|Directory structure with all weekly csv files grouped by month|

i.e. `XboxeraLeaderboard.exe weekly ./doc/scores`

This is also the behavior used by the *weekly* GitHub action which runs every Monday at 06:20 UTC. This action writes a new weekyXYZ.csv and a new weekly GitHub Page with all the information for a forum post.

[![Generate weekly leaderboard](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/weekly.yml/badge.svg)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/weekly.yml)

### Calculate monthly ranking

Behaves mostly the same as the weekly calculation. Adds all points in weekly.csv files and points from special events like a game of the month to the global leaderboard. Adds optional points for the monthly featured game if specified. 

`XboxeraLeaderboard.exe monthly [--game $monthly-game] $scores-subdir`

|Parameter|Description|
|---------|-----------|
|monthly|Indicates to calculate the monthly gamerscore gains and write them to the file structure in a new month.csv file. Updates $scores-subdir/lastscanstats.txt.|
|$monthly-game|Optional name of the monthly featured game. Will be calculated before the summation of all weekly scores for the monthly leaderboard. If ommited the program will use the $MonthlyGame value from /docs/scores/scansettings.json |
|$scores-subdir|Directory structure with all weekly and monthly csv files grouped by month|

i.e. `XboxeraLeaderboard.exe monthly --game Bugsnax ./doc/scores`

This is also the behavior used by the *monthly* GitHub action which runs every first day of the month at 06:40 UTC. This action writes a new month.csv and a new monthly GitHub Page with all the information for a forum post.

[![Generate monthly leaderboard](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/monthly.yml/badge.svg)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/monthly.yml)
