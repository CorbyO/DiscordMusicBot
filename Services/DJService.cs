using Discord.Addons.Music.Common;
using Discord;
using Discord.Addons.Music.Source;
using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using static MusicBot.Services.DatabaseService;
using Discord.WebSocket;

namespace MusicBot.Services
{
    public class DJService
    {
        public DatabaseService _databaseService;
        public bool CanPlay(ulong guildID)
        {
            var guild = _databaseService[guildID];
            var queue = guild.Queue;
            
            if (guild.AudioPlayer.AudioClient == null) return false;
            if (queue.Count >= 0) return false;

            return true;
        }
        public async Task Play(ulong guildID)
        {
            var guild = _databaseService[guildID];
            var queue = guild.Queue;
            
            while (queue.Count > 0)
            {
                var track = queue.Dequeue();
                await guild.AudioPlayer.StartTrackAsync(track);
            }
        }

        public async void Join(ulong guildID, IAudioChannel to)
        {
            var guild = _databaseService[guildID];
            
            if (guild.AudioPlayer.AudioClient != to)
            {
                var temp = await to.ConnectAsync();
                guild.AudioPlayer.SetAudioClient(temp);
            }
        }

        public async Task Add(ulong guildID, Uri uri)
        {
            string query = uri.ToString();
            bool wellFormedUri = Uri.IsWellFormedUriString(query, UriKind.Absolute);
            var tracks = await TrackLoader.LoadAudioTrack(query, fromUrl: wellFormedUri);

            foreach (var track in tracks)
            {
                _databaseService[guildID].Queue.Enqueue(track);
            }
        }
    }
}
