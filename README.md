# XboxEra Leaderboard

gamerscore scanner for XboxEra's achievement leaderboard

see https://www.trueachievements.com/leaderboard.aspx?leaderboardid=8223

## How do ranking and points work?

The ten best achievement hunters in the week will be awarded points based on their position. Whoever earns the most gamerscore during the week will gain 10 points, the second place will gain 9 points, and so on. Then the Ranking will reset again and a new week will begin. If two or more users have the same gamerscore gains in one week they get the same place and points.

The tracking will happen between 00:01AM on Monday and 11:59PM on Sunday UTC.

The ten best hunters in the month will also be awarded points based on their position. Whoever earns the most gamerscore during the month will gain 100 points, the second place will gain 90 points, and so on. Then the ranking will reset again and a new month will begin. If two or more users have the same gamerscore gains in the last week they get the same place and points.

## Usage

Compile the Code with VSCode or VS2019 and run it on the Cmdline with the following syntax:

### Calculate weekly ranking

This scans Xbox for the current gamerscore, calculates the weekly gamerscore gains of each member and ranks them accordingly. It also adds the points to their global leaderboard points. Both files are in a csv format with |-delimeters, which tools like Excel understand.

`XboxeraLeaderboard.exe $lastweek-filename $nextweek-filename`

|Parameter|Description|
|---------|-----------|
|$lastweek-filename|the name of the input csv file containing all data from last week including the XUIDs for all gamertags and their previous total leaderboard points (see week31.csv for an example)|
|$nextweek-filename|the name of the output csv file the tool should writes. Has the same format as $lastweek-filename. This file is to be used as the input for the next weekly run. Contains the new total gamerscore, gains since last run, the weekly points ranking and the new total leaderboard points.|

The csv-Files can't be used directly in forum posts and contain additional information like XUIDs. To make it simpler to just copy & paste the tables to forum posts, the scanner outputs these information to stdout.

i.e. `XboxeraLeaderboard.exe week31.csv week32.csv`

### Automatic scheduled weekly run

Functions mostly the same as in manual weekly run, but operates with a fixed directory structure inside the repository (./doc/scores).

`XboxeraLeaderboard.exe --weekly $scores-subdir`

|Parameter|Description|
|---------|-----------|
|--weekly|indicates to calculate the weekly gains and write them to the file structure in a new weekXYZ.csv file. Updates $scores-subdir/lastscanstats.txt.|
|$scores-subdir|directory structure with all weekly csv files grouped by month|

This is run by the *weekly* GitHub action every Monday at 06:20 UTC.

i.e. `XboxeraLeaderboard.exe --weekly ./doc/scores`
