using ATS.Common.Poco;
using Nest;
using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/admin/urls", "GET")]
public class GetAllUrls : IGet, IReturn<GetAllUrlsResponse>
{
}

[Route("/admin/pings", "GET")]
public class GetPing : IGet, IReturn<GetPingResponse>
{
    public string Url { get; set; }
}

public class GetPingResponse
{
    public PingResultPoco Ping { get; set; }
}

public class GetAllUrlsResponse
{
    public string[] Urls { get; set; }
}

[Route("/admin/pings", "DELETE")]
public class DeletePing : IDelete
{
    public string Url { get; set; }
}

[Route("/admin/pings/all", "DELETE")]
public class DeleteAllPings : IDelete
{
}

[Route("/admin/spider/restart", "PUT")]
public class RestartSpider : IPut
{
}

[Route("/admin/queues/purge", "DELETE")]
public class PurgeQueues : IDelete
{
    public string TypeFullName { get; set; }
}

[Route("/admin/ping-all", "POST")]
public class PingAll : IPost
{
    public string LinkFileName { get; set; }
}

[Route("/admin/republish/ping", "POST")]
public class RepublishPings : IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/ping-store", "POST")]
public class RepublishPingsStore : IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/try-new-ping", "POST")]
public class RepublishTryNewPing : IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/update-ping", "POST")]
public class RepublishUpdatePing : IPost
{
    public int Count { get; set; } = 10;
}

