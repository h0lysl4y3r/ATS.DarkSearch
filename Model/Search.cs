using ATS.Common;
using ATS.Common.Poco;
using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/search", "POST")]
public class Search : BaseRequest, IPost, IReturn<SearchResponse>
{
    public string Text { get; set; }
    public int Page { get; set; } = 0;
}

public class SearchResponse
{
    public long Total { get; set; }
    public SearchResultPoco[] Results { get; set; }
}
