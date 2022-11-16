using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace MusicBot.Module
{
    public class CommandModuleBase : ModuleBase<SocketCommandContext>
    {
        protected static class Emojis
        {    
            public static readonly Emoji Process = new("💬");
            public static readonly Emoji Error = new("⛔");
            public static readonly Emoji Success = new("✅");
        }

        protected async Task<IUserMessage> SendAnswer(Emoji emoji, string message)
        {
            var target = Context.Message;

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(message);
        }

        protected async Task<IUserMessage> SendAnswer(Emoji emoji, EmbedBuilder embedBuilder)
        {
            Debug.Assert(Context.Message != null, "Context.Message is null");
            var target = Context.Message;
            var e = embedBuilder.Build();

            _ = target.AddReactionAsync(emoji);
            return await target.ReplyAsync(embed: e);
        }

        protected async Task<IUserMessage> SendAnswer(Emoji emoji, IUserMessage userMessage, string message)
        {
            _ = Context.Message.AddReactionAsync(emoji);
            await userMessage.ModifyAsync(x => x.Content = message);
            return userMessage;
        }

        protected async Task<IUserMessage> SendAnswer(Emoji emoji, IUserMessage userMessage, Action<MessageProperties> action)
        {
            _ = Context.Message.AddReactionAsync(emoji);

            await userMessage.ModifyAsync(action);
            return userMessage;
        }
    }
}