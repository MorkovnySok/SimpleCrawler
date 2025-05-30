using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;

namespace SimpleCrawler;

public class DocumentationCrawler
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;

    private readonly HashSet<string> _visitedUrls = [];
    private readonly List<DocumentPage> _documents = [];
    private readonly string _xpath;
    private readonly string _authToken;
    private readonly string _outputDirectory;

    public DocumentationCrawler(
        string baseUrl,
        string xpath,
        string authToken,
        string outputDirectory
    )
    {
        _httpClient = new HttpClient(
            new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.All,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            }
        );
        _baseUri = new Uri(baseUrl);
        _xpath = xpath;
        _authToken = authToken;
        _outputDirectory = outputDirectory;

        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task CrawlAsync()
    {
        await CrawlPageAsync(_baseUri.AbsoluteUri);
        await SaveResultsAsync();
    }

    private async Task CrawlPageAsync(string url)
    {
        if (_visitedUrls.Contains(url))
        {
            return;
        }

        try
        {
            _visitedUrls.Add(url);
            Console.WriteLine($"Crawling: {url}");
            var request = CreateRequest(url);
            Console.WriteLine($"Headers: " + await request.ToRawString());
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var contentNode = doc.DocumentNode.SelectSingleNode(_xpath);
            if (contentNode == null)
            {
                await Console.Error.WriteLineAsync("No content node found. Check your xPath.");
                return;
            }

            _documents.Add(
                new DocumentPage
                {
                    Url = url,
                    Content = contentNode.InnerText.Trim(),
                }
            );

            var links = contentNode.SelectNodes("//a[@href]");
            if (links != null)
            {
                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        var absoluteUrl = new Uri(_baseUri, href).AbsoluteUri;
                        if (
                            absoluteUrl.StartsWith(_baseUri.AbsoluteUri)
                            && !_visitedUrls.Contains(absoluteUrl)
                            && !absoluteUrl.Contains('#')
                        )
                        {
                            await CrawlPageAsync(absoluteUrl);
                        }
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error crawling {url}: {ex.Message}");
        }
    }

    private HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
        );
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("Connection", "keep-alive");

        var token = _authToken;
        SetAuthorizationHeader(request, token);
        return request;
    }

    private void SetAuthorizationHeader(HttpRequestMessage request, string token)
    {
        if (token.Contains(":"))
        {
            token = token.Replace("Basic ", string.Empty);
            var data = token.Split(":");
            var username = data[0];
            var password = data[1];
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{username}:{password}")
            );
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
        else
        {
            token = token.Replace("Bearer ", string.Empty);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task SaveResultsAsync()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_documents, options);
        var outputPath = Path.Combine(_outputDirectory, "crawler_results.json");
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"Results saved to {outputPath}");
    }
}