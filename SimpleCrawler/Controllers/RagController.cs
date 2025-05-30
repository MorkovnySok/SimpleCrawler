using Microsoft.AspNetCore.Mvc;

namespace SimpleCrawler.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RagController(RagProcessor ragProcessor, ILogger<RagController> logger)
    : ControllerBase
{
    [HttpPost("crawl")]
    public async Task<IActionResult> Crawl([FromBody] CrawlRequest request)
    {
        try
        {
            var crawler = new DocumentationCrawler(
                request.Url,
                request.Xpath,
                request.AuthToken,
                "crawled-data"
            );
            await crawler.CrawlAsync();
            await ragProcessor.ImportDocumentsAsync();
            return Ok("Crawling and importing completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during crawling");
            return StatusCode(500, "Error during crawling: " + ex.Message);
        }
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] QuestionRequest request)
    {
        try
        {
            var answer = await ragProcessor.AskQuestionAsync(request.Question);
            return Ok(new { answer });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing question");
            return StatusCode(500, "Error processing question: " + ex.Message);
        }
    }
}

public class CrawlRequest
{
    public string Url { get; set; } = "";
    public string Xpath { get; set; } = "";
    public string AuthToken { get; set; } = "";
}

public class QuestionRequest
{
    public string Question { get; set; } = "";
}
