using ServiceStack;

namespace ATS.DarkSearch.Model;

// DEBUG
[Route("/admin/urls")]
public class GetUrlsRequest : IGet, IReturn<string[]>
{
}

[Route("/admin/restart-spider")]
public class RestartSpiderRequest : IPost
{
}

[Route("/admin/ping-all")]
public class PingAllRequest : IPost
{
}
