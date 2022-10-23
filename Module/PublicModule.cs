using System;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Xml;
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

        [Command("도움말")]
        [Alias("ㄷㅇㅁ", "ㄷ", "ehdnaakf", "eda", "e", "?")]
        public Task PrintHelp()
        {
            var embed = new EmbedFieldBuilder[5]
            {
                new EmbedFieldBuilder()
                    .WithName(":small_blue_diamond: %재생 <링크>: 해당 유튜브 링크를 실행합니다.")
                    .WithValue("줄임: ㅈㅅ, ㅈ, wotod, wt, w"),
                new EmbedFieldBuilder()
                    .WithName(":small_blue_diamond: %검색 <검색어>: 검색어를 유튜브에서 찾습니다.")
                    .WithValue("줄임: ㄳ, ㄱㅅ, ㄱ, rjator, rt, r, search"),
                new EmbedFieldBuilder()
                    .WithName(":small_blue_diamond: %숫자: 검색 목록에서 항목을 고릅니다.")
                    .WithValue("줄임: "),
                new EmbedFieldBuilder()
                    .WithName(":small_blue_diamond: %큐: 실행 예정 목록을 불러옵니다.")
                    .WithValue("줄임: ㅋ, zb, z, Queue, queue"),
                new EmbedFieldBuilder()
                    .WithName(":small_blue_diamond: %스킵: 현재 실행중인 노래를 넘깁니다.")
                    .WithValue("줄임: ㅅ, ㅅㅋ, tmzlq, t, tz")
            };
            return SendAnswer(_success, new EmbedBuilder()
                .WithColor(Color.Orange)
                .WithTitle("도움말")
                .WithDescription("명령어의 목록 입니다.")
                .WithFields(embed));
        }

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
                sb.AppendLine(i.Title);
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
            var reservedData = new DatabaseService.ReservedData
            (
                new Uri($"https://www.youtube.com/watch?v={result?.Id?.VideoId}"),
                result.Snippet.Title.Trim(),
                guildUser
            );

            var voiceChannel = guildUser.VoiceChannel;
            if (voiceChannel == null) return SendAnswer(_error, "음성 채널에 들어가 있어야 합니다.");

            var temp = SendAnswer(_success, searchResult.Message, (x) =>
            {
                x.Embeds = new Embed[]
                {
                    new EmbedBuilder()
                        .WithAuthor("노래 선택")
                        .WithTitle(reservedData.Title)
                        .WithThumbnailUrl(result?.Snippet?.Thumbnails?.Medium?.Url)
                        .WithColor(Color.DarkRed)
                        .WithDescription($"[{result?.Snippet?.ChannelTitle}](https://www.youtube.com/channel/{result?.Snippet?.ChannelId})")
                        .WithUrl(reservedData.Uri.ToString())
                        .WithFooter("선택한 노래가 대기열에 추가 되었습니다.")
                        .Build()
                };
            });

            // enqueue
            EnqueueAndPlay(reservedData, voiceChannel);

            return temp;
        }

        [Command("재생")]
        [Alias("ㅈㅅ", "ㅈ", "wotod", "wt", "w")]
        public Task PlayURL([Remainder] string url)
        {
            Context.Message.AddReactionAsync(_process);
            var guildUser = Context.User as IGuildUser;
            var guild = Context.Guild;

            if (guildUser == null) return SendAnswer(_error, "알 수 없는 오류");
            var voiceChannel = guildUser.VoiceChannel;
            if (voiceChannel == null) return SendAnswer(_error, "음성 채널에 들어가 있어야 합니다.");
            var youtubeID = url.ToYoutubeID();
            if (youtubeID == null) return SendAnswer(_error, "유효한 유튜브 링크가 아닙니다.");

            var list = YouTubeService.Videos.List("snippet");
            list.Id = youtubeID;
            var response = list.Execute();
            var result = response.Items[0];
            
            var reservedData = new DatabaseService.ReservedData
            (
                new Uri(url),
                result.Snippet.Title.Trim(),
                guildUser
            );

            EnqueueAndPlay(reservedData, voiceChannel);

            return SendAnswer(_success,
                new EmbedBuilder()
                    .WithAuthor("노래 선택")
                    .WithTitle(reservedData.Title)
                    .WithThumbnailUrl(result?.Snippet?.Thumbnails?.Medium?.Url)
                    .WithColor(Color.DarkRed)
                    .WithDescription($"[{result?.Snippet?.ChannelTitle}](https://www.youtube.com/channel/{result?.Snippet?.ChannelId})")
                    .WithUrl(url)
                    .WithFooter("선택한 노래가 대기열에 추가 되었습니다.")
            );
        }

        [Command("스킵")]
        [Alias("ㅅ", "ㅅㅋ", "tmzlq", "t", "tz")]
        public Task SkipQueue()
        {
            Context.Message.AddReactionAsync(_process);
            var audioPlayer = DatabaseService[Context.Guild.Id].AudioPlayer;
            if (audioPlayer == null) return SendAnswer(_error, "재생 중이 아닙니다.");
            else
            {
                audioPlayer.Stop();
                return SendAnswer(_success, "노래를 스킵했습니다.");
            }
        }


        private async Task<IList<Google.Apis.YouTube.v3.Data.SearchResult>> Search(string keyword, IGuild guild, IUser user)
        {
            var searchListRequest = YouTubeService.Search.List("snippet");
            searchListRequest.Q = $"\"{keyword}\"";
            searchListRequest.MaxResults = 5;
            searchListRequest.Type = "video";
            searchListRequest.VideoDefinition = SearchResource.ListRequest.VideoDefinitionEnum.Any;
            searchListRequest.VideoDimension = SearchResource.ListRequest.VideoDimensionEnum.Value2d;
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
                var time = XmlConvert.ToTimeSpan(result.ContentDetails.Duration).ToMMSS();
                var title = searchResults[i].Snippet.Title.Remove("[]").Omit(40);

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
                var audioClient = await voiceChannel.ConnectAsync();
                PlayQueue(audioClient, guild.Queue);
            }
        }

        private async void PlayQueue(IAudioClient audioClient, Queue<DatabaseService.ReservedData> queue)
        {
            var guild = DatabaseService[Context.Guild.Id];
            using (var discordStream = audioClient.CreatePCMStream(AudioApplication.Music))
            {
                guild.AudioPlayer = new AudioPlayer(audioClient);
                while (queue.Count > 0)
                {
                    var reservedData = queue.Dequeue();

                    // youtube audio download
                    string query = reservedData.Uri.ToString();
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
                            .WithDescription(track.Info.Duration.ToInt().ToSecond().ToMMSS())
                            .Build());
                        await guild.AudioPlayer.StartTrackAsync(track);
                    }
                }
                guild.AudioPlayer = null;
            }
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, string message)
        {
            var target = Context.Message;

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(message);
        }

        private async Task<IUserMessage> SendAnswer(Emoji emoji, EmbedBuilder embedBuilder)
        {
            Debug.Assert(Context.Message != null, "Context.Message is null");
            var target = Context.Message;
            var e = embedBuilder.Build();

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(embed: e);
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
