using ATS.Common;
using ATS.Common.Poco;
using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/admin/urls", "GET")]
public class GetAllUrls : BaseRequest, IGet, IReturn<GetAllUrlsResponse>
{
    public string InputScrollId { get; set; }
    public int MaxResults { get; set; }
}

public class GetAllUrlsResponse
{
    public string OutputScrollId { get; set; }
    public string[] Urls { get; set; }
}

[Route("/admin/pings", "GET")]
public class GetPing : BaseRequest, IGet, IReturn<GetPingResponse>
{
    public string Url { get; set; }
}

public class GetPingResponse
{
    public PingResultPoco Ping { get; set; }
}

[Route("/admin/pings", "DELETE")]
public class DeletePing : BaseRequest, IDelete
{
    public string Url { get; set; }
}

[Route("/admin/pings/all", "DELETE")]
public class DeleteAllPings : BaseRequest, IDelete
{
}

[Route("/admin/pings/all", "POST")]
public class PingAll : BaseRequest, IPost
{
    public string LinkFileName { get; set; }
}

[Route("/admin/pings", "POST")]
public class PingSingle : BaseRequest, IPost, IReturn<PingSingleResponse>
{
    public string Url { get; set; }
}

public class PingSingleResponse
{
    public PingResultPoco Ping { get; set; }
}

[Route("/admin/queues/purge", "DELETE")]
public class PurgeQueues : BaseRequest, IDelete
{
    public string TypeFullName { get; set; }
}

[Route("/admin/spider/restart", "PUT")]
public class RestartSpider : BaseRequest, IPut
{
}

[Route("/admin/spider/pause", "PUT")]
public class PauseSpider : BaseRequest, IPut
{
    public bool IsPaused { get; set; }
}

[Route("/admin/spider/state", "GET")]
public class GetSpiderState : BaseRequest, IGet, IReturn<GetSpiderStateResponse>
{
}

public class GetSpiderStateResponse
{
    public bool IsPaused { get; set; }
}

[Route("/admin/republish/ping", "POST")]
public class RepublishPings : BaseRequest, IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/ping-store", "POST")]
public class RepublishPingsStore : BaseRequest, IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/try-new-ping", "POST")]
public class RepublishTryNewPing : BaseRequest, IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/republish/update-ping", "POST")]
public class RepublishUpdatePing : BaseRequest, IPost
{
    public int Count { get; set; } = 10;
}

