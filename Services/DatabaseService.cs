using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Music.Player;
using Discord.Audio;
using Discord.WebSocket;
using Google.Apis.YouTube.v3.Data;

namespace MusicBot.Services
{
    public class DatabaseService
    {
        private readonly DiscordSocketClient _discord;

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
        public class SearchResultData
        {
            public SearchResult[] SearchResultOrNull;
            public IUserMessage Message;
            public SearchResultData()
            {
                this.SearchResultOrNull = new SearchResult[5];
                this.Message = null;
            }
            public void Empty()
            {
                for (int i = 0; i < 5; i++)
                {
                    SearchResultOrNull[i] = null;
                }
            }
            public void Fill(IList<SearchResult> searchResults) 
            {
                Debug.Assert(searchResults.Count >= 5, "Search results must be at least 5");
                for (int i = 0; i < 5; i++)
                {
                    SearchResultOrNull[i] = searchResults[i];
                }
            }
        }
        public class GuildData
        {
            public Queue<ReservedData> Queue { get; private set; } = new();
            public Dictionary<ulong, SearchResultData> SearchTemp { get; private set; } = new();
            public bool IsPlaying { get; set; } = false;
        }

        private readonly Dictionary<ulong, GuildData> _guilds;

        public DatabaseService(DiscordSocketClient discord)
        {
            _discord = discord;
            _guilds = new Dictionary<ulong, GuildData>();
            
            _discord.GuildAvailable += OnGuildAvailable;
            _discord.GuildUnavailable += OnGuildUnavailable;
        }

        private Task OnGuildAvailable(SocketGuild arg)
        {
            _guilds.Add(arg.Id, new GuildData());
            return Task.CompletedTask;
        }

        private Task OnGuildUnavailable(SocketGuild arg)
        {
            _guilds.Remove(arg.Id);
            return Task.CompletedTask;
        }

        public GuildData this[ulong guildID]
        {
            get
            {
                if (_guilds.ContainsKey(guildID))
                {
                    return _guilds[guildID];
                }
                else
                {
                    throw new KeyNotFoundException();
                }
            }
        }
    }
}