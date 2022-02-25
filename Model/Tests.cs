using ServiceStack;

namespace ATS.DarkSearch.Model;

[Route("/tests/ping")]
public class TestPing
{
    public string Url { get; set; }
}

