using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using MusicBot.Services;
using System.Diagnostics;
using Newtonsoft.Json;

namespace MusicBot
{
    class Program
    {
        private IReadOnlyDictionary<string, string>? _config;
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _config = ReadConfig() ?? throw new FileLoadException("config.json");
            
            RequireFile("opus.dll");
            RequireFile("ffmpeg.exe");
            RequireFile("youtube-dl.exe");
            RequireFile("libsodium.dll");
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                await client.LoginAsync(TokenType.Bot, _config["TokenDiscord"]);
                await client.StartAsync();

                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

                await Task.Delay(Timeout.Infinite);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketConfig
                {
                    GatewayIntents = GatewayIntents.AllUnprivileged | 
                        GatewayIntents.MessageContent|
                        GatewayIntents.DirectMessages |
                        GatewayIntents.Guilds |
                        GatewayIntents.GuildBans |
                        GatewayIntents.GuildMessages |
                        GatewayIntents.MessageContent

                })
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton(new YouTubeService(new BaseClientService.Initializer()
                {
                    ApiKey = _config["TokenYoutube"],
                    ApplicationName = this.GetType().ToString()
                }))
                .AddSingleton<DatabaseService>()
                .BuildServiceProvider();
        }

        private void RequireFile(string filePath)
        {
            var path = Path.Combine(Environment.CurrentDirectory, filePath);
            Debug.Assert(File.Exists(path), $"Missing file: {path}");
        }

        private IReadOnlyDictionary<string, string>? ReadConfig()
        {
            var json = File.ReadAllText("config.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }
}