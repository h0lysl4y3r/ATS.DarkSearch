using ServiceStack;

namespace ATS.DarkSearch.Model;

// DEBUG
[Route("/admin/urls", "GET")]
public class GetUrlsRequest : IGet, IReturn<string[]>
{
}

[Route("/admin/restart-spider", "POST")]
public class RestartSpiderRequest : IPost
{
}

[Route("/admin/ping-all", "POST")]
public class PingAllRequest : IPost
{
}
