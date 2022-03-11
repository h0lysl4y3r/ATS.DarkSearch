using System;

namespace ATS.DarkSearch;

public class SearchResultPoco
{
    public string Url { get; set; }

    public DateTimeOffset Date { get; set; }

    public string Domain { get; set; }
    
    public string Title { get; set; }

    public string Description { get; set; }

    public DateTimeOffset LastModified { get; set; }
}