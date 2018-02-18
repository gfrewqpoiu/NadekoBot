﻿using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System.Threading.Tasks;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Gambling.Services;
using System.Net.Http;
using Newtonsoft.Json;
using System.Linq;
using NadekoBot.Core.Common;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Modules.Gambling.Common.Events;
using System;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class CurrencyEventsCommands : NadekoSubmodule<CurrencyEventsService>
        {
            public enum OtherEvent
            {
                BotListUpvoters
            }

            private readonly DiscordSocketClient _client;
            private readonly IBotCredentials _creds;
            private readonly ICurrencyService _cs;

            public CurrencyEventsCommands(DiscordSocketClient client, ICurrencyService cs, IBotCredentials creds)
            {
                _client = client;
                _creds = creds;
                _cs = cs;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [NadekoOptions(typeof(EventOptions))]
            [OwnerOnly]
            public async Task EventStart(Event.Type ev, params string[] options)
            {
                var (opts, _) = OptionsParser.Default.ParseFrom(new EventOptions(), options);
                if (!await _service.TryCreateEventAsync(Context.Guild.Id,
                    Context.Channel.Id,
                    ev,
                    opts,
                    GetEmbed
                    ))
                {
                    await ReplyErrorLocalized("start_event_fail").ConfigureAwait(false);
                    return;
                }
            }

            private EmbedBuilder GetEmbed(Event.Type type, EventOptions opts, long currentPot)
            {

                switch (type)
                {
                    case Event.Type.Reaction:
                        return new EmbedBuilder()
                                    .WithOkColor()
                                    .WithTitle(GetText("reaction_title"))
                                    .WithDescription(GetDescription(opts.Amount, currentPot))
                                    .WithFooter(GetText("new_reaction_footer", opts.Hours));
                    default:
                        break;
                }
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            private string GetDescription(long amount, long potSize)
            {
                string potSizeStr = Format.Bold(potSize == 0
                    ? "∞"
                    : potSize.ToString() + _bc.BotConfig.CurrencySign);
                return GetText("new_reaction_event",
                                   _bc.BotConfig.CurrencySign,
                                   Format.Bold(amount + _bc.BotConfig.CurrencySign),
                                   potSize);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task EventStart(OtherEvent e)
            {
                switch (e)
                {
#if GLOBAL_NADEKO
                    case CurrencyEvent.BotListUpvoters:
                        await BotListUpvoters(arg);
                        break;
#endif
                    default:
                        await Task.CompletedTask;
                        return;
                }
            }

            private async Task BotListUpvoters(long amount)
            {
                if (amount <= 0 || string.IsNullOrWhiteSpace(_creds.BotListToken))
                    return;
                string res;
                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Add("Authorization", _creds.BotListToken);
                    res = await http.GetStringAsync($"https://discordbots.org/api/bots/116275390695079945/votes?onlyids=true");
                }
                var ids = JsonConvert.DeserializeObject<ulong[]>(res);
                await _cs.AddBulkAsync(ids, ids.Select(x => "Botlist Upvoter Event"), ids.Select(x => amount), true);
                await ReplyConfirmLocalized("bot_list_awarded",
                    Format.Bold(amount.ToString()),
                    Format.Bold(ids.Length.ToString())).ConfigureAwait(false);
            }

            //    private async Task SneakyGameStatusEvent(ICommandContext context, long num)
            //    {
            //        if (num < 10 || num > 600)
            //            num = 60;

            //        var ev = new SneakyEvent(_cs, _client, _bc, num);
            //        if (!await _service.StartSneakyEvent(ev, context.Message, context))
            //            return;
            //        try
            //        {
            //            var title = GetText("sneakygamestatus_title");
            //            var desc = GetText("sneakygamestatus_desc",
            //                Format.Bold(100.ToString()) + _bc.BotConfig.CurrencySign,
            //                Format.Bold(num.ToString()));
            //            await context.Channel.SendConfirmAsync(title, desc)
            //                .ConfigureAwait(false);
            //        }
            //        catch
            //        {
            //            // ignored
            //        }
            //    }
        }
    }
}
