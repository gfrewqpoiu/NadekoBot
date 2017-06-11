﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Modules.Games.Trivia;
using NadekoBot.Services;
using NadekoBot.Services.Database.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;


namespace NadekoBot.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class TriviaCommands : NadekoSubmodule
        {
            private readonly CurrencyService _cs;
            private readonly DiscordShardedClient _client;
            private readonly BotConfig _bc;

            public static ConcurrentDictionary<ulong, TriviaGame> RunningTrivias { get; } = new ConcurrentDictionary<ulong, TriviaGame>();

            public TriviaCommands(DiscordShardedClient client, BotConfig bc, CurrencyService cs)
            {
                _cs = cs;
                _client = client;
                _bc = bc;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task Trivia([Remainder] string additionalArgs = "")
                => InternalTrivia(10, additionalArgs);

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public Task Trivia(int winReq = 10, [Remainder] string additionalArgs = "")
                => InternalTrivia(winReq, additionalArgs);

            public async Task InternalTrivia(int winReq, string additionalArgs = "")
            {
                var channel = (ITextChannel)Context.Channel;
                var user = (IGuildUser)Context.User;

                additionalArgs = additionalArgs?.Trim()?.ToLowerInvariant();

                var showHints = !additionalArgs.Contains("nohint");
                var isPokemon = additionalArgs.Contains("pokemon");

                var trivia = new TriviaGame(_strings, _client, _bc, _cs, channel.Guild, channel, showHints, winReq, user, isPokemon);
                if (RunningTrivias.TryAdd(channel.Guild.Id, trivia))
                {
                    try
                    {
                        await trivia.StartGame().ConfigureAwait(false);
                    }
                    finally
                    {
                        RunningTrivias.TryRemove(channel.Guild.Id, out trivia);
                        await trivia.EnsureStopped().ConfigureAwait(false);
                    }
                    return;
                }

                await Context.Channel.SendErrorAsync(GetText("trivia_already_running") + "\n" + trivia.CurrentQuestion)
                    .ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Tl()
            {
                var channel = (ITextChannel)Context.Channel;

                TriviaGame trivia;
                if (RunningTrivias.TryGetValue(channel.Guild.Id, out trivia))
                {
                    await channel.SendConfirmAsync(GetText("leaderboard"), trivia.GetLeaderboard()).ConfigureAwait(false);
                    return;
                }

                await ReplyErrorLocalized("trivia_none").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Tq()
            {
                var channel = (ITextChannel)Context.Channel;
                var user = (IGuildUser)Context.User;

                TriviaGame trivia;
                if (RunningTrivias.TryGetValue(channel.Guild.Id, out trivia))
                {
                    await trivia.StopGame(user).ConfigureAwait(false);
                    return;
                }

                await ReplyErrorLocalized("trivia_none").ConfigureAwait(false);
            }
        }
    }
}