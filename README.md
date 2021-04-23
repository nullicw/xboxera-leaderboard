# XboxEra Leaderboard

gamerscore scanner for XboxEra's achievement leaderboard

see https://www.trueachievements.com/leaderboard.aspx?leaderboardid=8223


## Usage

Compile the Code with VSCode or VS2019 and run it on the Cmdline with the following syntax:

`XboxeraLeaderboard.exe $lastweek-filename $nextweek-filename ($output-for-xboxera)`

|Parameter|Description|
|---------|-----------|
|$lastweek-filename|the name of the input file containing all data from last week including the XUIDs for all gamertags, see week31.txt|
|$nextweek-filename|the name of the output file the scanner writes. Has the same format as $lastweek-filename. This file is to be used as the input for the next weekly run|
|$output-for-xboxera|the name of the second file the scanner writes. Omits XUIDs and is in a format Discourse directly understands. This parameter is optional and if not provided the scanner outputs this content on stdout|

i.e. `XboxeraLeaderboard.exe week31.txt week32.txt`


