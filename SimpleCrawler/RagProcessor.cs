using System.Net.Http.Json;
using System.Text.Json;
using ChromaDB.Client;

namespace SimpleCrawler;

public class RagProcessor
{
    private readonly HttpClient _ollamaClient;
    private readonly ChromaClient _chromaClient;
    private ChromaCollectionClient _collectionClient;
    private const string EMBEDDING_MODEL = "nomic-embed-text";
    private const string CHAT_MODEL = "mistral";

    public RagProcessor(string ollamaUrl = "http://localhost:11434", string chromaUrl = "http://localhost:8000")
    {
        // Initialize Ollama client
        _ollamaClient = new HttpClient { BaseAddress = new Uri(ollamaUrl) };

        // Initialize ChromaDB client
        var configOptions = new ChromaConfigurationOptions(uri: $"{chromaUrl}/api/v1/");
        var chromaHttpClient = new HttpClient();
        _chromaClient = new ChromaClient(configOptions, chromaHttpClient);
    }

    public async Task InitializeCollection(string collectionName)
    {
        var collection = await _chromaClient.GetOrCreateCollection(collectionName);
        _collectionClient = new ChromaCollectionClient(collection, _chromaClient.ConfigurationOptions, _chromaClient.HttpClient);
    }

    private async Task<float[]> GetEmbeddingsFromOllama(string text)
    {
        var response = await _ollamaClient.PostAsJsonAsync("/api/embeddings", new
        {
            model = EMBEDDING_MODEL,
            prompt = text
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
        return result?.Embedding ?? Array.Empty<float>();
    }

    public async Task ImportDocumentsAsync()
    {
        var jsonFiles = Directory.GetFiles("CrawledData", "*.json");
        foreach (var jsonFile in jsonFiles)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFile);
            var documents = JsonSerializer.Deserialize<List<DocumentPage>>(jsonContent);

            if (documents == null) continue;

            foreach (var doc in documents)
            {
                var embedding = await GetEmbeddingsFromOllama(doc.Content);
                
                // Add document to ChromaDB
                await _collectionClient.Add(
                    ids: [doc.Url],
                    embeddings: [new ReadOnlyMemory<float>(embedding)],
                    metadatas: [new Dictionary<string, object> 
                    { 
                        { "url", doc.Url }
                    }],
                    documents: [doc.Content]
                );
            }
        }
    }

    public async Task<string> AskQuestionAsync(string question)
    {
        // Get embeddings for the question
        var questionEmbedding = await GetEmbeddingsFromOllama(question);

        // Query ChromaDB for relevant documents
        var queryResult = await _collectionClient.Query(
            queryEmbeddings: [new ReadOnlyMemory<float>(questionEmbedding)],
            nResults: 3,
            include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Documents | ChromaQueryInclude.Distances
        );

        if (!queryResult.Any() || !queryResult[0].Any())
        {
            return "No relevant information found.";
        }

        // Build context from search results
        var context = string.Join("\n\n", queryResult[0].Select(r => r.Document));

        // Generate response using Ollama
        var response = await _ollamaClient.PostAsJsonAsync("/api/chat", new
        {
            model = CHAT_MODEL,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant. Use the provided context to answer questions accurately. If you cannot find the answer in the context, say so." },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion: {question}\n\nAnswer based on the context:" }
            }
        });

        response.EnsureSuccessStatusCode();
        var chatResult = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return chatResult?.Message?.Content ?? "Failed to generate response.";
    }
}

public class OllamaEmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class OllamaChatResponse
{
    public OllamaChatMessage Message { get; set; } = new();
}

public class OllamaChatMessage
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = "";
} 