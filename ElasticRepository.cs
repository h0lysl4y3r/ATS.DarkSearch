using System;
using System.Collections.Generic;
using ATS.Common.Poco;
using Nest;
using ServiceStack;

namespace ATS.DarkSearch;

public class ElasticRepository
{
    public const string PingsIndex = "pings";

    public IReadOnlyCollection<PingResultPoco> Search(string text, int from = 0, int size = 10)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        
        var client = HostContext.AppHost.Resolve<ElasticClient>();
        var response = client.Search<PingResultPoco>(x => x
            .From(from)
            .Size(size)
            .Query(q => q
                .MultiMatch(m => m
                    .Fields(f => f
                        .Field(f1 => f1.Title)
                        .Field(f2 => f2.Description))
                    .Query(text))));

        return response.Documents;
    }
    
    public PingResultPoco GetPing(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        var client = HostContext.AppHost.Resolve<ElasticClient>();
        var response = client.Get<PingResultPoco>(url);

        return response.Found ? response.Source : null;
    }

    public void AddPing(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        var client = HostContext.AppHost.Resolve<ElasticClient>();
        client.IndexDocument(ping);
    }
    
    public void UpdatePing(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        var client = HostContext.AppHost.Resolve<ElasticClient>();
        client.Update<PingResultPoco, PingResultPoco>(
            ping.Url.ToString(), x => x.Doc(ping));
    }
}