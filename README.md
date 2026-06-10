# DataReader

This command line application, written in C#, reads data from the OSIsoft PI Data Archive.  
It was created to extract very large amounts of data in an efficient manner, using knowledge of the internals of the PI Data Archive and the Windows system to maximize throughput.

The application supports two modes accessed via verbs:
- **`raw`** (default): Extract raw archived values from PI tags
- **`summary`**: Extract calculated summaries (average, min, max, totals, etc.) over specified intervals
- **`test`**: Test tag search queries to verify which tags will be found

# Documentation

- **[User Guide](README.md)** - Command-line usage, examples, and options (this document)
- **[Architecture Guide](ARCHITECTURE.md)** - Internal architecture, threading model, and developer documentation

# Recent Improvements

The project was modernized and its extraction performance was significantly improved:

- **Migrated to .NET 10** and the [`Aveva.AFSDK`](https://www.nuget.org/packages/Aveva.AFSDK) NuGet package, so no local PI AF Client install is required to build or run (see Prerequisites).
- **Logging moved to NLog** (configured via `NLog.config`), replacing log4net.
- **File output now respects `--eventsPerFile`.** The summary path used to create one file per internal batch (thousands of tiny files on long extractions); rows now accumulate into stable, writer-scoped files that roll only when `--eventsPerFile` is reached.
- **Coordinated summary batching** so each bulk call to the PI Data Archive targets about 10,000 events (one value plus timestamp per tag), the sweet spot for the server bulk API. See [Performance and Tuning](#performance-and-tuning).
- **Correctness fixes**: daylight-saving spring-forward gaps are handled when generating interval boundaries, and bulk-call concurrency is capped (default 4) to avoid saturating the PI Data Archive.

Measured on a 3-year / 10-tag / 10-minute summary extraction: about 2x faster end-to-end, with output files reduced from thousands to a handful and no change to the data.

# Build

The application targets **.NET 10** and references the AVEVA AF SDK through the [`Aveva.AFSDK`](https://www.nuget.org/packages/Aveva.AFSDK) NuGet package, so no local PI AF Client installation is required to build. Build with Visual Studio 2022+ or from the command line:

```
dotnet build data-reader.sln -c Release
```

Once compiled, it creates a **Build** folder in the solution folder. You can take this folder and place it on the system you would like to make the test on.

# Prerequisites on host system

* [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64)
* [Microsoft Visual C++ Redistributable (x64)](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist) — required by the AF SDK
* Windows (the AF SDK is Windows-only)

> A local PI AF Client install is **not** required on the host: the AF SDK and its plug-ins are bundled with the application via the `Aveva.AFSDK` NuGet package.

# Getting Started

## Test Tag Queries First

Before starting your data retrieval, test your tag filters to ensure you'll get all the tags you need. The application will start reading data faster if you provide several small queries instead of one large query (e.g. *).

### Test tag queries and see count
```bash
DataReader.exe test -s PIServer01 -q sinus* cdt* "tag:<>sin* DataType:Float"
```

### Test queries and print all matching tag names
```bash
DataReader.exe test -s PIServer01 -q "tag:=Unit1* AND Location1:=1 AND PointSource:=OPC" --printTags
```

## Raw Data Extraction Examples

The `raw` verb (default) extracts archived data points exactly as stored in the PI Data Archive.

### Basic Raw Data Read (No Output)
Read values for the last 30 days without writing to file (for performance testing):

```bash
DataReader.exe raw -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 15 --estimatedTagsCount 10000
```

Or omit the `raw` verb since it's the default:

```bash
DataReader.exe -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 15 --estimatedTagsCount 10000
```

### Raw Data with File Output
Read values for the last 30 days and output to CSV files:

```bash
DataReader.exe raw -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 2 --estimatedTagsCount 6207 --enableWrite --outFileName "C:\temp\data"
```

### Using Tag File for Many Tags
When you have many tags (to avoid command line length limits):

**Create `tags.txt`:**
```
S4100_PLC_001!S4100_IIC0319_E_PV_HMI_R
S4100_PLC_001!S4100_CBE003_M1_C_SP_HMI_R
# Comments are supported with #
4100-CJA-001-M1.courant_moyen
```

**Run the command:**
```bash
DataReader.exe raw -s PIServer01 --tagFile tags.txt --st *-30d --et * --enableWrite --outFileName "C:\temp\rawdata"
```

### Multiple Tag Queries
Use multiple queries to optimize tag loading:

```bash
DataReader.exe raw -s PIServer01 -t sinus* cdt* "PointSource:=OPC" --st *-7d --et * --enableWrite --outFileName "C:\temp\rawdata"
```

### Custom Time Range
Read data for a specific date range:

```bash
DataReader.exe raw -s PIServer01 -t "tag:=Reactor*" --st "2024-01-01" --et "2024-01-31" --enableWrite --outFileName "C:\temp\january_data"
```

### Filter Output Data
Remove duplicates and filter out digital states:

```bash
DataReader.exe raw -s PIServer01 -t * --st *-7d --et * --removeDuplicates --filterDigitalStates --enableWrite --outFileName "C:\temp\filtered_data"
```

## Summary/Aggregate Data Extraction Examples

The `summary` verb extracts calculated aggregates over time intervals instead of raw data points.

**Important Note on Time Handling for Summaries**: Time parameters (`--st` and `--et`) are treated as **local time** when querying the PI Data Archive for summaries. This is critical to ensure that daily/hourly intervals align correctly with calendar days and handle daylight saving time (DST) transitions properly:
- A "daily" summary interval aligns with local midnight-to-midnight, not UTC midnight
- During DST transitions, days can be 23, 24, or 25 hours, and the local time approach handles this correctly
- Use local time expressions: `"2024-01-01"`, `"*-30d"`, `"*"`
- **Output timestamps are in LOCAL TIME** (format: `YYYY-MM-DDTHH:MM:SS`) to match the query time zone
- Log files show both local and UTC times for debugging purposes

### Daily Averages, Min, and Max
Extract daily summary statistics for the last 30 days:

```bash
DataReader.exe summary -s PIServer01 -t "tag:=Reactor*" --st *-30d --et * --summaryTypes "Average,Minimum,Maximum" --summaryInterval "1d" --enableWrite --outFileName "C:\temp\daily_summary"
```

### Hourly Totals for Flow Tags
Calculate hourly totals with time-weighted calculation:

```bash
DataReader.exe summary -s PIServer01 -t "tag:=Flow*" --st *-7d --et * --summaryTypes "Total" --summaryInterval "1h" --calculationBasis "TimeWeighted" --enableWrite --outFileName "C:\temp\hourly_totals"
```

### Using Tag File for Summary Data
When you have many tags and want summary data:

```bash
DataReader.exe summary -s PIServer01 --tagFile tags.txt --st "2024-01-01" --et "2024-01-31" --summaryTypes "All" --summaryInterval "1d" --timestampCalculation "MostRecentTime" --enableWrite --outFileName "C:\temp\complete_summary"
```

### 15-Minute Averages
Extract 15-minute average values:

```bash
DataReader.exe summary -s PIServer01 -t sinus* --st *-1d --et * --summaryTypes "Average" --summaryInterval "15m" --enableWrite --outFileName "C:\temp\15min_avg"
```

### All Available Summaries
Calculate all available summary types for a day:

```bash
DataReader.exe summary -s PIServer01 -t "PointSource:=OPC" --st *-1d --et * --summaryTypes "All" --summaryInterval "1d" --calculationBasis "TimeWeighted" --enableWrite --outFileName "C:\temp\complete_summary"
```

### Standard Deviation and Range
Calculate daily standard deviation and range:

```bash
DataReader.exe summary -s PIServer01 -t "tag:=Temperature*" --st *-30d --et * --summaryTypes "StdDev,Range,Average" --summaryInterval "1d" --enableWrite --outFileName "C:\temp\stats"
```

### Event-Weighted Summaries
Use event-weighted calculation for count-based data:

```bash
DataReader.exe summary -s PIServer01 -t "tag:=Count*" --st *-7d --et * --summaryTypes "Average,Count" --summaryInterval "1d" --calculationBasis "EventWeighted" --enableWrite --outFileName "C:\temp\event_weighted"
```

### Hourly Summaries with Custom Timestamps
Extract hourly summaries with timestamps at the end of each interval:

```bash
DataReader.exe summary -s PIServer01 -t * --st *-1d --et * --summaryTypes "Average,Minimum,Maximum" --summaryInterval "1h" --timestampCalculation "MostRecentTime" --enableWrite --outFileName "C:\temp\hourly_end_time"
```

### Monthly Summaries
Calculate monthly totals for long-term analysis:

```bash
DataReader.exe summary -s PIServer01 -t "tag:=Production*" --st *-365d --et * --summaryTypes "Total,Average,Maximum" --summaryInterval "30d" --enableWrite --outFileName "C:\temp\monthly_production"
```

## Connecting to PI Collective

### Connect to Specific Collective Member
Connect to a specific member of a PI Data Archive collective:

```bash
DataReader.exe raw -s MyCollective MemberServer01 -t * --st *-1d --et * --enableWrite --outFileName "C:\temp\collective_data"
```

# Usage

## Command Structure

```
DataReader.exe <verb> [options]
```

Available verbs:
- `raw` (default) - Extract raw archived data
- `summary` - Extract aggregate/summary data
- `test` - Test tag search queries

Get help for a specific verb:
```bash
DataReader.exe <verb> --help
```

## Common Options (Available for `raw` and `summary` verbs)

```
-s, --server               Required. PI Data Archive Server name to connect to.
                          You can connect to a specific collective member by 
                          passing 2 strings: [collectiveName] [memberName]

-t, --tagQueries           Queries to load the tags. The more you add, the 
                          better and the sooner the app will start reading data.
                          This option accepts many queries separated by a space.
                          e.g. sinus* SSN_NP60* "tag:<>sin* DataType:Float"

--tagFile                 Path to a text file containing tag names or queries,
                          one per line. Use this when you have many tags to 
                          avoid command line length limits (Windows limit: 
                          8,191 characters). Lines starting with # are treated 
                          as comments and ignored. Can be combined with 
                          --tagQueries option.

--st                      (Default: *-1d) Start Time to query data.
                          **IMPORTANT for summary queries**: Time values are
                          treated as LOCAL time to ensure daily/hourly intervals
                          align with calendar days and handle DST correctly.
                          Use local time expressions like "2024-01-01" or "*-30d".
                          For raw data queries, times are treated as specified.

--et                      (Default: *) End Time to query data.
                          **IMPORTANT for summary queries**: Time values are
                          treated as LOCAL time to ensure daily/hourly intervals
                          align with calendar days and handle DST correctly.
                          Use local time expressions like "2024-01-31" or "*".
                          For raw data queries, times are treated as specified.

--estimatedEventsPerDay   (Default: 4) Estimated number of events per tag per 
                          day, to help optimize reading speed

--estimatedTagsCount      (Default: 10000) Estimated total number of tags to 
                          read, to help optimize the application

--enableWrite             (Default: false) Outputs the data into CSV files.
                          If not specified, data is read but not output

--outFileName             File name to output data. A datetime and .csv 
                          extension will be appended. 
                          Example: C:\temp\data

--writersCount            (Default: 4) Number of file writers to run 
                          simultaneously

--eventsPerFile           (Default: 500000) Number of events to write per file

--help                    Display help for the specific verb

--version                 Display version information
```

## Raw Data Specific Options

```
--eventsPerRead           (Default: 10000) Number of events to read per data call

--removeDuplicates        Output values will not contain duplicated values

--filterDigitalStates     Output values will not contain digital states
```

## Summary Data Specific Options

```
--summaryTypes            (Default: Average,Minimum,Maximum) Summary types to 
                          calculate. Options: Total, Average, Minimum, Maximum, 
                          Range, StdDev, PopulationStdDev, Count, PercentGood, 
                          TotalWithUOM, All, AllForNonNumeric
                          Use comma-separated values for multiple types

--summaryInterval         (Default: 1d) Interval duration for each summary 
                          calculation. Examples: '1d' (1 day), '1h' (1 hour), 
                          '30m' (30 minutes), '15s' (15 seconds)
                          Positive durations start from earliest time, 
                          negative from latest

--calculationBasis        (Default: TimeWeighted) Method for evaluating data.
                          Options: TimeWeighted, EventWeighted, 
                          TimeWeightedContinuous, TimeWeightedDiscrete,
                          EventWeightedExcludeMostRecentEvent,
                          EventWeightedExcludeEarliestEvent,
                          EventWeightedIncludeBothEnds

--timestampCalculation    (Default: Auto) Timestamp to return for each summary.
                          Options: Auto, EarliestTime, MostRecentTime

--tagsChunkSize           (Default: 50) Number of tags sent per bulk call to the
                          PI Data Archive. Together with the number of summary
                          types and the interval count, this targets ~10,000
                          events per call. When extracting few tags, lower it
                          toward your actual tag count for best throughput
                          (see Performance and Tuning).

--intervalsPerBatch       (Default: 0 = auto) Number of summary intervals per
                          bulk call. 0 lets the app compute it to hit the
                          ~10,000-events-per-call target from tagsChunkSize and
                          summary types. Set a value only to override auto-sizing.
```

## Test Verb Options

```
-s, --server              Required. PI Data Archive Server name to connect to

-q, --queries             Required. Tag queries to test.
                          e.g. sinus* "tag:<>sin* DataType:Float"

--printTags               Print all tag names found by the queries
```

# Performance and Tuning

The application pulls large volumes of data efficiently by making **bulk calls** to the PI Data Archive, each fetching many tags at once. An *event* is a single value plus timestamp for one tag. The goal is for each bulk call to return roughly **10,000 events**: large enough to amortize round-trips, small enough to keep memory and the server comfortable.

For **summary** extractions, the per-call event count is:

```
events per call = tagsChunkSize x summaryTypes x intervalsPerBatch
```

The app sizes `intervalsPerBatch` automatically so this lands near 10,000, and it sweeps the time range period by period (all tags for a period, then the next) to make good use of the server cache.

**Practical tips:**

- **Few tags?** The default `--tagsChunkSize 50` assumes at least ~50 tags. When extracting fewer (say 10), set `--tagsChunkSize 10` so each call still reaches ~10,000 events. In a 3-year / 10-tag / 10-minute test this roughly halved the run time and cut network round-trips about 5x.
- **Many tags?** The default of 50 already works well; leave it.
- **Concurrency** is capped at 4 by default because the PI Data Archive serves bulk calls from a small thread pool. Raising it risks saturating the server.
- Use **`--eventsPerFile`** (default 500,000) to control how many rows go in each CSV file; files roll automatically when the limit is reached.

# Output Format

## Timestamp Handling

**All timestamps in output files are in LOCAL TIME** using the format `YYYY-MM-DDTHH:MM:SS±HH:MM` (ISO 8601 with timezone offset) to match the timezone of the query. This is particularly important for summary data where daily/hourly intervals must align with calendar days in the local timezone.

**Log files show both Local and UTC times** for easier debugging and correlation with UTC systems if needed.

## Raw Data Output
CSV files with format: `Timestamp,Value,TagName`

Example:
```
2024-01-15T10:00:00-05:00,23.45,Reactor1_Temperature
2024-01-15T10:05:00-05:00,23.67,Reactor1_Temperature
```

## Summary Data Output
CSV files with metadata header followed by summary values. Summary data includes an additional `AggregateType` column to identify which summary calculation each value represents:

Example:
```
# Summary Data Export
# Generated (Local Time): 2024-01-20T14:30:00-05:00
# SummaryTypes: Average, Minimum, Maximum
# Interval: 1d
# CalculationBasis: TimeWeighted
# Timestamp,Value,TagName,AggregateType
2024-01-15T00:00:00-05:00,23.45,Reactor1_Temperature,Average
2024-01-15T00:00:00-05:00,20.12,Reactor1_Temperature,Minimum
2024-01-15T00:00:00-05:00,26.78,Reactor1_Temperature,Maximum
2024-01-16T00:00:00-05:00,24.12,Reactor1_Temperature,Average
2024-01-16T00:00:00-05:00,21.34,Reactor1_Temperature,Minimum
2024-01-16T00:00:00-05:00,27.45,Reactor1_Temperature,Maximum
```

**Note:** Output files are named per writer and roll when `--eventsPerFile` is reached:
- Raw: `data_w1.csv`, then `data_w1_p1.csv`, `data_w1_p2.csv`, ...
- Summary: `data_summary_w1.csv`, then `data_summary_w1_p1.csv`, ...

Sort by the `Timestamp` column (or use `--writersCount 1`) if you need a single chronologically ordered set.



