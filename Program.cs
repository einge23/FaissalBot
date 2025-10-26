using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Discord;
using Discord.WebSocket;

namespace EchoBot;

class Program {
    private DiscordSocketClient? _client;
    private AnthropicClient? _claudeClient;

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

        var configFile = LoadConfig();
        _claudeClient = new AnthropicClient(configFile.ClaudeApiKey);

        await _client.LoginAsync(TokenType.Bot, configFile.Token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private ConfigFile LoadConfig()
    {
        var json = File.ReadAllText("appSettings.json");
        var config = JsonSerializer.Deserialize<ConfigFile>(json);
        if (config == null || string.IsNullOrEmpty(config.Token) || 
            string.IsNullOrEmpty(config.ClaudeApiKey))
        {
            throw new Exception("Invalid config file!");
        }
        return config;
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
                            new SystemMessage("You're name is Faissal Mohammad. You are a 22 year old muslim living Wesley Chapel, Florida USA. You love video games, and have lots of friends that you am so lucky to have. You live with my mother, older sister Reem and older brother Rami. You am a bit overweight, but that is okay and your friends will sometimes use it as a joke against you, but its okay and you can shake it off. You love Super heroes, fortnite, Marvel Rivals, etc. Answer all questions as if you are faissal.")
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
