using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Discord;
using Discord.WebSocket;

namespace EchoBot;

class Program {
    private DiscordSocketClient? _client;
    private AnthropicClient? _claudeClient;
    private string _systemPrompt = "";

    static Task Main(string[] args) => new Program().MainAsync();

    public async Task MainAsync()
    {
        var config = new DiscordSocketConfig {
            GatewayIntents =
                GatewayIntents.AllUnprivileged | 
                GatewayIntents.MessageContent
        };

        _client = new DiscordSocketClient(config);

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;

        _systemPrompt = LoadSystemPrompt();
        
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")
            ?? throw new Exception("DISCORD_BOT_TOKEN environment variable is not set!");
        
        var claudeApiKey = Environment.GetEnvironmentVariable("CLAUDE_API_KEY")
            ?? throw new Exception("CLAUDE_API_KEY environment variable is not set!");
        
        _claudeClient = new AnthropicClient(claudeApiKey);

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private string LoadSystemPrompt()
    {
        if (File.Exists("system_prompt.txt"))
        {
            return File.ReadAllText("system_prompt.txt");
        }
        return "You are a helpful AI assistant.";
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private Task ReadyAsync()
    {
        Console.WriteLine($"{_client?.CurrentUser} is connected and ready");
        return Task.CompletedTask;
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        Console.WriteLine($"Message received from {message.Author.Username}: {message.Content}");
        
        if (message.Author.IsBot) 
        {
            Console.WriteLine("Ignoring bot message");
            return;
        }
        if (_client?.CurrentUser == null) 
        {
            Console.WriteLine("Client or CurrentUser is null");
            return;
        }

        var userMessage = message as SocketUserMessage;
        if (userMessage == null) 
        {
            Console.WriteLine("Message is not SocketUserMessage");
            return;
        }

        var isMentioned = message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id) ||
                           message.Content.Contains($"<@&{_client.CurrentUser.Id}>");

        if (isMentioned)
        {
            Console.WriteLine($"Bot mentioned, content: {message.Content}");
            var content = message.Content
                .Replace($"<@{_client.CurrentUser.Id}>", "")
                .Replace($"<@&{_client.CurrentUser.Id}>", "")
                .Trim();
            Console.WriteLine($"Processed content: {content}");

            if(string.IsNullOrWhiteSpace(content))
            {
                await message.Channel.SendMessageAsync("Bruh send some text");
                return;
            }

            using (message.Channel.EnterTypingState())
            {
                try
                {
                    var messages = new List<Message>
                    {
                        new(RoleType.User, content)
                    };

                    var parameters = new MessageParameters
                    {
                        Messages = messages,
                        Model = "claude-sonnet-4-20250514",
                        MaxTokens = 1024,
                        Stream = false,
                        System =
                        [
                            new SystemMessage(_systemPrompt)
                        ]
                    };

                    Console.WriteLine("Calling Claude API...");
                    var response = await _claudeClient!.Messages.GetClaudeMessageAsync(parameters);
                    var claudeResponse = response.Content[0].ToString() ?? "";
                    var preview = claudeResponse.Length > 100 ? claudeResponse[..100] : claudeResponse;
                    Console.WriteLine($"Got response: {preview}...");
                    
                    if (!string.IsNullOrWhiteSpace(claudeResponse) && claudeResponse.Length > 2000)
                    {
                        var chunks = SplitMessage(claudeResponse, 2000);
                        foreach (var chunk in chunks)
                        {
                            await userMessage.ReplyAsync(chunk);
                        }
                    }
                    else
                    {
                        await userMessage.ReplyAsync(claudeResponse);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Claude API Error: {ex}");
                    try
                    {
                        await message.Channel.SendMessageAsync($"Error: {ex.Message}");
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"Failed to send error message: {innerEx}");
                    }
                }
            }
        }
    }

    private List<string> SplitMessage(string text, int maxLength)
    {
        var result = new List<string>();
        for (int i = 0; i < text.Length; i += maxLength)
        {
            result.Add(text.Substring(i, Math.Min(maxLength, text.Length - i)));
        }
        return result;
    }

    public class ConfigFile
    {
        public string Token { get; set; } = string.Empty;
        public string ClaudeApiKey { get; set; } = string.Empty;
    }
}
