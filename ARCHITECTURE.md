# Architecture

This document provides an in-depth look at the architecture and internal workings of the DataReader application, designed to help developers understand the system and contribute effectively.

## Table of Contents
- [Overview](#overview)
- [System Architecture](#system-architecture)
- [Component Details](#component-details)
- [Threading Model](#threading-model)
- [Data Flow](#data-flow)
- [Performance Optimizations](#performance-optimizations)
- [Extending the Application](#extending-the-application)

## Overview

DataReader is a high-performance command-line application built on .NET 10 that extracts data from OSIsoft PI Data Archive. The architecture follows a **producer-consumer pipeline pattern** with multiple concurrent workers, optimized for throughput when dealing with large datasets (millions of data points, thousands of tags).

### Key Design Principles

1. **Parallelism**: Leverages multi-threading to maximize PI Data Archive's 16 concurrent bulk call threads
2. **Batching**: Intelligently batches requests to minimize network overhead
3. **Pipelining**: Components work concurrently in a pipeline to keep the CPU and network busy
4. **Memory Efficiency**: Uses blocking collections and streaming to avoid loading entire datasets into memory

## System Architecture

### High-Level Component Diagram

```
???????????????????????????????????????????????????????????????????????
?                         DataReader Application                       ?
???????????????????????????????????????????????????????????????????????
?                                                                       ?
?  ????????????????      ????????????????      ????????????????      ?
?  ? TagsLoader   ???????? Orchestrator ????????  DataReader  ?      ?
?  ?              ?      ?              ?      ?  (Raw/Summary)?      ?
?  ?  Searches    ?      ?  Time Slicer ?      ?              ?      ?
?  ?  PI Tags     ?      ?              ?      ?  Fetches Data?      ?
?  ????????????????      ????????????????      ????????????????      ?
?         ?                     ?                       ?              ?
?         ?                     ?                       ?              ?
?         ?                     ?                       ?              ?
?  ????????????????????????????????????????????????????????????      ?
?  ?           Blocking Collections (Thread-Safe Queues)       ?      ?
?  ?  • IncomingPiPoints  • QueriesQueue  • DataQueue         ?      ?
?  ????????????????????????????????????????????????????????????      ?
?                                               ?                      ?
?                                               ?                      ?
?                                      ????????????????                ?
?                                      ?  DataWriter  ?                ?
?                                      ?              ?                ?
?                                      ?  CSV Output  ?                ?
?                                      ????????????????                ?
?                                                                       ?
?  ??????????????????????????????????????????????????????????????    ?
?  ?                     Statistics Worker                       ?    ?
?  ?         (Monitors throughput and queue depths)              ?    ?
?  ??????????????????????????????????????????????????????????????    ?
?                                                                       ?
???????????????????????????????????????????????????????????????????????
```

### Component Responsibilities

| Component | Responsibility | Output |
|-----------|---------------|--------|
| **TagsLoader** | Tag discovery and grouping | Tag groups (default: 50k tags) |
| **Orchestrator** | Time-based query planning | DataQuery objects with time slices |
| **DataReader** | Parallel data fetching from PI | AFValues (data points) |
| **DataWriter** | File output management | CSV files |
| **Statistics** | Performance monitoring | Console metrics |

## Component Details

### 1. TagsLoader

**Location**: `DataReader.Core\Workers\TagsLoader.cs`

**Purpose**: Discovers PI tags based on user queries and groups them for processing.

**Key Features**:
- Supports PI tag query syntax (wildcards, filters, attributes)
- Reads from command line or text files
- Groups tags into configurable chunks (default: 50,000)
- Sends tag groups to Orchestrator via `BlockingCollection<DataQuery>`

**Configuration**:
```csharp
// Controlled by DataReaderSettings
int TagGroupSize = 50000;  // Tags per group
```

### 2. Orchestrator

**Location**: `DataReader.Core\Workers\Orchestrator.cs`

**Purpose**: Splits the overall time range into manageable intervals and creates queries for each tag group.

**Key Responsibilities**:
1. Receives tag groups from TagsLoader
2. Calculates time intervals based on `TimeIntervalPerDataRequest`
3. Creates multiple `DataQuery` objects (one per time interval per tag group)
4. Queues queries for DataReader

**Algorithm**:
```csharp
// Pseudo-code
foreach (tagGroup in IncomingPiPoints)
{
    for (i = 0; i < dateIntervals.Count - 1; i++)
    {
        query = new DataQuery()
        {
            StartTime = dateIntervals[i],
            EndTime = dateIntervals[i+1] - 1 second,
            PiPoints = tagGroup,
            ChunkId = i
        };
        dataReader.Queue.Add(query);
    }
}
```

**Example Execution**:
```
Input:
  - Tags: 1000 tags
  - Time Range: Jan 1 - Jan 30 (30 days)
  - TimeIntervalPerDataRequest: 5 days

Output: 6 queries
  - Query 1: 1000 tags, Jan 1-5
  - Query 2: 1000 tags, Jan 6-10
  - Query 3: 1000 tags, Jan 11-15
  - Query 4: 1000 tags, Jan 16-20
  - Query 5: 1000 tags, Jan 21-25
  - Query 6: 1000 tags, Jan 26-30
```

### 3. DataReader (Raw vs Summary)

#### DataReaderBulk (Raw Data)

**Location**: `DataReader.Core\Workers\DataReaderBulk.cs`

**Purpose**: Fetches raw archived values using PI SDK bulk APIs.

**Key Features**:
- Uses `PIPointList.RecordedValues()` for bulk retrieval
- Chunks tags into smaller groups (default: 10,000) for parallel processing
- Supports pagination via `PIPagingConfiguration`
- Processes up to 16 chunks concurrently (MaxDegreeOfParallelism)

**API Call**:
```csharp
IEnumerable<AFValues> bulkData = pointList.RecordedValues(
    timeRange,
    AFBoundaryType.Inside,
    filterExpression: String.Empty,
    includeFilteredValues: false,
    pagingConfiguration);
```

#### DataReaderSummary (Aggregate Data)

**Location**: `DataReader.Core\Workers\DataReaderSummary.cs`

**Purpose**: Fetches calculated summaries (averages, min, max, totals, etc.) using optimized batching.

**Key Features**:
- Uses `PIPointList.Summaries()` for bulk summary retrieval
- Intelligent batch sizing based on summary types and tag count
- Default chunk size: 20 tags (configurable via `--tagsPerChunk`)
- Dynamically splits time ranges into optimal batches

**Batching Algorithm**:
```csharp
// Calculate intervals per batch to target ~10,000 events
int intervalsPerBatch = 10000 / (summaryTypesCount * tagsInChunk);

// Example: 5 summary types × 20 tags = 100 tags of data per interval
// 10000 / 100 = 100 intervals per batch
```

**API Call**:
```csharp
IEnumerable<IDictionary<AFSummaryTypes, AFValues>> bulkResults = 
    pointList.Summaries(
        timeRange,
        summaryInterval,
        summaryTypes,
        calculationBasis,
        timestampCalculation,
        pagingConfiguration);
```

**Time Range Splitting**:
```
Full Range: Jan 1 - Dec 31 (365 days)
Summary Interval: 1 day
Intervals per batch: 100

Result:
  - Batch 1: Jan 1 - Apr 10 (100 days)
  - Batch 2: Apr 11 - Jul 19 (100 days)
  - Batch 3: Jul 20 - Oct 27 (100 days)
  - Batch 4: Oct 28 - Dec 31 (65 days)
```

### 4. DataWriter

**Location**: `DataReader.Core\Workers\DataWriter.cs`

**Purpose**: Manages concurrent file writing to CSV.

**Key Features**:
- Multiple file writers (default: 4) to avoid I/O bottlenecks
- Automatic file splitting based on event count (default: 500k events/file)
- Handles both raw and summary data formats
- Supports data filtering (duplicates, digital states)

**File Naming Convention**:
```
{baseFileName}_{startTimeUTC}_{chunkId}_{subChunkId}[_summary]_w{writerId}[_p{splitNum}].csv
```

**Examples**:
```
extract_2023-02-25_12_00_00_UTC_1_0_summary_w1.csv
extract_2023-02-26_12_00_00_UTC_2_0_summary_w2.csv
extract_2023-02-27_12_00_00_UTC_3_0_summary_w1_p2.csv  (split due to line limit)
```

**Filename Components**:
- `baseFileName`: User-specified output name
- `startTimeUTC`: UTC start time (ISO-readable format for sorting)
- `chunkId`: Time slice from Orchestrator
- `subChunkId`: Batch index within DataReaderSummary
- `_summary`: Suffix for summary/aggregate data (omitted for raw data)
- `w{writerId}`: Writer thread ID (1-4 by default)
- `_p{splitNum}`: Part number when file split due to line limit (optional)

**Benefits**:
- ? **Chronologically sortable**: Files sort by timestamp
- ? **Unique**: ChunkId + SubChunkId + WriterId ensures no collisions
- ? **Readable**: Timestamp immediately visible
- ? **Compact**: No redundant information

### 5. Statistics

**Location**: `DataReader.Core\Workers\Statistics.cs`

**Purpose**: Real-time performance monitoring and reporting.

**Metrics Tracked**:
- Events processed per second
- Queue depths (data waiting to be written)
- Total events processed
- Elapsed time per operation

## Threading Model

### Thread Allocation

```
Application Threads:
?? Main Thread (1)
?  ?? Manages application lifecycle
?
?? TagsLoader Thread (1)
?  ?? Searches and loads tags
?
?? Orchestrator Thread (1)
?  ?? Creates time-sliced queries
?
?? DataReader Threads (16 max)
?  ?? Parallel.ForEach with MaxDegreeOfParallelism = 16
?     ?? Thread 1: Processing chunk 0
?     ?? Thread 2: Processing chunk 1
?     ?? ...
?     ?? Thread 16: Processing chunk 15
?
?? DataWriter Threads (4 default)
?  ?? Concurrent file writers
?     ?? Writer 1
?     ?? Writer 2
?     ?? Writer 3
?     ?? Writer 4
?
?? Statistics Thread (1)
   ?? Monitors and reports metrics
```

### Why 16 Threads?

The PI Data Archive server has **16 threads dedicated to bulk API calls**. Using more than 16 client threads would not improve performance as the server would become the bottleneck.

```csharp
// DataReaderSettings.cs
private int _maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 16);
```

### Thread Synchronization

**Blocking Collections** provide thread-safe communication between components:

```csharp
// TagsLoader ? Orchestrator
BlockingCollection<DataQuery> IncomingPiPoints

// Orchestrator ? DataReader
BlockingCollection<DataQuery> QueriesQueue

// DataReader ? DataWriter
BlockingCollection<WriteInfo> DataQueue

// Statistics monitoring
BlockingCollection<StatisticsInfo> StatisticsQueue
```

**Benefits**:
- **Thread-safe**: No manual locking required
- **Backpressure**: Automatically throttles producers if consumers fall behind
- **Cancellation**: Supports cooperative cancellation tokens

## Data Flow

### Detailed Execution Flow (Summary Mode)

```
User Command:
  DataReader.exe summary -s PIServer01 -t * --st *-30d --et * 
                 --summaryInterval "1d" --tagsPerChunk 20

Step 1: Initialization
  ?? Parse command-line arguments
  ?? Create DataReaderSettings
  ?  ?? BulkParallelChunkSize = 20 (tagsPerChunk)
  ?  ?? Calculate TimeIntervalPerDataRequest dynamically
  ?? Connect to PI Data Archive
  ?? Start all worker threads

Step 2: Tag Discovery (TagsLoader)
  ?? Execute tag search query: "*"
  ?? Found: 1000 tags
  ?? Group into chunks: 1000 ÷ 50000 = 1 group
  ?? Send to Orchestrator: [1000 tags]

Step 3: Time Slicing (Orchestrator)
  ?? Receive tag group: 1000 tags
  ?? Time range: Jan 1 - Jan 30 (30 days)
  ?? TimeIntervalPerDataRequest: 10 days (calculated)
  ?? Create intervals: [Jan 1-10, Jan 11-20, Jan 21-30]
  ?? Send to DataReader:
      ?? Query 1: 1000 tags, Jan 1-10
      ?? Query 2: 1000 tags, Jan 11-20
      ?? Query 3: 1000 tags, Jan 21-30

Step 4: Data Fetching (DataReaderSummary)
  ?? Receive Query 1: 1000 tags, Jan 1-10
  ?? Chunk tags: 1000 ÷ 20 = 50 chunks
  ?? Process chunks in parallel (16 threads):
  ?  ?
  ?  ?? Thread 1: Chunk 0 (tags 1-20)
  ?  ?  ?? Calculate: 10000 ÷ (3 types × 20 tags) = 167 intervals
  ?  ?  ?? Time: 10 days = 10 intervals (1d each)
  ?  ?  ?? Batches: 10 ÷ 167 = 1 batch (all 10 days)
  ?  ?  ?? API Call: PIPointList.Summaries(Jan 1-10, "1d", ...)
  ?  ?     ?? Returns: 20 tags × 3 types × 10 days = 600 values
  ?  ?
  ?  ?? Thread 2: Chunk 1 (tags 21-40)
  ?  ?  ?? [Same process as Thread 1]
  ?  ?
  ?  ?? ...
  ?  ?
  ?  ?? Thread 16: Chunk 15 (tags 301-320)
  ?     ?? [Same process as Thread 1]
  ?
  ?? Total API calls: 50 chunks = 50 bulk API calls
     (vs. 1000 individual calls in old implementation)

Step 5: Data Writing (DataWriter)
  ?? Receive WriteInfo from DataReader
  ?? Find available FileWriter
  ?? Write CSV data:
  ?  ?? Header: # Summary Data Export
  ?  ?? Metadata: SummaryTypes, Interval, etc.
  ?  ?? Values: Timestamp, Value, TagName, AggregateType
  ?? Close file when event limit reached

Step 6: Statistics Reporting
  ?? Monitor queue depths
  ?? Calculate throughput: events/second
  ?? Display progress to console
```

### Performance Characteristics

**Summary Mode Efficiency**:

| Scenario | Old (Individual Calls) | New (Bulk + Batching) | Improvement |
|----------|------------------------|----------------------|-------------|
| 1000 tags, 30 days, 5 types | 1,000 API calls | 50 API calls | **20x fewer calls** |
| 10,000 tags, 365 days, 3 types | 10,000 API calls | 500 API calls | **20x fewer calls** |
| Network latency: 10ms/call | 10 seconds | 0.5 seconds | **20x faster** |

**Why It's Faster**:
1. **Bulk API**: One call fetches data for multiple tags
2. **Batching**: One call fetches multiple time intervals
3. **Parallelism**: 16 concurrent threads maximize server utilization
4. **Pipelining**: All components work concurrently

## Performance Optimizations

### 1. Dynamic Interval Calculation

The system calculates `TimeIntervalPerDataRequest` based on the summary interval to optimize batching:

```csharp
// Program.cs - RunSummaryDataExtraction
var summaryInterval = AFTimeSpan.Parse(options.SummaryInterval);
int summaryTypesCount = CountSummaryTypes(options.SummaryTypes);

double intervalsPerRequest = 10000.0 / (options.TagsCount * summaryTypesCount);
double requestSeconds = intervalTimeSpan.TotalSeconds * intervalsPerRequest;
readerSettings.TimeIntervalPerDataRequest = TimeSpan.FromSeconds(requestSeconds);
```

**Example**:
```
Input: 
  - Tags: 10,000
  - Summary Types: 5 (Average, Min, Max, StdDev, Count)
  - Summary Interval: 1 hour

Calculation:
  - intervalsPerRequest = 10,000 / (10,000 × 5) = 0.2
  - Round up to 1 interval minimum
  - TimeIntervalPerDataRequest = 1 hour

Result: Orchestrator creates hourly queries
```

### 2. Adaptive Chunk Sizing

For summary operations, smaller chunks (default: 20 tags) allow for larger time batches:

```
tagsPerChunk = 20
summaryTypes = 5

intervalsPerBatch = 10,000 / (5 × 20) = 100 intervals

If summaryInterval = "1d":
  - One API call fetches 100 days of data for 20 tags
```

### 3. Memory Management

- **Streaming**: Data flows through the pipeline without loading entire datasets
- **Blocking Collections**: Automatic backpressure prevents memory exhaustion
- **Chunking**: Large tag sets split into manageable pieces

### 4. Network Optimization

- **Bulk APIs**: Minimize request overhead
- **Parallelism**: Keep network saturated
- **Batching**: Reduce round-trips

## Extending the Application

### Adding a New Data Reader

To add support for a new data extraction mode:

1. **Implement `IDataReader` interface**:
```csharp
public interface IDataReader
{
    BlockingCollection<DataQuery> GetQueriesQueue();
    Task Run();
}
```

2. **Create new reader class**:
```csharp
public class DataReaderInterpolated : TaskBase, IDataReader
{
    public readonly BlockingCollection<DataQuery> QueriesQueue = 
        new BlockingCollection<DataQuery>();
    
    public BlockingCollection<DataQuery> GetQueriesQueue()
    {
        return QueriesQueue;
    }
    
    protected override void DoTask(CancellationToken cancelToken)
    {
        foreach (var query in QueriesQueue.GetConsumingEnumerable(cancelToken))
        {
            // Implement interpolation logic
            var values = GetInterpolatedValues(query);
            
            // Send to DataWriter
            _dataWriter.DataQueue.Add(writeInfo, cancelToken);
        }
    }
}
```

3. **Add command-line verb** in `CommandLineOptions.cs`:
```csharp
[Verb("interpolated", HelpText = "Extract interpolated values at regular intervals")]
public class InterpolatedDataOptions : CommonOptions
{
    [Option("intervalStep", Default = "1h", HelpText = "Interval step for interpolation")]
    public string IntervalStep { get; set; }
}
```

4. **Wire up in `Program.cs`**:
```csharp
.WithParsed<InterpolatedDataOptions>(options => 
    RunInterpolatedDataExtraction(options, _logger))
```

### Adding a New Data Filter

To add custom filtering logic:

1. **Implement `IDataFilter` interface**:
```csharp
public interface IDataFilter
{
    bool IsFiltered(AFValue value);
}
```

2. **Create filter class**:
```csharp
public class OutlierFilter : IDataFilter
{
    private readonly double _threshold;
    
    public OutlierFilter(double threshold)
    {
        _threshold = threshold;
    }
    
    public bool IsFiltered(AFValue value)
    {
        if (value.Value is double dValue)
        {
            return Math.Abs(dValue) > _threshold;
        }
        return false;
    }
}
```

3. **Register in `FiltersFactory`**:
```csharp
if (options.FilterOutliers)
{
    filtersFactory.AddFilter(new OutlierFilter(options.OutlierThreshold));
}
```

### Adding New Output Formats

To support formats beyond CSV:

1. **Create new writer class** inheriting from or similar to `FileWriter`
2. **Add format option** to command-line options
3. **Modify DataWriter** to use appropriate writer based on format

## Configuration Reference

### DataReaderSettings

| Property | Default | Description |
|----------|---------|-------------|
| `MaxDegreeOfParallelism` | `Min(CPU cores, 16)` | Max concurrent threads |
| `TagGroupSize` | `50,000` | Tags loaded per batch |
| `BulkParallelChunkSize` | `10,000` (raw)<br>`20` (summary) | Tags processed per chunk |
| `BulkPageSize` | `1,000` | Paging size for bulk calls |
| `TimeIntervalPerDataRequest` | Dynamic | Time range per query |

### Tuning Guidelines

**For Raw Data**:
- Increase `BulkParallelChunkSize` for more parallelism (if system can handle it)
- Adjust `TimeIntervalPerDataRequest` based on data density
- Use `AutoTune()` method for automatic configuration

**For Summary Data**:
- Decrease `tagsPerChunk` for longer time periods (more intervals per batch)
- Increase `tagsPerChunk` for shorter time periods (fewer intervals per batch)
- Monitor network bandwidth and adjust accordingly

## Troubleshooting

### Performance Issues

**Symptom**: Slow data retrieval

**Diagnostic Steps**:
1. Check Statistics output for throughput
2. Monitor queue depths (high = bottleneck downstream)
3. Verify network latency to PI Server
4. Check PI Server load (16 threads saturated?)

**Solutions**:
- Adjust `tagsPerChunk` for summary operations
- Increase `TimeIntervalPerDataRequest` for larger time batches
- Reduce `MaxDegreeOfParallelism` if PI Server is overloaded

### Memory Issues

**Symptom**: Out of memory exceptions

**Solutions**:
- Reduce `TagGroupSize` to process fewer tags at once
- Reduce `BulkParallelChunkSize` to limit concurrent operations
- Increase `EventsPerFile` to create fewer files
- Enable write mode (`--enableWrite`) to stream data to disk

### Connection Issues

**Symptom**: Timeouts or connection errors

**Solutions**:
- Verify PI Server connectivity
- Check firewall rules
- Reduce `MaxDegreeOfParallelism` to lower connection count
- Increase timeout settings in PI SDK configuration

## Contributing

When contributing to the DataReader project:

1. **Maintain Thread Safety**: Always use thread-safe collections for cross-thread communication
2. **Follow Patterns**: Use the existing producer-consumer patterns
3. **Log Appropriately**: Use NLog (configured via `NLog.config`) for debugging and monitoring
4. **Document Performance**: Include benchmark results for significant changes
5. **Test at Scale**: Verify with large tag counts and long time ranges

## License

Copyright 2016 Patrice Thivierge F.

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
