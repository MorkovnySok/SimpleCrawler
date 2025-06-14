using SimpleCrawler;

if (args.Length != 3)
{
    Console.WriteLine(
        "Usage: SimpleCrawler.exe <url to crawl> <xpath to get content> <Authorization token>"
    );
    return;
}

var crawler = new DocumentationCrawler(
    baseUrl: args[0],
    xpath: args[1],
    authToken: args[2],
    outputDirectory: "CrawledData"
);

await crawler.CrawlAsync();
