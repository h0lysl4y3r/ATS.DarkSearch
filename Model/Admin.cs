using ServiceStack;

namespace ATS.DarkSearch.Model;

// DEBUG
[Route("/admin/urls")]
public class GetUrlsRequest : IReturn<string[]>
{
}

[Route("/admin/restart-spider")]
public class RestartSpiderRequest
{
}