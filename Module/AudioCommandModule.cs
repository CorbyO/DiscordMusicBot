using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Discord;
using Discord.Addons.Music.Common;
using Discord.Addons.Music.Player;
using Discord.Audio;
using Discord.Commands;
using Google.Apis.YouTube.v3;
using MusicBot.Extension;
using MusicBot.Services;

namespace MusicBot.Module
{
    public class AudioCommandModule : CommandModuleBase
    {
        public YouTubeService YouTubeService { get; set; }
        public DatabaseService DatabaseService { get; set; }

        [Command("검색")]
        [Alias("ㄳ", "ㄱㅅ", "ㄱ", "rjator", "rt", "r", "search")]
        public async Task SearchAsync()
        {
            await SendAnswer(Emojis.Error, "검색할 텍스트와 같이 명령해 주세요.");
        }

        [Command("검색")]
        [Alias("ㄳ","ㄱㅅ", "ㄱ", "rjator", "rt", "r", "search")]
        public async Task SearchAsync([Remainder] string text)
        {
            await Context.Message.AddReactionAsync(Emojis.Process);
            try
            {
                var guildUser = Context.User as IGuildUser ?? throw new Exception("알 수 없는 오류");
                var guild = Context.Guild;
                var resultTemp = DatabaseService[guild.Id].SearchTemp.ForceGet(guildUser.Id);
                
                _ = guildUser.VoiceChannel ?? throw new Exception("음성 채널에 들어가 있어야 합니다.");
                if (text.Length > 100) throw new Exception("검색할 텍스트는 100자 이하로 해 주세요.");

                var results = await Search(text, guild, Context.User);
                if (results.Count == 0) throw new Exception("검색 결과가 없습니다.");

                resultTemp.Fill(results);
                var builder = await ToEmbedFieldBuilder(text, results);
                resultTemp.Message = await SendAnswer(Emojis.Success, builder);
            }
            catch (Exception e)
            {
                await SendAnswer(Emojis.Error, e.Message);
            }
        }

        [Command("큐")]
        [Alias("ㅋ", "zb", "z", "Queue", "queue")]
        public async Task PrintQueue()
        {
            var queue = DatabaseService[Context.Guild.Id].Queue;
            if (queue.Count == 0)
            {
                await ReplyAsync("대기중인 노래 리스트가 없습니다.");
                return;
            }

            StringBuilder sb = new("대기중인 노래 리스트\n");

            foreach (var i in DatabaseService[Context.Guild.Id].Queue)
            {
                sb.AppendLine(i.Title);
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("1", RunMode = RunMode.Async)] public async Task Play1Async() => await PlayAsync(0);
        [Command("2", RunMode = RunMode.Async)] public async Task Play2Async() => await PlayAsync(1);
        [Command("3", RunMode = RunMode.Async)] public async Task Play3Async() => await PlayAsync(2);
        [Command("4", RunMode = RunMode.Async)] public async Task Play4Async() => await PlayAsync(3);
        [Command("5", RunMode = RunMode.Async)] public async Task Play5Async() => await PlayAsync(4);
        private async Task PlayAsync(int index)
        {
            await Context.Message.AddReactionAsync(Emojis.Process);
            try
            {
                var guildUser = (Context.User as IGuildUser) ?? throw new Exception("알 수 없는 오류");
                var authorID = guildUser.Id;
                var searchResult = DatabaseService[Context.Guild.Id].SearchTemp[authorID];
                var result = searchResult.SearchResult[index] ?? throw new Exception("먼저 '%검색' 명령어로 검색을 해 주세요.");
                var msg = searchResult.Message ?? throw new Exception("먼저 '%검색' 명령어로 검색을 해 주세요.");
                var voiceChannel = guildUser.VoiceChannel ?? throw new Exception("음성 채널에 들어가 있어야 합니다.");
                var snippet = result.Snippet;
                var title = snippet.Title;

                var uri = $"https://www.youtube.com/watch?v={result.Id.VideoId}";
                searchResult.Empty();
                var reservedData = new DatabaseService.ReservedData(new Uri(uri), title);

                var temp = SendAnswer(Emojis.Success, msg, (x) =>
                {
                    x.Embeds = new Embed[]
                    {
                        new EmbedBuilder()
                            .WithAuthor("노래 선택")
                            .WithTitle(title)
                            .WithThumbnailUrl(snippet.Thumbnails.High.Url)
                            .WithColor(Color.DarkRed)
                            .WithDescription($"[{snippet.ChannelTitle}](https://www.youtube.com/channel/{snippet.ChannelId})")
                            .WithUrl(uri)
                            .WithFooter("선택한 노래가 대기열에 추가 되었습니다.")
                            .Build()
                    };
                });

                // enqueue
                EnqueueAndPlay(reservedData, voiceChannel);
            }
            catch (Exception e)
            {
                await SendAnswer(Emojis.Error, e.Message);
            }
        }

        [Command("재생")]
        [Alias("ㅈㅅ", "ㅈ", "wotod", "wt", "w")]
        public async Task PlayURL([Remainder] string url)
        {
            await Context.Message.AddReactionAsync(Emojis.Process);
            try
            {
                var youtubeID = url?.ToYoutubeID() ?? throw new Exception("유효하지 않은 URL입니다.");
                var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel ?? throw new Exception("음성 채널에 들어가 있어야 합니다.");

                var list = YouTubeService.Videos.List("snippet");
                list.Id = youtubeID;
                var result = list.Execute().Items[0];
                var snippet = result.Snippet;
                var title = snippet.Title;
                
                var reservedData = new DatabaseService.ReservedData(new Uri(url), title);

                await SendAnswer(Emojis.Success,new EmbedBuilder()
                    .WithAuthor("노래 선택")
                    .WithTitle(title)
                    .WithThumbnailUrl(snippet.Thumbnails.Medium.Url)
                    .WithColor(Color.DarkRed)
                    .WithDescription($"[{snippet.ChannelTitle}](https://www.youtube.com/channel/{snippet.ChannelId})")
                    .WithUrl(url)
                    .WithFooter("선택한 노래가 대기열에 추가 되었습니다.")
                );

                EnqueueAndPlay(reservedData, voiceChannel);
            }
            catch (Exception e)
            {
                await SendAnswer(Emojis.Error, e.Message);
            }
        }

        [Command("스킵")]
        [Alias("ㅅ", "ㅅㅋ", "tmzlq", "t", "tz")]
        public async Task SkipQueue()
        {
            await Context.Message.AddReactionAsync(Emojis.Process);
            var audioPlayer = DatabaseService[Context.Guild.Id].AudioPlayer;
            if (audioPlayer is null)
            {
                await SendAnswer(Emojis.Error, "재생 중이 아닙니다.");
                return;
            }

            audioPlayer.Stop();
            await SendAnswer(Emojis.Success, "노래를 스킵했습니다.");
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
    }
}