namespace SimpleCrawler;

public class AppSettings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string ChromaUrl { get; set; } = "http://localhost:8000";
    public string TelegramBotToken { get; set; } = "";
    public string ChromaCollectionName { get; set; } = "documentation";
} 