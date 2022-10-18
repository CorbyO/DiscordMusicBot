using Discord.WebSocket;
using Discord;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using System.Text;
using Google.Apis.YouTube.v3.Data;
using System.Diagnostics;
using Google.Apis.Util;
using Discord.Audio;
using NAudio.Wave;

namespace MusicBot
{
    class Program
    {
        private readonly DiscordSocketClient _client;
        private readonly YouTubeService _youtubeService;
        
        public readonly struct ReservedData
        {
            public readonly SearchResult SearchResult;
            public readonly IUser User;
            public ReservedData(SearchResult SearchResult, IUser User)
            {
                this.SearchResult = SearchResult;
                this.User = User;
            }
        }
        public struct SearchResultData
        {
            public SearchResult[] SearchResult;
            public readonly IUser User;
            public SearchResultData(IUser User)
            {
                this.SearchResult = new Google.Apis.YouTube.v3.Data.SearchResult[5];
                this.User = User;
            }
            public void Empty()
            {
                for (int i = 0; i < 5; i++)
                {
                    SearchResult[i] = null;
                }
            }
            public void Fill(IList<SearchResult> searchResults) 
            {
                Debug.Assert(searchResults.Count >= 5, "Search results must be at least 5");
                for (int i = 0; i < 5; i++)
                {
                    SearchResult[i] = searchResults[i];
                }
            }
        }
        public readonly struct GuildData
        {
            public readonly Queue<ReservedData> Queue;
            public readonly Dictionary<ulong, SearchResultData> SearchTemp;

            public GuildData()
            {
                Queue = new Queue<ReservedData>();
                SearchTemp = new Dictionary<ulong, SearchResultData>();
            }
        }

        private readonly Dictionary<ulong, GuildData> _guilds;
        
        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public Program()
        {
            _youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyDJJorP7i-yn99VcSJXt662Rmdw2T_obog",
                ApplicationName = this.GetType().ToString()
            });

            _client = new DiscordSocketClient();
            _guilds = new Dictionary<ulong, GuildData>();

            _client.Log += Log;
            _client.Ready += Ready;
            _client.MessageReceived += MessageReceivedAsync;
            _client.GuildAvailable += _client_GuildAvailable;
        }

        private async Task _client_GuildAvailable(SocketGuild arg)
        {
            if (!_guilds.ContainsKey(arg.Id))
            {
                _guilds.Add(arg.Id, new GuildData());
            }
        }

        public async Task MainAsync()
        {
            await _client.LoginAsync(TokenType.Bot, "토큰");
            await _client.StartAsync();

            await Task.Delay(-1);

        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private Task Ready()
        {
            Console.WriteLine($"{_client.CurrentUser} 연결됨!");

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            if (message.Author.Id == _client.CurrentUser.Id) return;

            var content = message.Content;

            if (content[0] == '%')
            {
                var commands = GetCommands(content);
                Execute(message, commands);
            }
        }

        private string[] GetCommands(string content)
        {
            var commands = content.Split(' ');

            commands[0].Remove(0);

            return commands;
        }

        public async void Execute(SocketMessage message, string[] commands)
        {
            switch(commands[0])
            {
                case "실행": case "ㅅㅎ": case "ㅅ": case "tlfgod": case "tg": case "t":
                    {
                        if (commands.Length > 1) throw new CommandInvalidException("검색 할 내용이 없습니다.");

                        // youtube search only video and song
                        var searchListRequest = _youtubeService.Search.List("snippet");
                        searchListRequest.Q = GetSearchKeyword(commands);
                        searchListRequest.MaxResults = 5;
                        searchListRequest.Type = "video";

                        var searchListResponse = await searchListRequest.ExecuteAsync();

                        // send message youtube search result to discord channel
                        var searchResultMessage = GetSearchResultMessage(searchListResponse.Items);
                        await message.Channel.SendMessageAsync(searchResultMessage);

                        var searchTemp = _guilds[GetGuild(message).Id].SearchTemp;
                        var authorID = message.Author.Id;
                        if (!searchTemp.ContainsKey(authorID))
                        {
                            searchTemp.Add(authorID, new SearchResultData(message.Author));
                        }

                        searchTemp[authorID].Fill(searchListResponse.Items);
                    }
                    break;
                case "1": case "2": case "3": case "4": case "5":
                    {
                        var guild = _guilds[GetGuild(message).Id];
                        // check for search result
                        var searchTemp = guild.SearchTemp;
                        var authorID = message.Author.Id;
                        if (!searchTemp.ContainsKey(authorID))
                        {
                            throw new CommandInvalidException("검색 한 적이 없습니다.");
                        }

                        // enqueue
                        var result = searchTemp[authorID].SearchResult[int.Parse(commands[0]) - 1];
                        guild.Queue.Enqueue(new ReservedData(result, message.Author));

                        // join voice channel
                        var voiceChannel = GetJoindVoiceChannel(message);
                        var audioClient = await voiceChannel.ConnectAsync();

                        PlayQueue(audioClient, guild.Queue);
                    }
                    break;
            }
        }

        private async void PlayQueue(IAudioClient audioClient, Queue<ReservedData> queue)
        {
            using (audioClient)
            {
                do
                {
                    var reservedData = queue.Peek();

                    
                    
                    queue.Dequeue();
                } while (queue.Count == 0);
            }
        }
        
        public SocketVoiceChannel GetJoindVoiceChannel(SocketMessage message)
        {
            var guild = (message.Channel as SocketGuildChannel)?.Guild ?? throw new Exception();
            var user = message.Author;
            var audioChannel = guild.GetUser(user.Id).VoiceChannel;

            return audioChannel;
        }

        public IGuild GetGuild(SocketMessage message)
        {
            return (message.Channel as SocketGuildChannel)?.Guild ?? throw new Exception();
        }

        private string GetSearchKeyword(string[] commands)
        {
            var sb = new StringBuilder();
            
            for (int i = 1; i < commands.Length; i++)
            {
                sb.Append(commands[i]);
            }

            return sb.ToString();
        }

        private string GetSearchResultMessage(IList<SearchResult> searchResults)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < searchResults.Count; i++)
            {
                sb.Append($"{i + 1}. {searchResults[i].Snippet.Title}\n");
            }

            return sb.ToString();
        }
    }
}