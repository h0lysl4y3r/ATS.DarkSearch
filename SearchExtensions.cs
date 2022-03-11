using System;
using ATS.Common.Poco;
using ServiceStack;

namespace ATS.DarkSearch;

public static class SearchExtensions
{
    public static SearchResultPoco PopulateFromPing(this SearchResultPoco searchResult, PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));
        
        searchResult.PopulateWith(ping);

        if (searchResult.Description.IsNullOrEmpty()
            && !ping.Texts.IsEmpty())
        {
            var description = string.Join(" ", ping.Texts);
            var length = Math.Min(256, description.Length);
            if (length > 0)
                searchResult.Description = description.Substring(0, length);
        }

        return searchResult;
    }
    
}