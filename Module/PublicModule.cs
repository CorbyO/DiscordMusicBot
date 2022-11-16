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
    public class PublicModule : CommandModuleBase
    {
        [Command("도움말")]
        [Alias("ㄷㅇㅁ", "ㄷ", "ehdnaakf", "eda", "e", "?")]
        public async Task PrintHelp()
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
            await SendAnswer(Emojis.Success, new EmbedBuilder()
                .WithColor(Color.Orange)
                .WithTitle("도움말")
                .WithDescription("명령어의 목록 입니다.")
                .WithFields(embed));
        }

        [Command("ping")]
        [Alias("pong", "hello")]
        public async Task PingAsync()
        {
            await SendAnswer(Emojis.Success, "Pong");
        }
    }
}
