using System;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Discord;
using Discord.Addons.Music.Common;
using Discord.Addons.Music.Player;
using Discord.Audio;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.YouTube.v3;
using MusicBot.Extension;
using MusicBot.Services;
using static System.Net.Mime.MediaTypeNames;
using RunMode = Discord.Commands.RunMode;

namespace MusicBot.Module
{
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        public YouTubeService YouTubeService { get; set; }
        public DatabaseService DatabaseService { get; set; }

        private Emoji _process = new Emoji("💬");
        private Emoji _error = new Emoji("⛔");
        private Emoji _success = new Emoji("✅");

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => SendAnswer(_success, "Pong");
        
        [Command("검색")]
        [Alias("ㄳ", "ㄱㅅ", "ㄱ", "rjator", "rt", "r", "search")]
        public Task SearchAsync()
            => SendAnswer(_error, "검색할 텍스트와 같이 명령해 주세요.");

        [Command("검색")]
        [Alias("ㄳ","ㄱㅅ", "ㄱ", "rjator", "rt", "r", "search")]
        public Task SearchAsync([Remainder] string text)
        {
            Context.Message.AddReactionAsync(_process);
            var guildUser = Context.User as IGuildUser;
            var guild = Context.Guild;

            if (guildUser == null) return SendAnswer(_error, "알 수 없는 오류");
            if (text.Length > 100) return SendAnswer(_error, "검색할 텍스트는 100자 이하로 해 주세요.");
            if (guildUser.VoiceChannel == null) return SendAnswer(_error, "음성 채널에 들어가 있어야 합니다.");

            var results = Search(text, guild, Context.User).Result;
            if (results.Count == 0) return SendAnswer(_error, "검색 결과가 없습니다.");

            var search = DatabaseService[guild.Id].SearchTemp;
            var authorID = guildUser.Id;
            if (!search.TryGetValue(authorID, out var resultTemp))
                search.Add(authorID, resultTemp = new DatabaseService.SearchResultData());

            resultTemp.Fill(results);
            var task = SendAnswer(_success, ToEmbedFieldBuilder(text, results).Result);

            resultTemp.Message = task.Result;
            return task;
        }

        [Command("큐")]
        [Alias("ㅋ", "zb", "z", "Queue", "queue")]
        public Task PrintQueue()
        {
            var queue = DatabaseService[Context.Guild.Id].Queue;
            if (queue.Count == 0) return ReplyAsync("대기중인 노래 리스트가 없습니다.");

            StringBuilder sb = new("대기중인 노래 리스트\n");

            foreach (var i in DatabaseService[Context.Guild.Id].Queue)
            {
                sb.AppendLine(i.SearchResult.Snippet.Title);
            }

            return ReplyAsync(sb.ToString());
        }

        [Command("1", RunMode = RunMode.Async)] public Task Play1Async() => PlayAsync(0);
        [Command("2", RunMode = RunMode.Async)] public Task Play2Async() => PlayAsync(1);
        [Command("3", RunMode = RunMode.Async)] public Task Play3Async() => PlayAsync(2);
        [Command("4", RunMode = RunMode.Async)] public Task Play4Async() => PlayAsync(3);
        [Command("5", RunMode = RunMode.Async)] public Task Play5Async() => PlayAsync(4);

        private Task PlayAsync(int index)
        {
            Context.Message.AddReactionAsync(_process);

            if (!(Context.User is IGuildUser guildUser)) return SendAnswer(_error, "알 수 없는 오류");

            var guild = DatabaseService[Context.Guild.Id];
            // check for search result
            var authorID = guildUser.Id;
            if (!guild.SearchTemp.TryGetValue(authorID, out var searchResult)) return SendAnswer(_error, "먼저 '%검색' 명령어로 검색을 해 주세요.");

            var result = searchResult.SearchResultOrNull[index];
            searchResult.Empty();
            if (result == null) return SendAnswer(_error, "먼저 '%검색' 명령어로 검색을 해 주세요.");

            var voiceChannel = guildUser.VoiceChannel;
            if (voiceChannel == null) return SendAnswer(_error, "음성 채널에 들어가 있어야 합니다.");

            var temp = SendAnswer(_success, searchResult.Message, (x) =>
            {
                x.Embeds = new Embed[]
                {
                    new EmbedBuilder()
                        .WithAuthor("노래 선택")
                        .WithTitle($"{result?.Snippet?.Title?.Trim()}")
                        .WithThumbnailUrl(result?.Snippet?.Thumbnails?.Medium?.Url)
                        .WithColor(Color.DarkRed)
                        .WithDescription($"[{result?.Snippet?.ChannelTitle}](https://www.youtube.com/channel/{result?.Snippet?.ChannelId})")
                        .WithUrl($"https://www.youtube.com/watch?v={result?.Id?.VideoId}")
                        .WithFooter("선택한 노래가 대기열에 추가 되었습니다.")
                        .Build()
                };
            });

            // enqueue
            EnqueueAndPlay(new DatabaseService.ReservedData(result, guildUser), voiceChannel);

            return temp;
        }
        
        private async Task<IList<Google.Apis.YouTube.v3.Data.SearchResult>> Search(string keyword, IGuild guild, IUser user)
        {
            var searchListRequest = YouTubeService.Search.List("snippet");
            searchListRequest.Q = $"\"{keyword}\"";
            searchListRequest.MaxResults = 5;
            searchListRequest.Type = "video";
            searchListRequest.VideoDefinition = SearchResource.ListRequest.VideoDefinitionEnum.Any;
            searchListRequest.VideoDimension = SearchResource.ListRequest.VideoDimensionEnum.Value2d;
            searchListRequest.VideoCaption = SearchResource.ListRequest.VideoCaptionEnum.ClosedCaption;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
            searchListRequest.VideoCategoryId = "10";

            var searchListResponse = await searchListRequest.ExecuteAsync();

            return searchListResponse.Items;
        }

        private async Task<EmbedBuilder> ToEmbedFieldBuilder(string keyword, IList<Google.Apis.YouTube.v3.Data.SearchResult> searchResults)
        {
            var count = searchResults.Count;
            var ids = new string[count];
            var list = YouTubeService.Videos.List("contentDetails");
            for (int i = 0; i < count; i++)
            {
                ids[i] = searchResults[i].Id.VideoId;
            }
            list.Id = ids;
            var listResult = await list.ExecuteAsync();

            var fields = new EmbedFieldBuilder[count];
            for (int i = 0; i < count; i++)
            {
                var result = listResult.Items[i];
                var time = result.ContentDetails.Duration
                    .Replace("PT", null)
                    .Replace("H", "h ")
                    .Replace('M', ':')
                    .Replace("S", null);

                var title = searchResults[i].Snippet.Title
                    .Remove("[]")
                    .Omit(40);

                fields[i] = new EmbedFieldBuilder()
                    .WithName($"{(i + 1).ToEmoji()} {title}")
                    .WithValue($"`{time}`");
            }

            return new EmbedBuilder()
                .WithAuthor("노래 검색")
                .WithTitle($"키워드: `{keyword}`")
                .WithFields(fields)
                .WithColor(Color.Red)
                .WithThumbnailUrl("https://lh3.googleusercontent.com/DMPqTbcN-R_kPwzF0qg9zZH8UPLtVBoqrDQ_63zhmIq5NUBrllM5Xkj2h7Bi0X_KPzJ6_sTvRFIXWB2HIEeFd2EtnRyUbs0uWTPey3MYtSICaibNBfcA=v0-s1050")
                .WithFooter("듣고 싶은 노래를 %숫자 로 선택 해주세요.");
        }

        private async void EnqueueAndPlay(DatabaseService.ReservedData reservedData, IVoiceChannel voiceChannel)
        {
            var guild = DatabaseService[Context.Guild.Id];
            guild.Queue.Enqueue(reservedData);
            if (!guild.IsPlaying)
            {
                guild.IsPlaying = true;
                var audioClient = await voiceChannel.ConnectAsync();
                PlayQueue(audioClient, guild.Queue);
            }
        }

        private async void PlayQueue(IAudioClient audioClient, Queue<DatabaseService.ReservedData> queue)
        {
            using (var discordStream = audioClient.CreatePCMStream(AudioApplication.Music))
            {
                var player = new AudioPlayer(audioClient);
                while (queue.Count > 0)
                {
                    var reservedData = queue.Dequeue();

                    // youtube audio download
                    string query = $"https://www.youtube.com/watch?v={reservedData.SearchResult.Id.VideoId}";
                    bool wellFormedUri = Uri.IsWellFormedUriString(query, UriKind.Absolute);

                    var tracks = await TrackLoader.LoadAudioTrack(query, fromUrl: wellFormedUri);

                    foreach (var track in tracks)
                    {
                        await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor("노래 재생")
                            .WithTitle(track.Info.Title)
                            .WithUrl(query)
                            .WithThumbnailUrl(track.Info.ThumbnailUrl)
                            .WithColor(Color.Blue)
                            .WithDescription(track.Info.Duration + " 초")
                            .Build());
                        await player.StartTrackAsync(track);
                    }
                }
            }
            DatabaseService[Context.Guild.Id].IsPlaying = false;
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, string message)
        {
            var target = Context.Message;

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(message);
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, EmbedBuilder embedBuilder)
        {
            var target = Context.Message;

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(embed: embedBuilder.Build());
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, IUserMessage userMessage, string message)
        {
            _ = Context.Message.AddReactionAsync(emoji);
            await userMessage.ModifyAsync(x => x.Content = message);
            return userMessage;
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, IUserMessage userMessage, Action<MessageProperties> action)
        {
            _ = Context.Message.AddReactionAsync(emoji);

            await userMessage.ModifyAsync(action);
            return userMessage;
        }
    }
}
