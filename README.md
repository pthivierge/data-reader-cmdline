# DataReader
This command line application, wrote with C# reads data from the PI Data Archive.  
 

# Build
Once compiled, it created a **Build** folder in the solution folder.  You can take this folder and place it on the system you would like to make the test on.

# Prerequisites on host system
* .NET Framework 4.5+
* AFSDK 2.8+, for bulk calls support and also for the new *PIPointQuery.ParseQuery* method introduced in this version.

# Getting started

## Test PI Point Query(ies)

Before starting your data retrieval, it is best to check the tag filters you will be using to make sure you will have all the tags you need.  The application will start reading the data faster if you provide several small queries instead of a big one (e.g. *).

This command line will make 5 queries to get PI Tags.  Queries needs to be built on the [PIPointQuery syntax][1].  Each query needs to be separated by a space.  Put your query in quotes if it contain spaces. 
`datareader.exe --server PIServer01 --testTagSearch sinus* cdt* "tag:<>sin* DataType:Float" "PointSource:=#" "PointSource:=C"`

If you would like to see what are the tags that are included in your query, add *--printTags* command.
Example:
`datareader.exe --server PIServer01 --testTagSearch "tag:=Unit1* AND Location1:=1 AND PointSource:=OPC" --printTags`


## Read data
So, once your know that your tag queries will work as expected. You can query some data.  Start small to make sure things work as expected.

Read values for the last 30 days, no data will be written to file.  This is just to make a test reading data:  
`DataReader.exe -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 15 --estimatedTagscount 10000 --enableWrite --outfileName "C:\temp\data"`

See the **usage** section below to get more details about each parameter.

## Read data and write it to text files
Read values for the last 30 days and output the result into a file:  
`DataReader.exe -s PIServer01 -t PointSource:=# --st *-30d --et * --estimatedEventsPerDay 2 --estimatedTagscount 6207 --enableWrite --outfileName "C:\temp\data"`



# Usage



  -s, --server               Required. PI Data Archive Server name to connect

  -t, --tagQueries           Queries to load the tags, the more you add the
                             best and the sooner that app will start reading
                             data. This option accepts many queries separeted
                             by a space. e.g. sinus* SSN_NP60* "tag:<>sin*
                             DataType:Float"

  --testTagSearch            Makes a serch will all passed filters and prints
                             the results to the screen. e.g. sinus* SSN_NP60*
                             "tag:<>sin* DataType:Float"

  --printTags                Print all tag names when doing the testTagSearch

  --estimatedEventsPerDay    (Default: 4) provides an estimate of the number of
                             events per tag per day, to help optimising the
                             speed of reading

  --estimatedTagsCount       (Default: 10000) estimate of the total number of
                             tags that will be read, this will also help
                             optimizing the application

  --eventsPerRead            (Default: 10000) Defines how many events should be
                             read per data call.

  --st                       (Default: *-1d) Start Time to query data

  --et                       (Default: *) End Time to query data

  --enableWrite              (Default: False) Outputs the data into text files.  I not specified data is read but is not output.

  --writersCount             (Default: 4) Defines the numbers of files writers
                             that will runs simultaneously.

  --outFileName              file name to output data.  Works with the
                             EnableWrite option. A datetime and a .csv
                             extension wil appended the the name.  ex:
                             c:\temp\data would suffice

  --eventsPerFile            (Default: 50000) Number of events to write per
                             file

  --help                     Display this help screen.



#Notes

On a server with about 6000 tags, the following command gave very good read results.
It has yet to be tested on a server with a lot more tags.

`DataReader.exe -s PIServer01 -t * --st T-30d --et T --estimatedEventsPerDay 4 --estimatedTagscount 6207 --eventsPerRead 150000`


[1]:https://techsupport.osisoft.com/Documentation/PI-AF-SDK/html/b8fbb6da-7a4b-4570-a09d-7f2b85ed204d.htm

