﻿using Discord;
using Discord.WebSocket;
using IA;
using IA.Events;
using IA.Events.Attributes;
using IA.SDK;
using IA.SDK.Events;
using IA.SDK.Interfaces;
using Miki.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Miki.Modules
{
    [Module("Experimental")]
    internal class DeveloperModule
    {
        public DeveloperModule(RuntimeModule module)
        {
        }

        [Command(Name = "identifyemoji", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task IdentifyEmojiAsync(EventContext e)
        {
            Emote emote = Emote.Parse(e.arguments);

            await Utils.Embed
                .SetTitle("Emoji Identified!")
                .AddInlineField("Name", emote.Name)
                .AddInlineField("Id", emote.Id.ToString())
                .AddInlineField("Created At", emote.CreatedAt.ToString())
                .AddInlineField("Code", "`" + emote.ToString() + "`")
                .SetThumbnailUrl(emote.Url)
                .SendToChannel(e.Channel);
        }

        [Command(Name = "say", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SayAsync(EventContext e)
        {
            await e.Channel.SendMessage(e.arguments);
        }

        [Command(Name = "sayembed", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SayEmbedAsync(EventContext e)
        {
            await Utils.Embed.AddInlineField("SAY", e.arguments).SendToChannel(e.Channel);
        }

        [Command(Name = "setgame", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SetGameAsync(EventContext e)
        {
            await e.message.Discord.SetGameAsync(e.arguments);
        }

        [Command(Name = "setstream", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SetStreamAsync(EventContext e)
        {
            await e.message.Discord.SetGameAsync(e.arguments, "https://www.twitch.tv/velddev");
        }

        [Command(Name = "changeavatar", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task ChangeAvatarAsync(EventContext e)
        {
            Image s = new Image(new FileStream("./" + e.arguments, FileMode.Open));

            await Bot.instance.Client.GetShard(e.message.Discord.ShardId).CurrentUser.ModifyAsync(z =>
            {
                z.Avatar = new Optional<Image?>(s);
            });
        }

        [Command(Name = "dumpshards", Accessibility = EventAccessibility.DEVELOPERONLY, Aliases = new string[] { "ds" })]
        public async Task DumpShards(EventContext context)
        {
            IDiscordEmbed embed = Utils.Embed;
            embed.Title = "Shards";

            foreach (DiscordSocketClient c in Bot.instance.Client.Shards)
            {
                embed.AddInlineField("Shard " + c.ShardId, $"State:  {c.ConnectionState}\nPing:   {c.Latency}\nGuilds: {c.Guilds.Count}");
            }

            await embed.SendToChannel(context.Channel);
        }

        [Command(Name = "spellcheck", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SpellCheckAsync(EventContext context)
        {
            IDiscordEmbed embed = Utils.Embed;

            embed.SetTitle("Spellcheck - top results");

            API.StringComparison.StringComparer sc = new API.StringComparison.StringComparer(context.commandHandler.GetAllEventNames());
            List<API.StringComparison.StringComparison> best = sc.CompareToAll(context.arguments)
                                                                 .OrderBy(z => z.score)
                                                                 .ToList();
            int x = 1;

            foreach (API.StringComparison.StringComparison c in best)
            {
                embed.AddInlineField($"#{x}", c);
                x++;
                if (x > 16) break;
            }

            await embed.SendToChannel(context.Channel);
        }

        [Command(Name = "setdonator", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SetDonator(EventContext context)
        {
            using (MikiContext database = new MikiContext())
            {
                if (context.message.MentionedUserIds.Count > 0)
                {
                    Achievement a = await database.Achievements.FindAsync(context.message.MentionedUserIds.First().ToDbLong(), "donator");
                    if (a == null)
                    {
                        database.Achievements.Add(new Achievement() { Id = context.message.MentionedUserIds.First().ToDbLong(), Name = "donator", Rank = 0 });
                        await database.SaveChangesAsync();
                    }
                }
                else
                {
                    ulong.TryParse(context.message.Content, out ulong x);
                    if (x != 0)
                    {
                        database.Achievements.Add(new Achievement() { Id = x.ToDbLong(), Name = "donator", Rank = 0 });
                        await database.SaveChangesAsync();
                    }
                }
                await context.Channel.SendMessage(":ok_hand:");
            }
        }

        [Command(Name = "setmekos", Accessibility = EventAccessibility.DEVELOPERONLY)]
        public async Task SetMekos(EventContext e)
        {
            using (var context = new MikiContext())
            {
                User u = await context.Users.FindAsync(e.message.MentionedUserIds.First().ToDbLong());
                if (u == null)
                {
                    return;
                }
                u.Currency = int.Parse(e.arguments.Split(' ')[1]);
                await context.SaveChangesAsync();
            }
        }
    }
}