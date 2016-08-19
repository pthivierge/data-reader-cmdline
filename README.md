# DataReader
This command line application, wrote with C# reads data from the PI Data Archive.  
The current form reads using the plot values bulk call, but can potentially be extended in an easy manner.  

# Build
Once compiled, it created a **Build** folder in the solution folder.  You can take this folder and place it on the system you would like to make the test on.

# Prerequisites on host system
* .NET Framework 4.5+
* AFSDK 2.6+, for bulk calls support

# Getting started
Read plot values for the last 30 days:  
`datareader.exe -s PIServer01 --st *-30d --et * -t Tag1 Tag2 Tag3 Tag4`;

Read plot values for the last 30 days and output the result into a file:  
`datareader.exe -s PIServer01 --st *-30d --et * -t Tag1 Tag2 Tag3 Tag4 --enableWrite --outFileName C:\Temp\data`;

For plotvalues intervals parameter, see [AFListData, PlotValues Method Documentation](information[1:https://techsupport.osisoft.com/Documentation/PI-AF-SDK/html/M_OSIsoft_AF_Data_AFListData_PlotValues.htm)

# Usage

     -s, --server                 Required. PI Data Archive Server name to connect
     
     --st                         (Default: *-1d) Start Time to query data
     --et                         (Default: *)    End Time to query data
    
    -q, --queryMode              (Default: Bulk) Mode of data query. Currenlty allowed values: Bulk

     -i, --intervalsCount        (Default: 1) Splits the main interval into sub-intervals. i.e. 30d span / 30 (intervalsCount) = 30 calls for 1 day.

     -p, --plotValuesIntervals    (Default: 1024) Number of intervalls to pass to
                                  the plotvalues calls
     
     -t, --tagsList               (Default: System.String[]) List of tags to query
                                  data for default: sinusoid cdt158
                                  
     --help                       Display this help screen.
## Parameters to write to a file  
     --enableWrite                (Default: False) Outputs the data into text
                                  files

     --outFileName                file name to output data into.  Works with the
                                  EnableWrite option. A datetime and a .csv
                                  extension wil appended the the name.  ex:
                                  c:\temp\data would suffice

     --eventsPerFile              (Default: 50000) Number of events to write per
                                  file




