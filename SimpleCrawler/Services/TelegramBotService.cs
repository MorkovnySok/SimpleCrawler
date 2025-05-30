using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SimpleCrawler.Services;

public class TelegramBotService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly RagProcessor _ragProcessor;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public TelegramBotService(
        IConfiguration configuration,
        RagProcessor ragProcessor,
        ILogger<TelegramBotService> logger)
    {
        var botToken = configuration.GetValue<string>("AppSettings:TelegramBotToken") 
            ?? throw new ArgumentNullException("TelegramBotToken must be configured");
        
        _botClient = new TelegramBotClient(botToken);
        _ragProcessor = ragProcessor;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cancellationTokenSource.Token
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is not { Text: { } messageText } message)
                return;

            _logger.LogInformation("Received message: {message}", messageText);

            // Send "typing" action
            await _botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing, cancellationToken: cancellationToken);

            // Process the question
            var answer = await _ragProcessor.AskQuestionAsync(messageText);

            // Send the response
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: answer,
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            if (update.Message != null)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Sorry, there was an error processing your question. Please try again later.",
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
} 