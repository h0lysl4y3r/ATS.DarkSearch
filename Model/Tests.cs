using System;
using ATS.Common.Poco;
using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/tests/ping", "POST")]
public class TestPing : IPost
{
    public string Url { get; set; }
}

[Route("/tests/index-add", "POST")]
public class IndexAdd : PingResultPoco, IPost
{
}

[Route("/tests/index-update", "PUT")]
public class IndexUpdate : PingResultPoco, IPut
{
    public PingResultPoco Ping { get; set; }
}

[Route("/tests/index-get", "GET")]
public class IndexGet : IGet, IReturn<IndexGetResponse>
{
    public string Url { get; set; }
}

public class IndexGetResponse
{
    public PingResultPoco Ping { get; set; }
}

[Route("/tests/index-get-all", "GET")]
public class IndexGetAllUrls : IGet, IReturn<IndexGetAllUrlsResponse>
{
}

public class IndexGetAllUrlsResponse
{
    public string[] Urls { get; set; }
}

[Route("/tests/index-search", "POST")]
public class IndexSearch : IPost, IReturn<IndexSearchResponse>
{
    public string Text { get; set; }
}

public class IndexSearchResponse
{
    public SearchResultPoco[] Results { get; set; }
}
