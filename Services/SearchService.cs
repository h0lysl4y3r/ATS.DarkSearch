using System.Linq;
using ATS.DarkSearch.Model;
using ServiceStack;

namespace ATS.DarkSearch.Services;

public class SearchService : Service
{
    public object Post(Search request)
    {
        if (request.Page < 0)
            throw HttpError.BadRequest(nameof(request.Page));
        if (request.Text.IsNullOrEmpty())
            throw HttpError.BadRequest(nameof(request.Text));
        
        var repo = HostContext.AppHost.Resolve<PingsRepository>();
        var pings = repo.Search(request.Text, out var total, request.Page);
       
        return new SearchResponse()
        {
            Total = total,
            Results = pings
                .Select(x => new SearchResultPoco().PopulateFromPing(x))
                .ToArray()
        };
    }
        
}