using System.Linq;
using System.Net.Http;

namespace ATS.DarkSearch;

public static class HttpExtensions
{
    public static bool HasHeaderSafe(this HttpResponseMessage responseMessage, string headerName)
    {
        if (responseMessage == null)
            return false;

        try
        {
            if (responseMessage.Headers.Contains(headerName))
                return true;

            return responseMessage.Content.Headers.Contains(headerName);
        }
        catch
        {
            return false;
        }
    }

    public static string GetHeaderValueSafe(this HttpResponseMessage responseMessage, string headerName)
    {
        if (responseMessage == null)
            return null;

        try
        {
            if (responseMessage.Headers.TryGetValues(headerName, out var headers))
                return headers.FirstOrDefault();

            if (responseMessage.Content.Headers.TryGetValues(headerName, out var contentHeaders))
                return contentHeaders.FirstOrDefault();

            return null;
        }
        catch
        {
            return null;
        }
    }
}