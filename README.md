# XboxEra Leaderboard

gamerscore scanner for XboxEra's achievement leaderboard

see https://www.trueachievements.com/leaderboard.aspx?leaderboardid=8223

## How do ranking and points work?

The ten best achievement hunters in the week will be awarded points based on their position. Whoever earns the most gamerscore during the week will gain 10 points, the second place will gain 9 points, and so on. Then the Ranking will reset again and a new week will begin. If two or more users have the same gamerscore gains in one week they get the same place and points.

The tracking will happen between 00:01AM on Monday and 11:59PM on Sunday UTC.

The ten best hunters in the month will also be awarded points based on their position. Whoever earns the most gamerscore during the month will gain 100 points, the second place will gain 90 points, and so on. Then the ranking will reset again and a new month will begin. If two or more users have the same gamerscore gains in the last week they get the same place and points.

## Usage

Compile the Code with VSCode or VS2019 and run it on the Cmdline with the following syntax:

`XboxeraLeaderboard.exe $lastweek-filename $nextweek-filename ($output-for-xboxera)`

|Parameter|Description|
|---------|-----------|
|$lastweek-filename|the name of the input csv file containing all data from last week including the XUIDs for all gamertags, see week31.csv|
|$nextweek-filename|the name of the output csv file the scanner writes. Has the same format as $lastweek-filename. This file is to be used as the input for the next weekly run|
|$output-for-xboxera|the name of the second file the scanner writes. Omits XUIDs and is in a format Discourse directly understands. This parameter is optional and if not provided the scanner outputs this content to stdout|

i.e. `XboxeraLeaderboard.exe week31.csv week32.csv`


