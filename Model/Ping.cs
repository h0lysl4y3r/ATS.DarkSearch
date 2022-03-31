using ATS.Common;
using ATS.Common.Poco;

namespace ATS.DarkSearch.Model;

public class Ping : BaseRequest
{
    public string Url { get; set; }
}

public class TryNewPing : BaseRequest
{
    public string Url { get; set; }
}

public class UpdatePing : BaseRequest
{
    public string Url { get; set; }
}

public class StorePing : BaseRequest
{
    public PingResultPoco Ping { get; set; }
}