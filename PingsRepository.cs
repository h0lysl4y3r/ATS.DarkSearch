using System;
using System.Collections.Generic;
using System.Linq;
using ATS.Common.Model;
using ATS.Common.Poco;
using Microsoft.Extensions.Configuration;
using OpenSearch.Client;
using ServiceStack;

namespace ATS.DarkSearch;

public class PingsRepository
{
    public const string PingsIndex = "pings";
    public const int DefaultSize = 10;

    private readonly OpenSearchClientFactory _clientFactory;
    private readonly List<string> _searchExcludeWords;

    public PingsRepository(OpenSearchClientFactory clientFactory, IConfiguration config)
    {
        _clientFactory = clientFactory;

        _searchExcludeWords = config.GetSection("AppSettings:SearchExcludeWords")
            .Get<List<string>>();
    }

    public IReadOnlyCollection<PingResultPoco> Search(string text, out long total, int from = 0, int size = DefaultSize, DateFilter dateFilter = DateFilter.Last3Years)
    {
        total = 0;

        if (text == null)
            throw new ArgumentNullException(nameof(text));

        Func<QueryContainerDescriptor<PingResultPoco>, QueryContainer> query
            = (QueryContainerDescriptor<PingResultPoco> selector) =>
            {
                QueryContainer container = selector.DateRange(c => c
                    .Field(p => p.LastModified)
                    .GreaterThanOrEquals(GetDateMath(dateFilter))
                    .LessThanOrEquals(DateMath.Now)
                    .Format("dd/MM/yyyy")
                    .TimeZone("+00:00"))
                && selector.MultiMatch(m => m
                    .Fields(f => f
                        .Field(f1 => f1.Title, boost: 3)
                        .Field(f2 => f2.Description, boost: 2)
                        .Field(f3 => f3.Texts))
                    .Query(text));

                if (!_searchExcludeWords.Any(x => text!.Contains(x)))
                {
                    foreach (var word in _searchExcludeWords)
                    {
                        container = container && selector.Bool(b =>
                            b.MustNot(mn =>
                                mn.MultiMatch(mm =>
                                    mm.Fields(f => f
                                        .Field(f1 => f1.Title, boost: 3)
                                        .Field(f2 => f2.Description, boost: 2)
                                        .Field(f3 => f3.Texts))
                                    .Query(word)
                                )));
                    }
                }

                return container;
            };

        var client = _clientFactory.Create();
        var response = client.Search<PingResultPoco>(x => x
            .From(from)
            .Size(size)
            .Query(query));

        total = response.Total;
        return response.Documents;
    }

    private DateMath GetDateMath(DateFilter filter)
    {
        switch (filter)
        {
            case DateFilter.LastYear:
                return DateMath.Now.Subtract("1y").RoundTo(DateMathTimeUnit.Month);
            case DateFilter.LastMonth:
                return DateMath.Now.Subtract("1M").RoundTo(DateMathTimeUnit.Day);
            case DateFilter.LastWeek:
                return DateMath.Now.Subtract("7d").RoundTo(DateMathTimeUnit.Hour);
        }

        return DateMath.Now.Subtract("3y").RoundTo(DateMathTimeUnit.Month);
    }

    public string[] GetUrls(string inputScrollId, out string outputScrollId, int maxResults)
    {
        if (maxResults <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        outputScrollId = null;

        var client = _clientFactory.Create();

        ISearchResponse<PingResultPoco> response = null;
        if (!inputScrollId.IsNullOrEmpty())
        {
            response = client.Scroll<PingResultPoco>("10s", inputScrollId);
        }
        else
        {
            response = client.Search<PingResultPoco>(s => s
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
        }

        var urls = new List<string>();
        while (response.Documents.Any())
        {
            outputScrollId = response.ScrollId;

            foreach (var document in response.Documents)
            {
                urls.Add(document.Url);
            }

            if (urls.Count >= maxResults)
                break;

            response = client.Scroll<PingResultPoco>("10s", response.ScrollId);
        }

        return urls.ToArray();
    }

    public long Count()
    {
        var client = _clientFactory.Create();

        var countRequest = new CountRequest(Indices.Index(PingsIndex));
        return client.Count(countRequest)?.Count ?? 0;
    }

    public PingResultPoco Get(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        var client = _clientFactory.Create();

        var response = client.Get<PingResultPoco>(url);
        return response.Found ? response.Source : null;
    }

    public bool Add(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        var client = _clientFactory.Create();

        return client.IndexDocument(ping).IsValid;
    }

    public bool Update(PingResultPoco ping)
    {
        if (ping == null)
            throw new ArgumentNullException(nameof(ping));

        var client = _clientFactory.Create();

        return client.Update<PingResultPoco, PingResultPoco>(
            ping.Url.ToString(), x => x.Doc(ping))
            .IsValid;
    }

    public bool Delete(string url)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url));

        var client = _clientFactory.Create();

        return client.Delete<PingResultPoco>(url).IsValid;
    }
}