using System.Text;

namespace SimpleCrawler;

public static class Extensions
{
    public static async Task<string> ToRawString(this HttpRequestMessage request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{request.Method} {request.RequestUri} HTTP/{request.Version}");
        foreach (var header in request.Headers)
        {
            sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
        }
        if (request.Content != null)
        {
            sb.AppendLine();
            sb.AppendLine(await request.Content.ReadAsStringAsync());
        }
        return sb.ToString();
    }
}