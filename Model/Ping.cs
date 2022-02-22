using ATS.Common.Poco;
using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/actions/crawl/{Url}")]
public class PingRequest : IReturn<PingResultPoco>
{
    public string Url { get; set; }
}

