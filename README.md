# DataReader
This command line application, written in C#, reads data from the OSIsoft PI Data Archive.  
It was created to extract very large amounts of data in an efficient manner, using knowledge of the internals of the PI Data Archive and the Windows system to maximize throughput.

The application supports two modes:
- **Raw Data Mode**: Extract raw archived values from PI tags
- **Summary/Aggregate Mode**: Extract calculated summaries (average, min, max, totals, etc.) over specified intervals

# Build
Once compiled, it creates a **Build** folder in the solution folder. You can take this folder and place it on the system you would like to make the test on.

# Prerequisites on host system
* .NET Framework 4.5+
* AFSDK 2.8+, for bulk calls support and also for the new *PIPointQuery.ParseQuery* method introduced in this version.

# Getting Started

## Initial Step - Test PI Point Query(ies)

Before starting your data retrieval, it is best to check the tag filters you will be using to make sure you will have all the tags you need. The application will start reading the data faster if you provide several small queries instead of a big one (e.g. *).

This command line will make 5 queries to get PI Tags. Queries need to be built on the [PIPointQuery syntax][1]. Each query needs to be separated by a space. Put your query in quotes if it contains spaces.

```bash
datareader.exe --server PIServer01 --testTagSearch sinus* cdt* "tag:<>sin* DataType:Float" "PointSource:=#" "PointSource:=C"
```

If you would like to see what are the tags that are included in your query, add the `--printTags` command:

```bash
datareader.exe --server PIServer01 --testTagSearch "tag:=Unit1* AND Location1:=1 AND PointSource:=OPC" --printTags
```

## Raw Data Examples

### Basic Raw Data Read (No Output)
Read values for the last 30 days without writing to file (for testing):

```bash
DataReader.exe -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 15 --estimatedTagsCount 10000
```

### Raw Data with File Output
Read values for the last 30 days and output the result into CSV files:

```bash
DataReader.exe -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 2 --estimatedTagsCount 6207 --enableWrite --outFileName "C:\temp\data"
```

### Multiple Tag Queries
Use multiple queries to optimize tag loading:

```bash
DataReader.exe -s PIServer01 -t sinus* cdt* "PointSource:=OPC" --st *-7d --et * --enableWrite --outFileName "C:\temp\rawdata"
```

### Custom Time Range
Read data for a specific date range:

```bash
DataReader.exe -s PIServer01 -t "tag:=Reactor*" --st "2024-01-01" --et "2024-01-31" --enableWrite --outFileName "C:\temp\january_data"
```

### Filter Output Data
Remove duplicates and filter out digital states:

```bash
DataReader.exe -s PIServer01 -t * --st *-7d --et * --removeDuplicates --filterDigitalStates --enableWrite --outFileName "C:\temp\filtered_data"
```

## Summary/Aggregate Data Examples

### Daily Averages, Min, and Max
Extract daily summary statistics for the last 30 days:

```bash
DataReader.exe -s PIServer01 -t "tag:=Reactor*" --st *-30d --et * --enableSummary --summaryTypes "Average,Minimum,Maximum" --summaryInterval "1d" --enableWrite --outFileName "C:\temp\daily_summary"
```

### Hourly Totals for Flow Tags
Calculate hourly totals with time-weighted calculation:

```bash
DataReader.exe -s PIServer01 -t "tag:=Flow*" --st *-7d --et * --enableSummary --summaryTypes "Total" --summaryInterval "1h" --calculationBasis "TimeWeighted" --enableWrite --outFileName "C:\temp\hourly_totals"
```

### 15-Minute Averages
Extract 15-minute average values:

```bash
DataReader.exe -s PIServer01 -t sinus* --st *-1d --et * --enableSummary --summaryTypes "Average" --summaryInterval "15m" --enableWrite --outFileName "C:\temp\15min_avg"
```

### All Available Summaries
Calculate all available summary types for a day:

```bash
DataReader.exe -s PIServer01 -t "PointSource:=OPC" --st *-1d --et * --enableSummary --summaryTypes "All" --summaryInterval "1d" --calculationBasis "TimeWeighted" --enableWrite --outFileName "C:\temp\complete_summary"
```

### Standard Deviation and Range
Calculate daily standard deviation and range:

```bash
DataReader.exe -s PIServer01 -t "tag:=Temperature*" --st *-30d --et * --enableSummary --summaryTypes "StdDev,Range,Average" --summaryInterval "1d" --enableWrite --outFileName "C:\temp\stats"
```

### Event-Weighted Summaries
Use event-weighted calculation for count-based data:

```bash
DataReader.exe -s PIServer01 -t "tag:=Count*" --st *-7d --et * --enableSummary --summaryTypes "Average,Count" --summaryInterval "1d" --calculationBasis "EventWeighted" --enableWrite --outFileName "C:\temp\event_weighted"
```

### Hourly Summaries with Custom Timestamps
Extract hourly summaries with timestamps at the end of each interval:

```bash
DataReader.exe -s PIServer01 -t * --st *-1d --et * --enableSummary --summaryTypes "Average,Minimum,Maximum" --summaryInterval "1h" --timestampCalculation "MostRecentTime" --enableWrite --outFileName "C:\temp\hourly_end_time"
```

### Monthly Summaries
Calculate monthly totals for long-term analysis:

```bash
DataReader.exe -s PIServer01 -t "tag:=Production*" --st *-365d --et * --enableSummary --summaryTypes "Total,Average,Maximum" --summaryInterval "30d" --enableWrite --outFileName "C:\temp\monthly_production"
```

## Connecting to PI Collective

### Connect to Specific Collective Member
Connect to a specific member of a PI Data Archive collective:

```bash
DataReader.exe -s MyCollective MemberServer01 -t * --st *-1d --et * --enableWrite --outFileName "C:\temp\collective_data"
```

# Usage

## General Options

```
-s, --server               Required. PI Data Archive Server name to connect to.
                          You can connect to a specific collective member by 
                          passing 2 strings: [collectiveName] [memberName]

-t, --tagQueries           Queries to load the tags. The more you add, the 
                          better and the sooner the app will start reading data.
                          This option accepts many queries separated by a space.
                          e.g. sinus* SSN_NP60* "tag:<>sin* DataType:Float"

--testTagSearch           Makes a search with all passed filters and prints
                          the results to the screen.
                          e.g. sinus* SSN_NP60* "tag:<>sin* DataType:Float"

--printTags               Print all tag names when doing the testTagSearch

--st                      (Default: *-1d) Start Time to query data

--et                      (Default: *) End Time to query data
```

## Raw Data Options

```
--estimatedEventsPerDay   (Default: 4) Provides an estimate of the number of
                          events per tag per day, to help optimize reading speed

--estimatedTagsCount      (Default: 10000) Estimate of the total number of
                          tags that will be read, to help optimize the application

--eventsPerRead           (Default: 10000) Defines how many events should be
                          read per data call

--removeDuplicates        Output values will not contain duplicated values

--filterDigitalStates     Output values will not contain digital states
```

## Summary/Aggregate Options

```
--enableSummary           (Default: False) Extract aggregate/summary data 
                          (average, min, max, etc.) instead of raw data

--summaryTypes            (Default: "Average,Minimum,Maximum") Summary types to 
                          calculate. Options: Total, Average, Minimum, Maximum, 
                          Range, StdDev, PopulationStdDev, Count, PercentGood, 
                          TotalWithUOM, All, AllForNonNumeric
                          Use comma-separated values for multiple types

--summaryInterval         (Default: "1d") Interval duration for each summary 
                          calculation. Examples: '1d' (1 day), '1h' (1 hour), 
                          '30m' (30 minutes), '15s' (15 seconds)
                          Positive durations start from earliest time, 
                          negative from latest

--calculationBasis        (Default: "TimeWeighted") Method for evaluating data.
                          Options: TimeWeighted, EventWeighted, 
                          TimeWeightedContinuous, TimeWeightedDiscrete,
                          EventWeightedExcludeMostRecentEvent,
                          EventWeightedExcludeEarliestEvent,
                          EventWeightedIncludeBothEnds

--timestampCalculation    (Default: "Auto") Timestamp to return for each summary.
                          Options: Auto, EarliestTime, MostRecentTime
```

## Output Options

```
--enableWrite             (Default: False) Outputs the data into text files.
                          If not specified, data is read but not output

--writersCount            (Default: 4) Defines the number of file writers
                          that will run simultaneously

--outFileName             File name to output data. Works with the enableWrite
                          option. A datetime and .csv extension will be appended
                          to the name. Example: c:\temp\data

--eventsPerFile           (Default: 500000) Number of events to write per file

--help                    Display this help screen
```

# Performance Notes

## Raw Data Performance
On a server with about 6000 tags, the following command gave very good read results:

```bash
DataReader.exe -s PIServer01 -t * --st T-30d --et T --estimatedEventsPerDay 4 --estimatedTagsCount 6207 --eventsPerRead 150000
```

## Summary Data Performance
For summary calculations, performance is typically better than raw data extraction because:
- Less data is transferred over the network
- Calculations are performed server-side in the PI Data Archive
- Parallel processing is used for multiple tags

Tips for optimal summary performance:
- Use appropriate `summaryInterval` values (larger intervals = less data)
- Request only the summary types you need
- Use `TimeWeighted` calculation basis for most continuous data
- Use `EventWeighted` for discrete or count-based data

# Output Format

## Raw Data Output
CSV files with format: `Timestamp,Value,TagName`

Example:
```
2024-01-15 10:00:00,23.45,Reactor1_Temperature
2024-01-15 10:05:00,23.67,Reactor1_Temperature
```

## Summary Data Output
CSV files with metadata header followed by summary values:

Example:
```
# Summary Data Export
# SummaryTypes: Average, Minimum, Maximum
# Interval: 1d
# CalculationBasis: TimeWeighted
# Timestamp,Value,TagName
2024-01-15 00:00:00,23.45,Reactor1_Temperature
2024-01-16 00:00:00,24.12,Reactor1_Temperature
```

# Additional Resources

For more information on PIPointQuery syntax, see:
[PI AF SDK Documentation - PIPointQuery][1]

[1]:https://techsupport.osisoft.com/Documentation/PI-AF-SDK/html/b8fbb6da-7a4b-4570-a09d-7f2b85ed204d.htm

