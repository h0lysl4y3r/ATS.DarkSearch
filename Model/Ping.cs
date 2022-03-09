using ATS.Common.Poco;

namespace ATS.DarkSearch.Model;

public class Ping
{
    public string Url { get; set; }
}

public class TryNewPing
{
    public string Url { get; set; }
}

public class UpdatePing
{
    public string Url { get; set; }
}

public class StorePing
{
    public PingResultPoco Ping { get; set; }
}