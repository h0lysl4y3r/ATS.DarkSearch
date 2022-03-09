using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/admin/urls", "GET")]
public class GetAllUrls : IGet, IReturn<GetAllUrlsResponse>
{
}

public class GetAllUrlsResponse
{
    public string[] Urls { get; set; }
}

[Route("/admin/spider-restart", "POST")]
public class RestartSpider : IPost
{
}

[Route("/admin/ping-all", "POST")]
public class PingAll : IPost
{
    public string LinkFileName { get; set; }
}

[Route("/admin/ping-republish", "POST")]
public class RepublishPings : IPost
{
    public int Count { get; set; } = 10;
}

[Route("/admin/ping-republish-store", "POST")]
public class RepublishPingsStore : IPost
{
    public int Count { get; set; } = 10;
}