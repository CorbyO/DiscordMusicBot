using System.Text;
using Discord;
using Discord.Addons.Music.Common;
using Discord.Addons.Music.Player;
using Discord.Addons.Music.Source;
using Discord.Audio;
using Discord.Commands;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using MusicBot.Services;

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
                var voiceChannel = guildUser.VoiceChannel;
                if (guildUser.VoiceChannel == null)
                {
                    await ReplyAsync("음성 채널에 들어가 있어야 합니다.");
                    return;
                }

                // youtube search only video and song
                var result = await Search(text, Context.Guild, Context.User);

                // send message youtube search result to discord channel
                var searchResultMessage = GetSearchResultMessage(result);
                
                await ReplyAsync(searchResultMessage);
            }
        }

        [Command("1")]
        public async Task Play1Async() => await PlayAsync(0);

        [Command("2")]
        public async Task Play2Async() => await PlayAsync(1);

        [Command("3")]
        public async Task Play3Async() => await PlayAsync(2);

        [Command("4")]
        public async Task Play4Async() => await PlayAsync(3);

        [Command("5")]
        public async Task Play5Async() => await PlayAsync(4);

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
                    throw new CommandInvalidException("검색 한 적이 없습니다.");
                }

                // enqueue
                var result = searchTemp[authorID].SearchResultOrNull[index];
                guild.Queue.Enqueue(new DatabaseService.ReservedData(result, author));

                // join voice channel
                var audioClient = await voiceChannel.ConnectAsync();

                PlayQueue(audioClient, guild.Queue);
            }
        }

        private async Task<IList<Google.Apis.YouTube.v3.Data.SearchResult>> Search(string keyword, IGuild guild, IUser user)
        {
            var searchListRequest = YouTubeService.Search.List("snippet");
            searchListRequest.Q = keyword;
            searchListRequest.MaxResults = 5;
            searchListRequest.Type = "video";
            searchListRequest.VideoCategoryId = "10";

            var searchListResponse = await searchListRequest.ExecuteAsync();

            var searchTemp = DatabaseService[guild.Id].SearchTemp;
            var authorID = user.Id;
            if (!searchTemp.ContainsKey(authorID))
            {
                searchTemp.Add(authorID, new DatabaseService.SearchResultData(user));
            }

            searchTemp[authorID].Fill(searchListResponse.Items);

            return searchListResponse.Items;
        }

        private string GetSearchResultMessage(IList<Google.Apis.YouTube.v3.Data.SearchResult> searchResults)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < searchResults.Count; i++)
            {
                sb.Append($"{i + 1}. {searchResults[i].Snippet.Title}\n");
            }

            return sb.ToString();
        }

        private async void PlayQueue(IAudioClient audioClient, Queue<DatabaseService.ReservedData> queue)
        {
            using (audioClient)
            {
                do
                {
                    var reservedData = queue.Peek();

                    // youtube audio download
                    AudioPlayer audioPlayer = new AudioPlayer();
                    string query = $"https://www.youtube.com/watch?v={reservedData.SearchResult.Id.VideoId}";
                    bool wellFormedUri = Uri.IsWellFormedUriString(query, UriKind.Absolute);
                    List<AudioTrack> tracks = await TrackLoader.LoadAudioTrack(query, fromUrl: wellFormedUri);

                    // Pick the first entry and use AudioPlayer.StartTrack to play it on Thread Pool
                    AudioTrack firstTrack = tracks.ElementAt(0);

                    // OR
                    // await track to finish playing
                    await audioPlayer.StartTrackAsync(firstTrack);

                    queue.Dequeue();
                } while (queue.Count > 0);
            }
        }
    }
}
