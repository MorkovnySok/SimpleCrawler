using SimpleCrawler;
using SimpleCrawler.Services;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure RAG services
builder.Services.AddSingleton<RagProcessor>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var settings = config.GetSection("AppSettings").Get<AppSettings>()!;
    var processor = new RagProcessor(settings.OllamaUrl, settings.ChromaUrl);
    processor.InitializeCollection(settings.ChromaCollectionName).Wait();
    return processor;
});

// Add Telegram bot service
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
