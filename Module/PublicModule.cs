using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Discord;
using Discord.Addons.Music.Common;
using Discord.Addons.Music.Player;
using Discord.Addons.Music.Source;
using Discord.Audio;
using Discord.Commands;
using Discord.Interactions;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MusicBot.Extension;
using MusicBot.Services;
using RunMode = Discord.Commands.RunMode;

namespace MusicBot.Module
{
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        public YouTubeService YouTubeService { get; set; }
        public DatabaseService DatabaseService { get; set; }


        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");

        [Command("검색")]
        [Alias("ㄳ","ㄱㅅ", "ㄱ", "rjator", "rt", "r", "search")]
        public async Task SearchAsync([Remainder] string text)
        {
            if (text == null)
            {
                await ReplyAsync("검색할 텍스트와 같이 명령해 주세요.");
                return;
            }
            text = text.TrimStart().TrimEnd();
            if (text.Length == 0) 
            {
                await ReplyAsync("검색할 텍스트와 같이 명령해 주세요.");
                return;
            }
            if (text.Length > 100) 
            {
                await ReplyAsync("검색할 텍스트는 100자 이하로 해 주세요.");
                return;
            }
            if(Context.User is IGuildUser guildUser)
            {
                var guild = Context.Guild;
                var voiceChannel = guildUser.VoiceChannel;
                if (guildUser.VoiceChannel == null)
                {
                    await ReplyAsync("음성 채널에 들어가 있어야 합니다.");
                    return;
                }

                // youtube search only video and song
                var results = await Search(text, guild, Context.User);
                
                var searchTemp = DatabaseService[guild.Id].SearchTemp;
                var authorID = guildUser.Id;
                if (!searchTemp.ContainsKey(authorID))
                {
                    searchTemp.Add(authorID, new DatabaseService.SearchResultData());
                }

                searchTemp[authorID].Fill(results);

                // send message youtube search result to discord channel
                var searchResultMessage = GetSearchResultMessage(results);

                searchTemp[authorID].Message = await Context.Message.ReplyAsync(await searchResultMessage);
            }
        }

        [Command("1", RunMode = RunMode.Async)]
        public Task Play1Async() => PlayAsync(0);

        [Command("2", RunMode = RunMode.Async)]
        public Task Play2Async() => PlayAsync(1);
        
        [Command("3", RunMode = RunMode.Async)]
        public Task Play3Async() => PlayAsync(2);

        [Command("4", RunMode = RunMode.Async)]
        public Task Play4Async() => PlayAsync(3);

        [Command("5", RunMode = RunMode.Async)]
        public Task Play5Async() => PlayAsync(4);
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

        private async Task PlayAsync(int index)
        {
            if(Context.User is IGuildUser guildUser)
            {
                var voiceChannel = guildUser.VoiceChannel;
                if (guildUser.VoiceChannel == null)
                {
                    await ReplyAsync("음성 채널에 들어가 있어야 합니다.");
                    return;
                }

                var guild = DatabaseService[Context.Guild.Id];
                // check for search result
                var searchTemp = guild.SearchTemp;
                var author = Context.User;
                var authorID = author.Id;

                if (!searchTemp.ContainsKey(authorID))
                {
                    await ReplyAsync("먼저 '%검색' 명령어로 검색을 해 주세요.");
                    return;
                }

                // enqueue
                var searchResult = searchTemp[authorID];
                var result = searchResult.SearchResultOrNull[index];

                if (result == null)
                {
                    await ReplyAsync("먼저 '%검색' 명령어로 검색을 해 주세요.");
                    return;
                }

                await searchResult.Message.ModifyAsync(x => x.Content = $"대기 목록에 추가: '{result.Snippet.Title}'");

                guild.Queue.Enqueue(new DatabaseService.ReservedData(result, author));

                if(!guild.IsPlaying)
                {
                    guild.IsPlaying = true;
                    var audioClient = await voiceChannel.ConnectAsync();
                    PlayQueue(audioClient, guild.Queue);
                }
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
            searchListRequest.VideoCaption = SearchResource.ListRequest.VideoCaptionEnum.ClosedCaption;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
            searchListRequest.VideoCategoryId = "10";

            var searchListResponse = await searchListRequest.ExecuteAsync();

            return searchListResponse.Items;
        }

        private async Task<string> GetSearchResultMessage(IList<Google.Apis.YouTube.v3.Data.SearchResult> searchResults)
        {
            var sb = new StringBuilder("**검색 결과**\n");
            var count = searchResults.Count;
            var ids = new string[count];
            var list = YouTubeService.Videos.List("contentDetails");
            
            for (int i = 0; i < count; i++)
            {
                ids[i] = searchResults[i].Id.VideoId;
            }

            list.Id = ids;

            var listResult = await list.ExecuteAsync();

            for (int i = 0; i < count; i++)
            {
                var result = listResult.Items[i];
                var time = result.ContentDetails.Duration
                    .Replace("PT", null)
                    .Replace("H", "시 ")
                    .Replace("M", "분 ")
                    .Replace("S", "초");

                var title = searchResults[i].Snippet.Title;
                sb.Append($"> {ToEmoji(i + 1)} `{title.Omit(40, Encoding.ASCII)}` `{time}`\n");
            }

            return sb.ToString();
        }

        private string ToEmoji(int number)
        {
            switch(number)
            {
                case 0: return ":zero:";
                case 1: return ":one:";
                case 2: return ":two:";
                case 3: return ":three:";
                case 4: return ":four:";
                case 5: return ":five:";
                case 6: return ":six:";
                case 7: return ":seven:";
                    case 8: return ":eight:";
                case 9: return ":nine:";
                case 10: return ":ten:";
                default: return number.ToString();
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
                        await ReplyAsync($"노래를 실행 합니다: {track.Info.Title}");
                        await player.StartTrackAsync(track);
                    }
                }
            }
            DatabaseService[Context.Guild.Id].IsPlaying = false;
        }
    }
}
