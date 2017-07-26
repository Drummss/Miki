﻿using Discord;
using IA;
using IA.Events;
using IA.Events.Attributes;
using IA.Node;
using IA.SDK;
using IA.SDK.Events;
using IA.SDK.Interfaces;
using Miki.Accounts;
using Miki.Accounts.Achievements;
using Miki.Languages;
using Miki.Models;
using Miki.Objects;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Miki.Modules
{
    [Module("pasta")]
    public class PastaModule
    {
        [Command(Name = "poppasta")]
        public async Task DoPastaLeaderboardsPopular(EventContext context)
        {
            using (var d = new MikiContext())
            {
                List<LeaderboardsItem> leaderboards = d.Database.SqlQuery<LeaderboardsItem>("select TOP 12 Id as Name, TimesUsed as Value from dbo.GlobalPastas as c order by Value DESC").ToList();

                IDiscordEmbed e = Utils.Embed
                    .SetTitle(context.GetResource("poppasta_title"))
                    .SetColor(new IA.SDK.Color(1, 0.6f, 0.2f));

                foreach (LeaderboardsItem t in leaderboards)
                {
                    e.AddInlineField(t.Name, (t == leaderboards.First() ? "👑 " + t.Value.ToString() : "✨ " + t.Value.ToString()));
                }

                await e.SendToChannel(context.Channel.Id);
            }
        }

        [Command(Name = "toppasta")]
        public async Task DoPastaLeaderboardsLove(EventContext context)
        {
            using (var d = new MikiContext())
            {
                List<LeaderboardsItem> leaderboards = d.Database.SqlQuery<LeaderboardsItem>("select TOP 12 Id as Name, ((SELECT Count(*) from Votes where Id = c.Id AND PositiveVote = 1) - (SELECT Count(*) from Votes where Id = c.Id AND PositiveVote = 0)) as Value from dbo.GlobalPastas as c order by Value DESC").ToList();

                IDiscordEmbed e = Utils.Embed
                    .SetTitle(context.GetResource("toppasta_title"))
                    .SetColor(new IA.SDK.Color(1, 0, 0));
                
                foreach(LeaderboardsItem t in leaderboards)
                {
                    e.AddInlineField(t.Name, (t == leaderboards.First() ? "💖 " + t.Value.ToString() : (t.Value < 0 ? "💔 " : "❤ ") + t.Value.ToString()));
                }

                await e.SendToChannel(context.Channel.Id);
            }
        }

        [Command(Name = "mypasta")]
        public async Task MyPasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            int page = 0;
            if (!string.IsNullOrWhiteSpace(e.arguments))
            {
                if (int.TryParse(e.arguments, out page))
                {
                    page -= 1;
                }
            }

            long authorId = e.Author.Id.ToDbLong();

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                var pastasFound = context.Database.SqlQuery<PastaSearchResult>("select [GlobalPastas].id, count(*) OVER() AS total_count from [GlobalPastas] where CreatorID = @p0 ORDER BY id OFFSET @p1 ROWS FETCH NEXT 25 ROWS ONLY;",
                    authorId, page * 25).ToList();

                if (pastasFound?.Count > 0)
                {
                    string resultString = "";

                    pastasFound.ForEach(x => { resultString += "`" + x.Id + "` "; });

                    IDiscordEmbed embed = Utils.Embed;
                    embed.Title = e.GetResource("mypasta_title", e.Author.Username);
                    embed.Description = resultString;
                    embed.CreateFooter();
                    embed.Footer.Text = e.GetResource("pasta_page_index", page + 1, (Math.Ceiling((double)pastasFound[0].Total_Count / 25)).ToString());

                    await embed.SendToChannel(e.Channel);
                    return;
                }

                await Utils.ErrorEmbed(locale, e.GetResource("mypasta_error_no_pastas"))
                    .SendToChannel(e.Channel);
            }
        }

        [Command(Name = "createpasta")]
        public async Task CreatePasta(EventContext e)
        {
            List<string> arguments = e.arguments.Split(' ').ToList();

            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if(arguments.Count < 2)
            {
                await Utils.ErrorEmbed(locale, e.GetResource("createpasta_error_no_content")).SendToChannel(e.Channel.Id);
                return;
            }

            string id = arguments[0];
            arguments.RemoveAt(0);

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                try
                {
                    GlobalPasta pasta = await context.Pastas.FindAsync(id);

                    if (pasta != null)
                    {
                        await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_create_error_already_exist")).SendToChannel(e.Channel);
                        return;
                    }

                    context.Pastas.Add(new GlobalPasta() { Id = id, Text = string.Join(" ", arguments), CreatorId = e.Author.Id, date_created = DateTime.Now });
                    await context.SaveChangesAsync();
                    await Utils.SuccessEmbed(locale, e.GetResource("miki_module_pasta_create_success", id)).SendToChannel(e.Channel);
                }
                catch (Exception ex)
                {
                    Log.ErrorAt("IdentifyPasta", ex.Message);
                }
            }
        }

        [Command(Name = "deletepasta")]
        public async Task DeletePasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if (string.IsNullOrWhiteSpace(e.arguments))
            {
                await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_specify", e.GetResource("miki_module_pasta_error_specify")))
                    .SendToChannel(e.Channel.Id);
                return;
            }

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                GlobalPasta pasta = await context.Pastas.FindAsync(e.arguments);

                if(pasta == null)
                {
                    await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_null")).SendToChannel(e.Channel);
                    return;
                }

                if(pasta.CanDeletePasta(e.Author.Id))
                {
                    context.Pastas.Remove(pasta);

                    List<PastaVote> votes = context.Votes.AsNoTracking().Where(p => p.Id.Equals(e.arguments)).ToList();
                    context.Votes.RemoveRange(votes);

                    await context.SaveChangesAsync();

                    await Utils.SuccessEmbed(locale, e.GetResource("miki_module_pasta_delete_success", e.arguments)).SendToChannel(e.Channel);
                    return;
                }
                await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_no_permissions", e.GetResource("miki_module_pasta_error_specify_delete"))).SendToChannel(e.Channel);
                return;
            }
        }

        [Command(Name = "editpasta")]
        public async Task EditPasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if (string.IsNullOrWhiteSpace(e.arguments))
            {
                await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_specify", e.GetResource("miki_module_pasta_error_specify_edit")))
                    .SendToChannel(e.Channel.Id);
                return;
            }

            if(e.arguments.Split(' ').Length == 1)
            {
                await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_specify", e.GetResource("miki_module_pasta_error_specify_edit")))
                    .SendToChannel(e.Channel.Id);
                return;
            }

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                string tag = e.arguments.Split(' ')[0];
                e.arguments = e.arguments.Substring(tag.Length + 1);

                GlobalPasta p = await context.Pastas.FindAsync(tag);

                if (p.CreatorId == e.Author.Id || Bot.instance.Events.Developers.Contains(e.Author.Id))
                {
                    p.Text = e.arguments;
                    await context.SaveChangesAsync();
                    await e.Channel.SendMessage($"Edited `{tag}`!");
                }
                else
                {
                    await e.Channel.SendMessage($@"You cannot edit pastas you did not create. Baka!");
                }
            }
        }

        [Command(Name = "pasta")]
        public async Task GetPasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if (string.IsNullOrWhiteSpace(e.arguments))
            {
                await Utils.ErrorEmbed(locale, e.GetResource("pasta_error_no_arg")).SendToChannel(e.Channel);
                return;
            }

            List<string> arguments = e.arguments.Split(' ').ToList();
            
            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                GlobalPasta pasta = await context.Pastas.FindAsync(arguments[0]);
                if (pasta == null)
                {
                    await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_search_error_no_results", e.arguments)).SendToChannel(e.Channel);
                    return;
                }
                pasta.TimesUsed++;
                await e.Channel.SendMessage(pasta.Text);
                await context.SaveChangesAsync();
            }
        }

        [Command(Name = "infopasta")]
        public async Task IdentifyPasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if (string.IsNullOrWhiteSpace(e.arguments))
            {
                await Utils.ErrorEmbed(locale, e.GetResource("infopasta_error_no_arg"))
                    .SendToChannel(e.Channel.Id);
                return;
            }

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                try
                {
                    GlobalPasta pasta = await context.Pastas.FindAsync(e.arguments);

                    if(pasta == null)
                    {
                        await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_null")).SendToChannel(e.Channel);
                        return;
                    }

                    User creator = await context.Users.FindAsync(pasta.creator_id);

                    IDiscordEmbed b = Utils.Embed;

                    b.SetAuthor(pasta.Id.ToUpper(), "", "");
                    b.Color = new IA.SDK.Color(47, 208, 192);
                   
                    if (creator != null)
                    {
                        b.AddInlineField(e.GetResource("miki_module_pasta_identify_created_by"), $"{ creator.Name} [{creator.Id}]");
                    }

                    b.AddInlineField(e.GetResource("miki_module_pasta_identify_date_created"), pasta.date_created.ToShortDateString());

                    b.AddInlineField(e.GetResource("miki_module_pasta_identify_times_used"), pasta.TimesUsed.ToString());

                    VoteCount v = pasta.GetVotes(context);

                    b.AddInlineField(e.GetResource("infopasta_rating"), $"⬆️ { v.Upvotes} ⬇️ {v.Downvotes}");

                    await b.SendToChannel(e.Channel);

                }
                catch (Exception ex)
                {
                    Log.ErrorAt("IdentifyPasta", ex.Message);
                }
            }
        }

        [Command(Name = "searchpasta")]
        public async Task SearchPasta(EventContext e)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            if(string.IsNullOrWhiteSpace(e.arguments))
            {
                await Utils.ErrorEmbed(locale, e.GetResource("searchpasta_error_no_arg"))
                    .SendToChannel(e.Channel.Id);
                return;
            }

            List<string> arguments = e.arguments.Split(' ').ToList();
            int page = 0;         

            if (arguments.Count > 1)
            {
                if (int.TryParse(arguments[arguments.Count - 1], out page))
                {
                    page -= 1;
                }
            }

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();
                var pastasFound = context.Database.SqlQuery<PastaSearchResult>("select [GlobalPastas].Id, count(*) OVER() AS total_count from [GlobalPastas] where Id like @p0 ORDER BY id OFFSET @p1 ROWS FETCH NEXT 25 ROWS ONLY;",
                    "%" + arguments[0] + "%", page * 25).ToList();

                if (pastasFound?.Count > 0)
                {
                    string resultString = "";
                        
                    pastasFound.ForEach(x => { resultString += "`" + x.Id + "` "; });

                    IDiscordEmbed embed = Utils.Embed;
                    embed.Title = e.GetResource("miki_module_pasta_search_header");
                    embed.Description = resultString;
                    embed.CreateFooter();
                    embed.Footer.Text = e.GetResource("pasta_page_index", page + 1, (Math.Ceiling((double)pastasFound[0].Total_Count / 25)).ToString());

                    await embed.SendToChannel(e.Channel);
                    return;
                }

                await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_search_error_no_results", arguments[0]))
                    .SendToChannel(e.Channel);
            }
        }

        [Command(Name = "lovepasta")]
        public async Task LovePasta(EventContext e)
        {
            await VotePasta(e, true);
        }

        [Command(Name = "hatepasta")]
        public async Task HatePasta(EventContext e)
        {
            await VotePasta(e, false);
        }

        private async Task VotePasta(EventContext e, bool vote)
        {
            Locale locale = Locale.GetEntity(e.Channel.Id.ToDbLong());

            using (var context = MikiContext.CreateNoCache())
            {
                context.Set<GlobalPasta>().AsNoTracking();

                var pasta = await context.Pastas.FindAsync(e.arguments);

                if(pasta == null)
                {
                    await Utils.ErrorEmbed(locale, e.GetResource("miki_module_pasta_error_null")).SendToChannel(e.Channel);
                    return;
                }

                long authorId = e.Author.Id.ToDbLong();

                var voteObject = context.Votes.Where(q => q.Id == e.arguments && q.__UserId == authorId).FirstOrDefault();

                if (voteObject == null)
                {
                    voteObject = new PastaVote() { Id = e.arguments, __UserId = e.Author.Id.ToDbLong(), PositiveVote = vote };
                    context.Votes.Add(voteObject);
                }
                else
                {
                    voteObject.PositiveVote = vote;
                }
                await context.SaveChangesAsync();

                var votecount = pasta.GetVotes(context);

                await Utils.SuccessEmbed(locale, e.GetResource("miki_module_pasta_vote_success", votecount.Upvotes - votecount.Downvotes)).SendToChannel(e.Channel);
            }
        }
    }
}