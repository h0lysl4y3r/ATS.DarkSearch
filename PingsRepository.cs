using System;
using System.Collections.Generic;
using System.Linq;
using ATS.Common.Poco;
using Nest;
using ServiceStack;

namespace ATS.DarkSearch;

public class PingsRepository
{
    public const string PingsIndex = "pings";

    private readonly ElasticClient _client;
    
    public PingsRepository(ElasticClient client)
    {
        _client = client;
    }
    
    public IReadOnlyCollection<PingResultPoco> Search(string text, int from = 0, int size = 10)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        
        var response = _client.Search<PingResultPoco>(x => x
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

    public string[] GetUrls()
    {
        var response = _client.Search<PingResultPoco>(s => s
            .Source(sf => sf
                .Includes(i => i 
                    .Fields(
                        f => f.Url
                    )
                )
            )
            .Query(q => q
                .MatchAll()
            )
            .Scroll("10s") 
        );

        var urls = new List<string>();
        while (response.Documents.Any()) 
        {
            foreach (var document in response.Documents)
            {
                urls.Add(document.Url);
            }
            response = _client.Scroll<PingResultPoco>("10s", response.ScrollId);
        }

        return urls.ToArray();
    }
    
    public PingResultPoco Get(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        var response = _client.Get<PingResultPoco>(url);
        return response.Found ? response.Source : null;
    }

    public bool Add(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        return _client.IndexDocument(ping).IsValid;
    }
    
    public bool Update(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        return _client.Update<PingResultPoco, PingResultPoco>(
            ping.Url.ToString(), x => x.Doc(ping))
            .IsValid;
    }
    
    public bool Delete(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        return _client.Delete<PingResultPoco>(url).IsValid;
    }
}