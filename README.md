[![.NET build](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/dotnet-build.yml/badge.svg?branch=main)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/dotnet-build.yml)

# XboxEra Leaderboard

gamerscore scanner for XboxEra's achievement leaderboard

see https://www.trueachievements.com/leaderboard.aspx?leaderboardid=8223

## How do ranking and points work?

The ten best achievement hunters in the week will be awarded points based on their position. Whoever earns the most gamerscore during the week will gain 10 points, the second place will gain 9 points, and so on. Then the Ranking will reset again and a new week will begin. If two or more users have the same gamerscore gains in one week they get the same place and points.

The weekly tracking will happen around 06:20AM on Monday UTC and cover the last 7 days.

Whoever had the most Gamerscore in the featured monthly game earns 100 points. If two or more people happen to share the same amount of Gamerscore in said game, they will all get the 100 points.

The ten best hunters in the month will also be awarded points based on their position. Whoever earns the most gamerscore during the month will gain 100 points, the second place will gain 90 points, and so on. Then the ranking will reset again and a new month will begin. If two or more users have the same gamerscore gains in the last week they get the same place and points.

## Usage

Clone the repository, compile the Code with VSCode or VS2019 or use the compiled windows executable (see https://github.com/nullicw/xboxera-leaderboard/releases) and run it on the cmdline with one of the following options:

### Calculate weekly ranking

This scans Xbox for the current gamerscore, calculates the weekly gamerscore gains of each user and ranks them accordingly. It also adds the points to the global leaderboard. Both files are in csv format with ; delimeters.

`XboxeraLeaderboard.exe $lastweek-filename $nextweek-filename`

|Parameter|Description|
|---------|-----------|
|$lastweek-filename|the name of the input csv file containing all data from last week including the XUIDs for all gamertags and their previous total leaderboard points (see week31.csv for an example)|
|$nextweek-filename|the name of the output csv file the tool should writes. Has the same format as $lastweek-filename. This file is to be used as the input for the next weekly run. Contains the new total gamerscore, the gains since last run, the weekly points ranking and the new total leaderboard points.|

i.e. `XboxeraLeaderboard.exe week31.csv week32.csv`

The csv-Files can't be used directly in forum posts and contain additional information like XUIDs. To make it simpler the scanner outputs the tables to stdout in Discourse's  format. Just copy & paste this to a forum post.

### Scheduled weekly run

Behaves mostly the same as the manual weekly run, but operates with a fixed directory structure inside the repository (./doc/scores).

`XboxeraLeaderboard.exe --weekly $scores-subdir`

|Parameter|Description|
|---------|-----------|
|--weekly|indicates to calculate the weekly gamerscore gains and write them to the file structure in a new weekXYZ.csv file. Updates $scores-subdir/lastscanstats.txt.|
|$scores-subdir|directory structure with all weekly csv files grouped by month|

i.e. `XboxeraLeaderboard.exe --weekly ./doc/scores`

This is also the behavior used by the *weekly* GitHub action which runs every Monday at 06:20 UTC. This action writes a new weekyXYZ.csv and a new weekly GitHub Page with all the information for a forum post.

[![Generate weekly leaderboard](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/weekly.yml/badge.svg)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/weekly.yml)

### Scheduled monthly run

Behaves mostly the same as the scheduled weekly run. Adds all points in weekly.csv files and points from special events like a game of the month to the global leaderboard.

`XboxeraLeaderboard.exe --monthly $scores-subdir`

|Parameter|Description|
|---------|-----------|
|--monthly|indicates to calculate the monthly gamerscore gains and write them to the file structure in a new month.csv file. Updates $scores-subdir/lastscanstats.txt.|
|$scores-subdir|directory structure with all weekly and monthly csv files grouped by month|

i.e. `XboxeraLeaderboard.exe --monthly ./doc/scores`

This is also the behavior used by the *monthly* GitHub action which runs every first day of the month at 06:40 UTC. This action writes a new month.csv and a new monthly GitHub Page with all the information for a forum post.

[![Generate monthly leaderboard](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/monthly.yml/badge.svg)](https://github.com/nullicw/xboxera-leaderboard/actions/workflows/monthly.yml)

### Rank the monthly featured game

Calculated who had the most Gamerscore for the monthly featured game and awards them 100 points for the the global leaderboard. Note: the specified name of the game has to be the _exact_ title on Xbox Live. If the title contains spaces, enclose the game name on the commandline with ".

`XboxeraLeaderboard.exe --game=$featured-game $scores-subdir`

|Parameter|Description|
|---------|-----------|
|--game=$featured-game|indicates the featured game to scan all users and to write the file for the game as $featuredgame.csv in the monthly folder|
|$scores-subdir|directory structure with all weekly and monthly csv files grouped by month|

i.e. `XboxeraLeaderboard.exe --game="Dragon Quest Builders 2" ./doc/scores`
