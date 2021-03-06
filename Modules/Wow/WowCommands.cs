using NinjaBotCore.Database;
using NinjaBotCore.Models.Wow;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Net;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NinjaBotCore.Services;

namespace NinjaBotCore.Modules.Wow
{
    public class WowCommands : ModuleBase
    {
        private ChannelCheck _cc;
        private WarcraftLogs _logsApi;
        private WowApi _wowApi;
        private DiscordSocketClient _client;
        private RaiderIOApi _rioApi;
        private readonly IConfigurationRoot _config;
        private string _prefix;

        public WowCommands(WowApi api, ChannelCheck cc, WarcraftLogs logsApi, RaiderIOApi rioApi, DiscordSocketClient client, IConfigurationRoot config)
        {
            _cc = cc;            
            _logsApi = logsApi;            
            _wowApi = api;
            _rioApi = rioApi;                                    
            _client = client;            
            _config = config;
            _prefix = _config["prefix"];
        }

        [Command("shunt", RunMode = RunMode.Async)]
        public async Task ShowShunt()
        {
            await Context.Channel.SendFileAsync("shunt.gif");
        }

        [Command("rpi", RunMode = RunMode.Async)]
        public async Task GetMythicPlus([Remainder] string args = null)
        {
            var sb = new StringBuilder();
            var embed = new EmbedBuilder();
            GuildChar charInfo = null;
            if (string.IsNullOrEmpty(args))
            {
                embed.Title = $"M+ Character Info Help";
                sb.AppendLine($"Usage examples:");
                sb.AppendLine($":black_small_square: **{_prefix}rpi** charactername");
                sb.AppendLine($"\t:black_small_square: Search raider.io for *charactername* (first in guild, then in the rest of WoW(US))");
                sb.AppendLine($":black_small_square: **{_prefix}rpi** charactername realmname");
                sb.AppendLine($"\t:black_small_square: Search raider.io for *charactername* on *realmname* WoW (US)");
                sb.AppendLine($":black_small_square: **{_prefix}rpi** charactername realmname region(us or eu)");
                sb.AppendLine($"\t:black_small_square: Search raider.io for *charactername* on *realmname* WoW (US or EU as specified)");
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
                return;
            }
            else
            {
                RaiderIOModels.RioMythicPlusChar mPlusInfo = null;
                charInfo = await GetCharFromArgs(args, Context);
                Character armoryInfo = null;
                string realmSlug = string.Empty;
                string region = string.Empty;
                if (!(string.IsNullOrEmpty(charInfo.regionName)))
                {
                    mPlusInfo = _rioApi.GetCharMythicPlusInfo(charName: charInfo.charName, realmName: charInfo.realmName.Replace(" ","%20"), region: charInfo.regionName.ToLower());
                    armoryInfo = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName, charInfo.regionName);
                }
                else
                {
                    mPlusInfo = _rioApi.GetCharMythicPlusInfo(charName: charInfo.charName, realmName: charInfo.realmName.Replace(" ","%20"));
                    armoryInfo = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName);
                }                
                switch (charInfo.locale.Substring(3).ToLower())
                {
                    case "us":
                        {
                            region = "us";
                            realmSlug = WowApi.RealmInfo.realms.Where(r => r.name.Replace("'","").ToLower().Contains(charInfo.realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                            break;
                        }
                    case "ru":
                        {                 
                            region = "ru";   
                            realmSlug = WowApi.RealmInfoRu.realms.Where(r => r.name.Replace("'","").ToLower().Contains(charInfo.realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                            break;
                        }
                    case "gb":
                        {
                            region = "eu";
                            realmSlug = WowApi.RealmInfoEu.realms.Where(r => r.name.Replace("'","").ToLower().Contains(charInfo.realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                            break;
                        }
                    default:
                        {
                            realmSlug = WowApi.RealmInfo.realms.Where(r => r.name.Replace("'","").ToLower().Contains(charInfo.realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                            break;
                        }
                }               
                sb.AppendLine($"**__Raid Progression__**");
                string normalKilled = GetNumberEmojiFromString((int)mPlusInfo.RaidProgression.Uldir.NormalBossesKilled);
                string heroicKilled = GetNumberEmojiFromString((int)mPlusInfo.RaidProgression.Uldir.HeroicBossesKilled);
                string mythicKilled = GetNumberEmojiFromString((int)mPlusInfo.RaidProgression.Uldir.MythicBossesKilled);
                string totalBosses = GetNumberEmojiFromString((int)mPlusInfo.RaidProgression.Uldir.TotalBosses);                                
                sb.AppendLine($"\t **normal** [{normalKilled} / {totalBosses}] **heroic** [{heroicKilled} / {totalBosses}] **mythic** [{mythicKilled} / {totalBosses}]");
                sb.AppendLine();
                sb.AppendLine($"**__Best Runs__**");                
                foreach (var run in mPlusInfo.MythicPlusBestRuns)
                {
                    sb.AppendLine($"\t [:white_square_button: [{run.ShortName}(**{run.MythicLevel}**)] {run.ClearTimeMs / 60000} minutes]({run.Url.AbsoluteUri})");
                } 
                sb.AppendLine();                                
                sb.AppendLine($"**__M+ Rankings For Active Spec ({mPlusInfo.ActiveSpecRole})__**");
                switch (mPlusInfo.ActiveSpecRole.ToLower())
                {
                    case "dps":
                    {
                        sb.AppendLine($"\t Realm [{mPlusInfo.MythicPlusRanks.Dps.Realm}] Region [{mPlusInfo.MythicPlusRanks.Dps.Region}] World [{mPlusInfo.MythicPlusRanks.Dps.World}]");
                        break;
                    }
                    case "healer":
                    {
                        sb.AppendLine($"\t Realm [{mPlusInfo.MythicPlusRanks.Healer.Realm}] Region [{mPlusInfo.MythicPlusRanks.Healer.Region}] World [{mPlusInfo.MythicPlusRanks.Healer.World}]");
                        break;
                    }
                    case "tank":
                    {
                        sb.AppendLine($"\t Realm [{mPlusInfo.MythicPlusRanks.Tank.Realm}] Region [{mPlusInfo.MythicPlusRanks.Tank.Region}] World [{mPlusInfo.MythicPlusRanks.Tank.World}]");
                        break;
                    }
                }
                sb.AppendLine();          
                embed.Title = $"Mythic+ Information For {mPlusInfo.Name} on {mPlusInfo.Realm}";
                embed.AddField("Raider.IO",$"[{mPlusInfo.Name}]({mPlusInfo.ProfileUrl.AbsoluteUri})", true);
                embed.AddField("WoW Armory",$"[{mPlusInfo.Name}]({armoryInfo.armoryURL})", true);
                embed.AddField("Warcraftlogs",$"[{mPlusInfo.Name}](https://www.warcraftlogs.com/character/{region}/{realmSlug}/{mPlusInfo.Name})", true);
                embed.ThumbnailUrl = $"{mPlusInfo.ThumbnailUrl.AbsoluteUri}";
                embed.Description = sb.ToString();
                embed.WithColor(new Color(0, 200, 150));
                embed.Footer = new EmbedFooterBuilder{
                    Text = $"Raider.IO Score {mPlusInfo.MythicPlusScores.All} ilvl {armoryInfo.items.averageItemLevel}({armoryInfo.items.averageItemLevelEquipped} equipped)"
                };                            
                await _cc.Reply(Context, embed);
            }
        }

        [Command("guildstats", RunMode = RunMode.Async)]
        public async Task GetRioGuildStats()
        {
            var embed = new EmbedBuilder();
            var sb = new StringBuilder();
            var guildInfo = Context.Guild;
            
            string title = string.Empty;            
            string discordGuildName = string.Empty;
            string thumbUrl = string.Empty;
            string region = string.Empty;

            if (guildInfo == null)
            {
                discordGuildName = Context.User.Username;
                thumbUrl = Context.User.GetAvatarUrl();
            }
            else
            {
                discordGuildName = Context.Guild.Name;
                thumbUrl = Context.Guild.IconUrl;
            }

            var guildObject = await GetGuildName(); 
            var guildStats = _rioApi.GetRioGuildInfo(guildName: guildObject.guildName, realmName: guildObject.realmSlug, region: guildObject.regionName);
                        
            string normalKilled = GetNumberEmojiFromString((int)guildStats.RaidProgression.Uldir.NormalBossesKilled);
            string heroicKilled = GetNumberEmojiFromString((int)guildStats.RaidProgression.Uldir.HeroicBossesKilled);
            string mythicKilled = GetNumberEmojiFromString((int)guildStats.RaidProgression.Uldir.MythicBossesKilled);
            string totalBosses = GetNumberEmojiFromString((int)guildStats.RaidProgression.Uldir.TotalBosses);
            
            title = $"{guildObject.guildName} on {guildObject.realmName}'s Raider.IO Stats";

            sb.AppendLine("**__Raid Progression:__**");
            sb.AppendLine($"\t **normal** [{normalKilled} / {totalBosses}]");
            sb.AppendLine($"\t **heroic** [{heroicKilled} / {totalBosses}]");
            sb.AppendLine($"\t **mythic** [{mythicKilled} / {totalBosses}]");
            sb.AppendLine();
            sb.AppendLine("**__Raid Rankings:__**");
            sb.AppendLine($"\t **normal** [ realm [**{guildStats.RaidRankings.Uldir.Normal.Realm}**] world [**{guildStats.RaidRankings.Uldir.Normal.World}**] region [**{guildStats.RaidRankings.Uldir.Normal.Region}**] ]");            
            sb.AppendLine($"\t **heroic** [ realm [**{guildStats.RaidRankings.Uldir.Heroic.Realm}**] world [**{guildStats.RaidRankings.Uldir.Heroic.World}**] region [**{guildStats.RaidRankings.Uldir.Heroic.Region}**] ]");
            sb.AppendLine($"\t **mythic** [ realm [**{guildStats.RaidRankings.Uldir.Mythic.Realm}**] world [**{guildStats.RaidRankings.Uldir.Mythic.World}**] region [**{guildStats.RaidRankings.Uldir.Mythic.Region}**] ]");
            sb.AppendLine();
            sb.AppendLine($"[{guildObject.guildName} Profile]({guildStats.ProfileUrl.AbsoluteUri})");

            embed.Title = title;
            embed.ThumbnailUrl = thumbUrl;
            embed.WithColor(new Color(0, 0, 255));
            embed.Description = sb.ToString();

            await _cc.Reply(Context, embed);                        
        }

        [Command("affixes", RunMode = RunMode.Async)]
        public async Task GetAffixes()
        {
            var embed = new EmbedBuilder();
            var sb = new StringBuilder();
            var guildInfo = Context.Guild;
            
            string title = string.Empty;            
            string discordGuildName = string.Empty;
            string thumbUrl = string.Empty;
            string region = string.Empty;

            if (guildInfo == null)
            {
                discordGuildName = Context.User.Username;
                thumbUrl = Context.User.GetAvatarUrl();
            }
            else
            {
                discordGuildName = Context.Guild.Name;
                thumbUrl = Context.Guild.IconUrl;
            }

            var guildObject = await GetGuildName(); 
            RaiderIOModels.Affix affixes = null;
            
            switch (guildObject.regionName.ToLower())
            {
                case "us":
                {
                    region = "us";
                    break;
                }
                case "eu":
                {
                    region = "eu";
                    break;
                }
                default:
                {
                    region = "us";
                    break;
                }
            }

            affixes = _rioApi.GetCurrentAffix(region: region);

            title = $"Current M+ Affixes ({region})";
           
            embed.Title = title;
            embed.ThumbnailUrl = thumbUrl;
            embed.WithColor(new Color(0, 255, 0));
            string affixLevel = string.Empty;
            foreach (var detail in affixes.AffixDetails)
            {
                switch (detail.Name.ToLower())
                {
                    case "fortified":
                    {
                        affixLevel = "2";
                        break;
                    }
                    case "tyrannical":
                    {
                        affixLevel = "2";
                        break;
                    }
                    case "teeming":
                    {
                        affixLevel = "4";
                        break;
                    }   
                    case "raging":
                    {
                        affixLevel = "4";
                        break;
                    } 
                    case "bursting":
                    {
                        affixLevel = "4";
                        break;
                    } 
                    case "sanguine":
                    {
                        affixLevel = "4";
                        break;
                    } 
                    case "necrotic":
                    {
                        affixLevel = "7";
                        break;
                    }      
                    case "skittish":
                    {
                        affixLevel = "7";
                        break;
                    }     
                    case "volcanic":
                    {
                        affixLevel = "7";
                        break;
                    } 
                    case "explosive":
                    {
                        affixLevel = "7";
                        break;
                    }                                                                           
                    case "quaking":
                    {
                        affixLevel = "7";
                        break;
                    }    
                    case "grievous":
                    {
                        affixLevel = "7";
                        break;
                    }
                    case "infested":
                    {
                        affixLevel = "10";
                        break;
                    }                                             
                }
                sb.AppendLine($"({affixLevel})[{detail.Name}]({detail.WowheadUrl})");
                sb.AppendLine($"\t*{detail.Description}*");
                sb.AppendLine();
            }

            sb.AppendLine($"[Leaderboard]({affixes.LeaderboardUrl.AbsoluteUri})");            
            embed.Description = sb.ToString();

            await _cc.Reply(Context, embed);                        
        }

        [Command("noggen", RunMode = RunMode.Async)]
        [RequireOwner]
        public async Task GetNoggen([Remainder] string args)
        {
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            string title = string.Empty;
            GuildMembers members = null;
            string thumbUrl = string.Empty;
            var guildInfo = Context.Guild;
            string discordGuildName = string.Empty;

            if (guildInfo == null)
            {
                discordGuildName = Context.User.Username;
                thumbUrl = Context.User.GetAvatarUrl();
            }
            else
            {
                discordGuildName = Context.Guild.Name;
                thumbUrl = Context.Guild.IconUrl;
            }

            NinjaObjects.GuildObject guildObject = await GetGuildName();

            title = $"Top Noggenfogger Consumers in **{guildObject.guildName}**";
            embed.Title = title;
            embed.ThumbnailUrl = thumbUrl;

            if (guildObject.guildName != null || members != null)
            {
                try
                {
                    if (args == "clear")
                    {
                        List<CharStats> statsFromDb = new List<CharStats>();

                        using (var db = new NinjaBotEntities())
                        {
                            statsFromDb = db.CharStats.Where(c => c.GuildName == guildObject.guildName).ToList();                                                                        
                        }
                        if (statsFromDb != null)
                        {
                            using (var db = new NinjaBotEntities())
                            {
                                var people = db.CharStats.Where(c => c.GuildName == guildObject.guildName).ToList();
                                foreach (var person in people)
                                {
                                    db.CharStats.Remove(person);
                                }                                
                                await db.SaveChangesAsync();
                            }                            
                        }
                    }
                    else 
                    {

                    if (!string.IsNullOrEmpty(guildObject.locale))
                    {
                        members = _wowApi.GetGuildMembers(guildObject.realmName, guildObject.guildName, locale: guildObject.locale, regionName: guildObject.regionName);
                    }
                    else
                    {
                        members = _wowApi.GetGuildMembers(guildObject.realmName, guildObject.guildName, regionName: guildObject.regionName);
                    }     

                    var maxLevelChars = members.members.Where(m => m.character.level == 120);               
                    List<CharStats> statsFromDb = new List<CharStats>();

                    using (var db = new NinjaBotEntities())
                    {
                        statsFromDb = db.CharStats.Where(c => c.GuildName == guildObject.guildName).ToList();                                                                        
                    }

                    foreach (var member in maxLevelChars)
                    {
                        var curMemberStats = new WowStats();
                        var match = statsFromDb.Where(s => s.CharName == member.character.name).FirstOrDefault();                                                
                        if (match == null) 
                        {
                            curMemberStats = _wowApi.GetCharStats(member.character.name, member.character.realm, guildObject.locale);
                            var elixer = curMemberStats.Statistics.SubCategories[0].SubCategories[0].Statistics.Where(s => s.Name == "Elixir consumed most").FirstOrDefault();
                            using (var db = new NinjaBotEntities())
                            {
                                db.CharStats.Add(new CharStats{
                                    CharName = member.character.name,
                                    RealmName = member.character.realm,
                                    GuildName = guildObject.guildName,
                                    ElixerConsumed = elixer.Highest,
                                    Quantity = elixer.Quantity                                    
                                });
                                await db.SaveChangesAsync();                                
                            }
                        }
                        else
                        {

                        }
                    }                  
                    if (statsFromDb.Count() > 0)
                    {
                        var candidates = statsFromDb.OrderByDescending(o => o.Quantity).Take(10).ToList();
                        if (candidates != null && candidates.Count() > 0)
                        {
                            foreach (var canditate in candidates)
                            {
                                sb.AppendLine($":black_medium_small_square: {canditate.CharName} -> *{canditate.Quantity}*");
                            }                            
                        }
                    }
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                    }                   
                }                
                catch (Exception ex)
                {
                    Console.WriteLine($"Get-Guild Error getting guild info: [{ex.Message}]");
                }
            }
        }
        
        [Command("Populate-Logs",RunMode = RunMode.Async)]
        [RequireOwner]
        public async Task PopulateLogs()
        {
            {
                List<WowGuildAssociations> guildList = null;
                List<LogMonitoring> logWatchList = null;
                try
                {

                    using (var db = new NinjaBotEntities())
                    {
                        guildList = db.WowGuildAssociations.ToList();
                        logWatchList = db.LogMonitoring.ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error getting guild/logwatch list -> [{ex.Message}]");
                }
                if (guildList != null)
                {
                    foreach (var guild in guildList)
                    {
                        try
                        {
                            var watchGuild = logWatchList.Where(w => w.ServerId == guild.ServerId).FirstOrDefault();
                            if (watchGuild != null)
                            {
                                if (watchGuild.MonitorLogs)
                                {
                                    //System.Console.WriteLine($"YES! Watch logs on {guild.ServerName}!");
                                    var logs = await _logsApi.GetReportsFromGuild(guildName: guild.WowGuild, realm: guild.WowRealm.Replace("'", ""), region: guild.WowRegion);
                                    if (logs != null)
                                    {
                                        var latestLog = logs[logs.Count - 1];
                                        DateTime startTime = _wowApi.UnixTimeStampToDateTime(latestLog.start);
                                        {
                                            using (var db = new NinjaBotEntities())
                                            {
                                                var latestForGuild = db.LogMonitoring.Where(l => l.ServerId == guild.ServerId).FirstOrDefault();
                                                latestForGuild.LatestLog = startTime;
                                                latestForGuild.ReportId = latestLog.id;
                                                await db.SaveChangesAsync();
                                            }
                                            //System.Console.WriteLine($"Updated [{watchGuild.ServerName}] -> [{latestLog.id}] [{latestLog.owner}]!");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Error checking for logs! -> [{ex.Message}]");
                        }
                    }
                }
            }
        }

        [Command("watch-logs", RunMode = RunMode.Async)]
        [Summary("Toggle automatic log watching from Warcraft logs")]
        public async Task ToggleLogWatchCommand()
        {
            bool enable = false;
            var embed = new EmbedBuilder();
            List<LogMonitoring> logMonitorList = null;
            StringBuilder sb = new StringBuilder();
            using (var db = new NinjaBotEntities())
            {
                logMonitorList = db.LogMonitoring.ToList();
            }
            if (logMonitorList != null)
            {
                var getGuild = logMonitorList.Where(l => l.ServerId == (long)Context.Guild.Id).FirstOrDefault();
                if (getGuild != null)
                {
                    if (!getGuild.MonitorLogs)
                    {
                        enable = true;
                    }
                }
                else
                {
                    enable = true;
                }
            }
            if (enable)
            {
                embed.Title = $"Enabling log watching for {Context.Guild.Name}!";
                sb.AppendLine($"When a new log is posted, you'll get a notification in this channel: **{Context.Channel.Name}**");
                sb.AppendLine($"If you'd like to have them posted in a different channel, use this command to disable the auto posting, and then again to enable them from the channel you'd like them posted in");
            }
            else
            {
                embed.Title = $"Disabling log watching for {Context.Guild.Name}!";
                sb.AppendLine($"Use the command again to enable log watching!");
            }
            using (var db = new NinjaBotEntities())
            {
                var getGuild = db.LogMonitoring.Where(l => l.ServerId == (long)Context.Guild.Id).FirstOrDefault();
                if (getGuild != null)
                {
                    getGuild.ChannelId = (long)Context.Channel.Id;
                    getGuild.ChannelName = Context.Channel.Name;
                    getGuild.MonitorLogs = enable;
                }
                else
                {
                    db.LogMonitoring.Add(new LogMonitoring
                    {
                        ServerId = (long)Context.Guild.Id,
                        ServerName = Context.Guild.Name,
                        ChannelId = (long)Context.Channel.Id,
                        ChannelName = Context.Channel.Name,
                        MonitorLogs = enable
                    });
                }
                await db.SaveChangesAsync();
            }
            embed.Description = sb.ToString();
            await _cc.Reply(Context, embed);
        }

        [Command("remove-achievement", RunMode = RunMode.Async)]
        [Alias("ra")]
        [RequireOwner]
        public async Task RemoveAchieve(long id)
        {
            using (var db = new NinjaBotEntities())
            {
                var foundCheeve = db.FindWowCheeves.Where(c => c.AchId == id).FirstOrDefault();
                if (foundCheeve != null)
                {
                    db.Remove(foundCheeve);
                    await db.SaveChangesAsync();
                    await _cc.Reply(Context, $"Removed achievement id {id} from the database!");
                }
                else
                {
                    await _cc.Reply(Context, $"Sorry, unable to find achievement ID {id} in the database!");
                }
            }
        }

        [Command("add-achievement", RunMode = RunMode.Async)]
        [Alias("adda")]
        [RequireOwner]
        public async Task AddAchieve(long id, int cat)
        {
            using (var db = new NinjaBotEntities())
            {
                var foundCheeve = db.FindWowCheeves.Where(c => c.AchId == id);
                if (foundCheeve != null)
                {
                    var category = db.AchCategories.Where(c => c.CatId == cat).FirstOrDefault();
                    if (category != null)
                    {
                        try
                        {
                            db.FindWowCheeves.Add(new FindWowCheeve
                            {
                                AchId = id,
                                AchCategory = category
                            });
                            await db.SaveChangesAsync();
                            await _cc.Reply(Context, $"Added achievement ID {id} with category {category.CatName} to the database!");
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"{ex.Message}");
                        }
                    }
                    else
                    {
                        await _cc.Reply(Context, $"Unable to find category with ID {cat} in the database!");
                    }

                }
                else
                {
                    await _cc.Reply(Context, $"Sorry, achievement {id} already exists in the database!");
                }
            }
        }

        [Command("list-achievements", RunMode = RunMode.Async)]
        [Alias("la")]
        [RequireOwner]
        public async Task ListCheeves()
        {
            StringBuilder sb = new StringBuilder();
            List<FindWowCheeve> cheeves = new List<FindWowCheeve>();
            using (var db = new NinjaBotEntities())
            {
                cheeves = db.FindWowCheeves.ToList();
            }
            if (cheeves.Count > 0)
            {
                foreach (var cheeve in cheeves)
                {
                    sb.AppendLine($"{cheeve.AchId}");
                }
            }
            await _cc.Reply(Context, sb.ToString());
        }

        [Command("tu", RunMode = RunMode.Async)]
        [RequireOwner]
        public async Task StartTimer()
        {
            await _logsApi.StartTimer();
        }

        [Command("td", RunMode = RunMode.Async)]
        [RequireOwner]
        public async Task StopTimer()
        {
            await _logsApi.StopTimer();
        }

        [Command("wowdiscord", RunMode = RunMode.Async)]
        [Summary("List out the class discord channels")]
        public async Task ListWowDiscordServers()
        {
            try
            {
                List<WowResources> resourceList = null;
                using (var db = new NinjaBotEntities())
                {
                    resourceList = db.WowResources.Where(r => r.ResourceDescription == "Discord").ToList();
                }
                if (resourceList != null)
                {
                    var embed = new EmbedBuilder();
                    embed.Title = $"WoW Class Discord List";
                    foreach (var resource in resourceList)
                    {
                        embed.AddField(new EmbedFieldBuilder
                        {
                            Name = $"{resource.ClassName}",
                            Value = $"{resource.Resource}",
                            IsInline = true
                        });
                    }
                    embed.WithColor(new Color(0, 255, 0));
                    await _cc.Reply(Context, embed);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error listing channels: [{ex.Message}]");
                await _cc.Reply(Context, $"Sorry, {Context.User.Username}, something went wrong :(");
            }
        }

        [Command("ksm", RunMode = RunMode.Async)]
        [Summary("Check a character for the Keystone Master achievement")]
        public async Task CheckKsm([Remainder]string args = null)
        {
            var charInfo = await GetCharFromArgs(args, Context);
            var sb = new StringBuilder();
            var embed = new EmbedBuilder();
            bool ksm = false;
            embed.Title = "Keystone Master Achievement Check";
            if (!string.IsNullOrEmpty(charInfo.charName))
            {
                Character charAchievements = null;
                if (!string.IsNullOrEmpty(charInfo.regionName))
                {
                    charAchievements = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName, charInfo.regionName);

                }
                else
                {
                    charAchievements = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName);
                }
                if (charAchievements != null)
                {
                    foreach (var cheeve in charAchievements.achievements.achievementsCompleted)
                    {
                        if (cheeve == 11162)
                        {
                            ksm = true;
                        }
                    }
                }
                if (!ksm)
                {
                    sb.AppendLine($"**{charAchievements.name}** from **{charAchievements.realm}** does not have the Keystone Master achievement! :(");
                    embed.WithColor(new Color(255, 0, 0));
                }
                else
                {
                    sb.AppendLine($"**{charAchievements.name}** from **{charAchievements.realm}** has the Keystone Master achievement! :)");
                    embed.WithColor(new Color(0, 255, 0));
                }
                embed.ThumbnailUrl = charAchievements.thumbnailURL;
            }
            else
            {
                sb.AppendLine($"Sorry, unable to find that character!");
            }
            embed.Description = sb.ToString();
            await _cc.Reply(Context, embed);
        }

        [Command("armory", RunMode = RunMode.Async)]
        [Summary("Gets a character's armory info (WoW)")]
        public async Task GetArmory([Remainder]string args = null)
        {
            string charName = string.Empty;
            string realmName = string.Empty;
            string messageToSend = string.Empty;
            string profileURL = string.Empty;
            string cheevMessage = string.Empty;
            string userName = string.Empty;
            string errorMessage = string.Empty;
            GuildChar charInfo = null;
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            if (string.IsNullOrEmpty(args))
            {
                embed.Title = $"WoW Armory Command Reference";
                sb.AppendLine($"Usage examples:");
                sb.AppendLine($":black_small_square: **{_prefix}armory** charactername");
                sb.AppendLine($"\t:black_small_square: Search armory for *charactername* (first in guild, then in the rest of WoW(US))");
                sb.AppendLine($":black_small_square: **{_prefix}armory** charactername realmname");
                sb.AppendLine($"\t:black_small_square: Search armory for *charactername* on *realmname* WoW (US)");
                sb.AppendLine($":black_small_square: **{_prefix}armory** charactername realmname region(us or eu)");
                sb.AppendLine($"\t:black_small_square: Search armory for *charactername* on *realmname* WoW (US or EU as specified)");
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
                return;
            }
            else
            {
                charInfo = await GetCharFromArgs(args, Context);
            }
            try
            {
                charName = charInfo.charName;
                realmName = charInfo.realmName;
                Character armoryInfo = null;
                if (!(string.IsNullOrEmpty(charInfo.regionName)))
                {
                    armoryInfo = _wowApi.GetCharInfo(charName, realmName, charInfo.regionName);
                }
                else
                {
                    armoryInfo = _wowApi.GetCharInfo(charName, realmName);
                }
                string powerMessage = GetPowerMessage(armoryInfo);
                if (!string.IsNullOrEmpty(powerMessage))
                {
                    sb.AppendLine(powerMessage);
                }

                string foundAchievements = FindAchievements(armoryInfo);
                if (!string.IsNullOrEmpty(foundAchievements))
                {
                    sb.AppendLine($"__Notable achievements:__");
                    sb.AppendLine(foundAchievements);
                }

                sb.AppendLine($"Armory URL: {armoryInfo.armoryURL}");
                sb.AppendLine($"Last Modified: **{_wowApi.UnixTimeStampToDateTime(armoryInfo.lastModified)}**");

                embed.Description = sb.ToString();
                embed.ThumbnailUrl = armoryInfo.thumbnailURL;
                embed.Title = $"__Armory information for (**{charName}**) on **{realmName}** (Level {armoryInfo.level} {armoryInfo.genderName} {armoryInfo.raceName} {armoryInfo.className} ({armoryInfo.mainSpec}))__";

                switch (armoryInfo.className.ToLower())
                {
                    case "monk":
                        {
                            embed.WithColor(new Color(0, 255, 0));
                            break;
                        }
                    case "druid":
                        {
                            embed.WithColor(new Color(214, 122, 2));
                            break;
                        }
                    case "death knight":
                        {
                            embed.WithColor(new Color(255, 0, 0));
                            break;
                        }
                    case "demon hunter":
                        {
                            embed.WithColor(new Color(140, 0, 126));
                            break;
                        }
                    case "hunter":
                        {
                            embed.WithColor(new Color(0, 255, 0));
                            break;
                        }
                    case "mage":
                        {
                            embed.WithColor(new Color(0, 250, 255));
                            break;
                        }
                    case "paladin":
                        {
                            embed.WithColor(new Color(255, 0, 220));
                            break;
                        }
                    case "priest":
                        {
                            embed.WithColor(new Color(255, 255, 255));
                            break;
                        }
                    case "rogue":
                        {
                            embed.WithColor(new Color(255, 255, 2));
                            break;
                        }
                    case "shaman":
                        {
                            embed.WithColor(new Color(0, 0, 255));
                            break;
                        }
                    case "warlock":
                        {
                            embed.WithColor(new Color(72, 0, 168));
                            break;
                        }
                    case "warrior":
                        {
                            embed.WithColor(new Color(119, 55, 0));
                            break;
                        }
                }
                await _cc.Reply(Context, embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Armory Info Error -> [{ex.Message}]");
                await _cc.Reply(Context, $"Unable to find **{charName}**");
                return;
            }
        }

        [Command("logs", RunMode = RunMode.Async)]
        [Summary("Gets logs from Warcraftlogs")]
        public async Task GetLogs([Remainder] string args = "")
        {
            NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
            string guildName = string.Empty;
            string realmName = string.Empty;
            string guildRegion = string.Empty;
            string locale = string.Empty;
            StringBuilder sb = new StringBuilder();
            List<Reports> guildLogs = new List<Reports>();
            int maxReturn = 2;
            int arrayCount = 0;
            string discordGuildName = string.Empty;
            var guildInfo = Context.Guild;
            var embed = new EmbedBuilder();

            guildObject = await GetGuildName();
            guildName = guildObject.guildName;
            realmName = guildObject.realmName.Replace("'", string.Empty);
            guildRegion = guildObject.regionName;
            locale = guildObject.locale;
            if (!string.IsNullOrEmpty(guildObject.locale))
            {
                locale = guildObject.locale;
            }
            if (string.IsNullOrEmpty(guildRegion))
            {
                guildRegion = "US";
            }
            if (Context.Channel is IDMChannel)
            {
                discordGuildName = Context.Channel.Name;
            }
            else if (Context.Channel is IGuildChannel)
            {
                discordGuildName = Context.Guild.Name;
            }
            if (args != null && args.Split(' ')[0].ToLower() == "name")
            {
                try
                {
                    guildLogs = await _logsApi.GetReportsFromUser(args.Split(' ')[1]);
                   // arrayCount = guildLogs.Count - 1;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Unable to find logs from **{args.Split(' ')[1]}**");
                    Console.WriteLine($"Erorr getting logs from user -> [{ex.Message}]");
                    await _cc.Reply(Context, sb.ToString());
                    return;
                }
                if (guildLogs.Count > 0)
                {
                    sb.AppendLine();
                    for (int i = 0; i <= maxReturn && i <= guildLogs.Count; i++)
                    {
                        sb.AppendLine($"[__**{guildLogs[arrayCount].title}** **/** **{guildLogs[arrayCount].zoneName}**__]({guildLogs[arrayCount].reportURL})");
                        sb.AppendLine($"\t:timer: Start time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[arrayCount].start)}**");
                        sb.AppendLine($"\t:stopwatch: End time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[arrayCount].end)}**");
                        sb.AppendLine($"\t:mag: [WoWAnalyzer](https://wowanalyzer.com/report/{guildLogs[0].id}) | :sob: [WipeFest](https://www.wipefest.net/report/{guildLogs[arrayCount].id})");
                        sb.AppendLine();
                        arrayCount++;
                    }
                    Console.WriteLine($"Sending logs to {Context.Channel.Name}, requested by {Context.User.Username}");

                    embed.Title = $":1234:__Logs from **{args.Split(' ')[1]}**__:1234: ";
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                    return;
                }
                else if (guildLogs.Count == 0)
                {
                    sb.AppendLine($"[__**{guildLogs[0].title}** **/** **{guildLogs[0].zoneName}**__]({guildLogs[0].reportURL})");
                    sb.AppendLine($"\t:timer: Start time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[0].start)}**");
                    sb.AppendLine($"\t:stopwatch: End time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[0].end)}**");
                    sb.AppendLine($"\t:mag: [WoWAnalyzer](https://wowanalyzer.com/report/{guildLogs[0].id}) | :sob: [WipeFest](https://www.wipefest.net/report/{guildLogs[arrayCount].id})");
                    sb.AppendLine();
                    Console.WriteLine($"Sending logs to {Context.Channel.Name}, requested by {Context.User.Username}");
                    embed.Title = $":1234: __Logs for **{guildName}** on **{realmName}**__:1234: ";
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                }
            }
            else
            {
                if (args.Split(',').Count() > 1)
                {
                    if (args.Contains(',') && !string.IsNullOrEmpty(args))
                    {
                        switch (args.Split(',').Count())
                        {
                            case 2:
                                {
                                    realmName = args.Split(',')[0].ToString().Trim();
                                    guildName = args.Split(',')[1].ToString().Trim();
                                    break;
                                }
                            case 3:
                                {
                                    realmName = args.Split(',')[0].ToString().Trim();
                                    guildName = args.Split(',')[1].ToString().Trim();
                                    guildRegion = args.Split(',')[2].ToString().Trim();
                                    break;
                                }
                        }
                    }
                    else
                    {
                        sb.AppendLine("Please specify a guild and realm name!");
                        sb.AppendLine($"Example: {_prefix}logs Thunderlord, UR KEY UR CARRY");
                        await _cc.Reply(Context, sb.ToString());
                        return;
                    }
                }
                if (string.IsNullOrEmpty(guildName) || string.IsNullOrEmpty(realmName))
                {
                    sb.AppendLine("Please specify a guild and realm name!");
                    sb.AppendLine($"Example: {_prefix}logs Thunderlord, UR KEY UR CARRY");
                    await _cc.Reply(Context, sb.ToString());
                    return;
                }
                try
                {
                    if (string.IsNullOrEmpty(locale))
                    {
                        guildLogs = await _logsApi.GetReportsFromGuild(guildName: guildName, realm: realmName, region: guildRegion);
                    }
                    else
                    {
                        guildLogs = await _logsApi.GetReportsFromGuild(guildName: guildName, realm: realmName, region: guildRegion, locale: locale, realmSlug: guildObject.realmSlug);
                    }
                    //arrayCount = guildLogs.Count - 1;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Unable to find logs for **{guildName}** on **{realmName}**");
                    Console.WriteLine($"{ex.Message}");
                    await _cc.Reply(Context, sb.ToString());
                    return;
                }
                if (guildLogs.Count > 0)
                {
                    sb.AppendLine();
                    for (int i = 0; i <= maxReturn && i <= guildLogs.Count; i++)
                    {
                        sb.AppendLine($"[__**{guildLogs[arrayCount].title}** **/** **{guildLogs[arrayCount].zoneName}**__]({guildLogs[arrayCount].reportURL})");
                        sb.AppendLine($"\t:timer: Start time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[arrayCount].start)}**");
                        sb.AppendLine($"\t:stopwatch: End time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[arrayCount].end)}**");
                        sb.AppendLine($"\t:mag: [WoWAnalyzer](https://wowanalyzer.com/report/{guildLogs[0].id}) | :sob: [WipeFest](https://www.wipefest.net/report/{guildLogs[arrayCount].id})");
                        sb.AppendLine();
                        arrayCount++;
                    }
                    Console.WriteLine($"Sending logs to {Context.Channel.Name}, requested by {Context.User.Username}");
                    embed.Title = $":1234:__Logs for **{guildName}** on **{realmName}**__:1234: ";
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                }
                else if (guildLogs.Count == 0)
                {
                    sb.AppendLine($"[__**{guildLogs[0].title}** **/** **{guildLogs[0].zoneName}**__]({guildLogs[0].reportURL})");
                    sb.AppendLine($"\t:timer: Start time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[0].start)}**");
                    sb.AppendLine($"\t:stopwatch: End time: **{_logsApi.UnixTimeStampToDateTime(guildLogs[0].end)}**");
                    sb.AppendLine($"\t:mag: [WoWAnalyzer](https://wowanalyzer.com/report/{guildLogs[0].id}) | :sob: [WipeFest](https://www.wipefest.net/report/{guildLogs[arrayCount].id})");
                    sb.AppendLine($"\t");
                    sb.AppendLine();
                    Console.WriteLine($"Sending logs to {Context.Channel.Name}, requested by {Context.User.Username}");
                    embed.Title = $":1234: __Logs for **{guildName}** on **{realmName}**__:1234: ";
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                }
                else
                {
                    embed.Title = $"Unable to find logs for {guildName} on {realmName} ({guildRegion})";
                    embed.Description = $"**{Context.User.Username}**, ensure you've uploaded the logs as attached to **{guildName}** on http://www.warcraftlogs.com \n";
                    embed.Description += $"More information: http://www.wowhead.com/guides/raiding/warcraft-logs";
                    await _cc.Reply(Context, embed);
                }
            }
        }

        [Command("set-guild", RunMode = RunMode.Async)]
        [Summary("Sets a realm/guild association for a Discord server")]
        public async Task SetGuild([Remainder]string args = "")
        {
            string realmName = string.Empty;
            string guildName = string.Empty;
            string region = string.Empty;
            string locale = string.Empty;

            if (args.Contains(',') && !string.IsNullOrEmpty(args))
            {
                switch (args.Split(',').Count())
                {
                    case 2:
                        {
                            realmName = args.Split(',')[0].ToString().Trim();
                            guildName = args.Split(',')[1].ToString().Trim();
                            region = "us";                            
                            break;
                        }
                    case 3:
                        {
                            realmName = args.Split(',')[0].ToString().Trim();
                            guildName = args.Split(',')[1].ToString().Trim();
                            region = args.Split(',')[2].ToString().Trim().ToLower();
                            break;
                        }
                }
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Please specify realm, guild, and locale (supported locales: us (default)/eu/ru)!");
                sb.AppendLine($"Example: {_prefix}set-guild Destromath, NinjaBread Men");
                sb.AppendLine($"Example: {_prefix}set-guild Silvermoon, Rome in a Day, eu");
                sb.AppendLine($"Example: {_prefix}set-guild Ревущий фьорд, Порейдим месяц, ru");
                await _cc.Reply(Context, sb.ToString());
                return;
            }
            locale = GetLocaleFromRegion(ref region);
            string discordGuildName = string.Empty;
            try
            {
                if (Context.Channel is IDMChannel)
                {
                    discordGuildName = Context.Channel.Name;
                }
                else if (Context.Channel is IGuildChannel)
                {
                    discordGuildName = Context.Guild.Name;
                    if (!((IGuildUser)Context.User).GuildPermissions.KickMembers) return;
                }
                GuildMembers members = null;
                try
                {
                    members = _wowApi.GetGuildMembers(realmName, guildName, locale: locale, regionName: region);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting guild info -> [{ex.Message}]");
                }

                if (members != null)
                {
                    guildName = members.name;
                    realmName = members.realm;
                    await _cc.Reply(Context, "Looking up realm realm information, hang tight!");
                    await SetGuildAssociation(guildName, realmName, locale: locale, regionName: region);
                    await GetGuild();
                }
                else
                {
                    await _cc.Reply(Context, $"Unable to associate guild/realm (**{guildName}**/**{realmName}**) (region {region}) to **{discordGuildName}** (typo?)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Set-Guild error: {ex.Message}");
            }
        }

        [Command("get-guild", RunMode = RunMode.Async)]
        [Summary("Report Discord Server -> Guild Association")]
        public async Task GetGuild([Remainder] string args = "")
        {
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            string title = string.Empty;
            GuildMembers members = null;
            string thumbUrl = string.Empty;
            var guildInfo = Context.Guild;
            string discordGuildName = string.Empty;

            if (guildInfo == null)
            {
                discordGuildName = Context.User.Username;
                thumbUrl = Context.User.GetAvatarUrl();
            }
            else
            {
                discordGuildName = Context.Guild.Name;
                thumbUrl = Context.Guild.IconUrl;
            }

            NinjaObjects.GuildObject guildObject = await GetGuildName();

            title = $"Guild association for **{discordGuildName}**";

            embed.Title = title;
            embed.ThumbnailUrl = thumbUrl;
            if (guildObject.guildName != null || members != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(guildObject.locale))
                    {
                        members = _wowApi.GetGuildMembers(guildObject.realmName, guildObject.guildName, locale: guildObject.locale, regionName: guildObject.regionName);
                    }
                    else
                    {
                        members = _wowApi.GetGuildMembers(guildObject.realmName, guildObject.guildName, regionName: guildObject.regionName);
                    }                                        
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Get-Guild Error getting guild info: [{ex.Message}]");
                }
                string guildName = string.Empty;
                string guildRealm = string.Empty;
                string guildRegion = string.Empty;
                string faction = string.Empty;
                string battlegroup = members.battlegroup;
                int achievementPoints = members.achievementPoints;
                switch (members.side)
                {
                    case 0:
                        {
                            faction = "Alliance";
                            embed.WithColor(new Color(0, 0, 255));
                            break;
                        }
                    case 1:
                        {
                            faction = "Horde";
                            embed.WithColor(new Color(255, 0, 0));
                            break;
                        }
                }
                guildName = members.name;
                guildRealm = members.realm;
                guildRegion = guildObject.regionName;
                sb.AppendLine($"Guild Name: **{guildName}**");
                sb.AppendLine($"Realm Name: **{guildRealm}**");
                sb.AppendLine($"Members: **{members.members.Count().ToString()}**");
                sb.AppendLine($"Battlegroup: **{battlegroup}**");
                sb.AppendLine($"Faction: **{faction}**");
                sb.AppendLine($"Region: **{guildRegion}**");
                sb.AppendLine($"Achievement Points: **{achievementPoints.ToString()}**");
            }
            else
            {
                sb.AppendLine($"No guild association found for **{discordGuildName}**!");
                sb.AppendLine($"Please use {_prefix}set-guild realmName, guild name, region (optional, defaults to US, valid values are eu or us) to associate a guild with **{discordGuildName}**");
            }
            embed.Description = sb.ToString();
            await _cc.Reply(Context, embed);
        }

        [Command("wow", RunMode = RunMode.Async)]
        [Summary("Use this combined with rankings (gets guild rank from WoWProgress), auctions (gets auctions for default realm ot realm specified), or search (search for a character!)")]
        public async Task GetRanking([Remainder] string args = null)
        {
            StringBuilder sb = new StringBuilder();
            bool commandProcessed = false;
            if (args != null)
            {
                if (args.Split(' ').Count() > 0)
                {
                    string commandArgs = string.Empty;
                    for (int i = 0; i < args.Split(' ').Count(); i++)
                    {
                        if (i > 0)
                        {
                            sb.Append($"{args.Split(' ')[i]} ");
                        }
                    }
                    commandArgs = sb.ToString().Trim();
                    switch (args.Split(' ')[0].ToLower())
                    {
                        case "rankings":
                            {
                                await GetWowRankings(commandArgs);
                                commandProcessed = true;
                                break;
                            }
                        case "auctions":
                            {
                                await GetAuctionItems(commandArgs);
                                commandProcessed = true;
                                break;
                            }
                        case "search":
                            {
                                await SearchWowChars(commandArgs);
                                commandProcessed = true;
                                break;
                            }
                    }
                }
            }
            if (!commandProcessed)
            {
                sb.Clear();
                sb.AppendLine($":black_medium_small_square: **rankings**");
                sb.AppendLine($"\t :black_small_square: Get rankings for the guild associated with this Discord server from http://www.wowprogress.com");
                sb.AppendLine($"\t :black_small_square: Alternatively, you can provide a guild and realm in this format: {_prefix}wow rankings *realmname*, *guildname*, (optional, defaults to US) *region* (us or eu are valid)");
                sb.AppendLine($":black_medium_small_square: **auctions**");
                sb.AppendLine($"\t :black_small_square: Get a list of the current auctions (most used raiding/crafting items) for the realm associated with this Discord server");
                sb.AppendLine($"\t :black_small_square: Alternatively, you can provide a realm in this format: {_prefix}wow auctions *realmname* (defaults to US)");
                sb.AppendLine($":black_medium_small_square: **search**");
                sb.AppendLine($"\t :black_small_square: Search the WoW website for a character name to find out the associated level and realm");
                var embed = new EmbedBuilder();
                embed.Title = $"{_prefix}wow Command Usage";
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
            }
        }

        private async Task<GuildChar> GetCharFromArgs(string args, ICommandContext context)
        {
            string regionPattern = "^[a-z]{2}$";
            string charName = string.Empty;
            string realmName = string.Empty;
            string foundRegion = string.Empty;
            Regex matchPattern = new Regex($@"{regionPattern}");
            GuildChar guildie = null;
            List<FoundChar> chars;
            NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
            GuildChar charInfo = new GuildChar
            {
                realmName = string.Empty,
                charName = string.Empty
            };
            int argNumber = args.Split(' ').Count();
            switch (argNumber)
            {
                case 1:
                    {
                        charName = args.Split(' ')[0].Trim();
                        break;
                    }
                case 2:
                    {
                        charName = args.Split(' ')[0].Trim();
                        realmName = args.Split(' ')[1].Trim();
                        break;
                    }
            }
            if (argNumber > 2)
            {
                charName = args.Split(' ')[0].Replace("'", string.Empty).Trim();
                realmName = string.Empty;
                int i = 0;
                do
                {
                    i++;
                    MatchCollection match = matchPattern.Matches(args.Split(' ')[i].ToLower());
                    if (match.Count > 0)
                    {
                        foundRegion = match[0].Value;
                        break;
                    }
                    if (i == argNumber - 1)
                    {
                        realmName += $"{args.Split(' ')[i]}".Replace("\"", "");
                    }
                    else
                    {
                        realmName += $"{args.Split(' ')[i]} ".Replace("\"", "");
                    }
                }
                while (i <= argNumber - 2);
                realmName = realmName.Trim();
            }
            if (string.IsNullOrEmpty(realmName))
            {
                //See if they're a guildie first
                try
                {
                    guildObject = await GetGuildName();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error looking up character: {ex.Message}");
                }
                if (guildObject.guildName != null && guildObject.realmName != null)
                {
                    guildie = _wowApi.GetCharFromGuild(charName, guildObject.realmName, guildObject.guildName, guildObject.regionName);
                    if (string.IsNullOrEmpty(guildie.charName))
                    {
                        guildie = null;
                    }
                }
                //Check to see if the character is in the guild
                if (guildie != null)
                {
                    charName = guildie.charName;
                    realmName = guildie.realmName;
                    charInfo.regionName = guildie.regionName;
                }
                else
                {
                    chars = _wowApi.SearchArmory(charName);
                    if (chars != null)
                    {
                        charName = chars[0].charName;
                        realmName = chars[0].realmName;
                    }
                }
            }
            if (!string.IsNullOrEmpty(foundRegion))
            {
                charInfo.regionName = foundRegion;
            }
            charInfo.charName = charName;
            charInfo.realmName = realmName;
            charInfo.locale = guildObject.locale;
            return charInfo;
        }

        [Command("gearlist", RunMode = RunMode.Async)]
        [Summary("Get a WoW character's gear list")]
        public async Task GetGearList([Remainder] string args = null)
        {
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            if (args == null)
            {
                embed.WithColor(new Color(0, 255, 0));
                embed.Title = $"{_prefix}gearlist Command Usage Guide";
                embed.WithAuthor(new EmbedAuthorBuilder
                {
                    Name = Context.User.Username,
                    IconUrl = Context.User.GetAvatarUrl()
                });
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = $"{_prefix}gearlist charName",
                    Value = "Get the gearlist for a character. Just specifying name will result in a guild lookup, followed by NA/US search",
                    IsInline = true
                });
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = $"{_prefix}gearlist charName realmName",
                    Value = "Get the gearlist for a character on a specific realm (defaults to US)",
                    IsInline = true
                });
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = $"{_prefix}gearlist charName realmName region(supports us or eu values)",
                    Value = "Get the gearlist for a character on a specific realm from a specific region (us or eu)",
                    IsInline = true
                });
                embed.AddField(new EmbedFieldBuilder
                {
                    Name = "Still need help?",
                    Value = "Visit [gngr.ninja/bot](https://gngr.ninja/bot)"
                });
                await _cc.Reply(Context, embed);
                return;
            }
            try
            {
                var charInfo = await GetCharFromArgs(args, Context);
                Character armoryInfo = new Character();
                if (!string.IsNullOrEmpty(charInfo.charName) && !string.IsNullOrEmpty(charInfo.realmName))
                {
                    if (!string.IsNullOrEmpty(charInfo.regionName))
                    {
                        armoryInfo = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName, charInfo.regionName);
                    }
                    else
                    {
                        armoryInfo = _wowApi.GetCharInfo(charInfo.charName, charInfo.realmName);
                    }
                    embed.Title = $"Gear List For {charInfo.charName} on {charInfo.realmName}";
                    embed.ThumbnailUrl = armoryInfo.profilePicURL;
                    if (armoryInfo.items.head != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Head ({armoryInfo.items.head.itemLevel})",
                            Value = $"[{armoryInfo.items.head.name}](http://www.wowhead.com/item={armoryInfo.items.head.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.hands != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Hands ({armoryInfo.items.hands.itemLevel})",
                            Value = $"[{armoryInfo.items.hands.name}](http://www.wowhead.com/item={armoryInfo.items.hands.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.neck != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Neck ({armoryInfo.items.neck.itemLevel})",
                            Value = $"[{armoryInfo.items.neck.name}](http://www.wowhead.com/item={armoryInfo.items.neck.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.waist != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Waist ({armoryInfo.items.waist.itemLevel})",
                            Value = $"[{armoryInfo.items.waist.name}](http://www.wowhead.com/item={armoryInfo.items.waist.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.shoulder != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Shoulders ({armoryInfo.items.shoulder.itemLevel})",
                            Value = $"[{armoryInfo.items.shoulder.name}](http://www.wowhead.com/item={armoryInfo.items.shoulder.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.legs != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Pants ({armoryInfo.items.legs.itemLevel})",
                            Value = $"[{armoryInfo.items.legs.name}](http://www.wowhead.com/item={armoryInfo.items.legs.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.back != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Back ({armoryInfo.items.back.itemLevel})",
                            Value = $"[{armoryInfo.items.back.name}](http://www.wowhead.com/item={armoryInfo.items.back.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.feet != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Feet ({armoryInfo.items.feet.itemLevel})",
                            Value = $"[{armoryInfo.items.feet.name}](http://www.wowhead.com/item={armoryInfo.items.feet.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.chest != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Chest ({armoryInfo.items.chest.itemLevel})",
                            Value = $"[{armoryInfo.items.chest.name}](http://www.wowhead.com/item={armoryInfo.items.chest.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.finger1 != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Ring ({armoryInfo.items.finger1.itemLevel})",
                            Value = $"[{armoryInfo.items.finger1.name}](http://www.wowhead.com/item={armoryInfo.items.finger1.id})",
                            IsInline = true
                        });
                    }

                    if (armoryInfo.items.wrist != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Wrists ({armoryInfo.items.wrist.itemLevel})",
                            Value = $"[{armoryInfo.items.wrist.name}](http://www.wowhead.com/item={armoryInfo.items.wrist.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.trinket1 != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Trinket ({armoryInfo.items.trinket1.itemLevel})",
                            Value = $"[{armoryInfo.items.trinket1.name}](http://www.wowhead.com/item={armoryInfo.items.trinket1.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.finger2 != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Ring ({armoryInfo.items.finger2.itemLevel})",
                            Value = $"[{armoryInfo.items.finger2.name}](http://www.wowhead.com/item={armoryInfo.items.finger2.id})",
                            IsInline = true
                        });
                    }

                    if (armoryInfo.items.trinket2 != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"Trinket ({armoryInfo.items.trinket2.itemLevel})",
                            Value = $"[{armoryInfo.items.trinket2.name}](http://www.wowhead.com/item={armoryInfo.items.trinket2.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.mainHand != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"MainHand ({armoryInfo.items.mainHand.itemLevel})",
                            Value = $"[{armoryInfo.items.mainHand.name}](http://www.wowhead.com/item={armoryInfo.items.mainHand.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.offHand != null)
                    {
                        embed.Fields.Add(new EmbedFieldBuilder
                        {
                            Name = $"OffHand ({armoryInfo.items.offHand.itemLevel})",
                            Value = $"[{armoryInfo.items.offHand.name}](http://www.wowhead.com/item={armoryInfo.items.offHand.id})",
                            IsInline = true
                        });
                    }
                    if (armoryInfo.items.averageItemLevel < 850)
                    {
                        embed.WithColor(new Color(0, 255, 0));
                    }
                    else if (armoryInfo.items.averageItemLevel > 850 && armoryInfo.items.averageItemLevel < 885)
                    {
                        embed.WithColor(new Color(0, 0, 255));
                    }
                    else
                    {
                        embed.WithColor(new Color(148, 0, 211));
                    }
                    sb.AppendLine($"Average ilvl ({armoryInfo.items.averageItemLevel}) Equipped ilvl ({armoryInfo.items.averageItemLevelEquipped})");
                    embed.Footer = new EmbedFooterBuilder { Text = sb.ToString() };
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error getting gear list, sorry!");
                embed.Description = sb.ToString();
                System.Console.WriteLine($"Error getting gear list for {ex.Message}");
            }
            await _cc.Reply(Context, embed);
        }

        [Command("top10", RunMode = RunMode.Async)]
        [Summary("Get the top 10 dps or hps for the latest raid in World of Warcraft (via warcraftlogs.com)")]
        public async Task GetTop10([Remainder] string args = null)
        {
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            string fightName = string.Empty;
            string guildOnly = string.Empty;
            string difficulty = string.Empty;
            string metric = string.Empty;
            string raidName = string.Empty;
            string thumbUrl = string.Empty;
            var guildInfo = Context.Guild;
            string discordGuildName = string.Empty;
            int encounterID = 0;
            string region = "us";

            //Attempt to get guild info
            NinjaObjects.GuildObject guildObject = await GetGuildName();
            string realmName = guildObject.realmName.Replace("'", string.Empty);
            string guildName = guildObject.guildName;
            region = guildObject.regionName;

            var fightList = WarcraftLogs.Zones.Where(z => z.id == 19).Select(z => z.encounters).FirstOrDefault();
            raidName = WarcraftLogs.Zones.Where(z => z.id == 19).Select(z => z.name).FirstOrDefault();

            //Get Guild Information for Discord Server (or channel for DM)
            if (Context.Channel is IDMChannel)
            {
                discordGuildName = Context.User.Username;
                thumbUrl = Context.User.GetAvatarUrl();
            }
            else if (Context.Channel is IGuildChannel)
            {
                discordGuildName = Context.Guild.Name;
                thumbUrl = Context.Guild.IconUrl;
            }
            //Argument logic
            if (args == null || args.Split(',')[0] == "help")
            {
                sb.AppendLine($"**{_prefix}top10** fightName(or ID from {_prefix}top10 list) guild(type guild to get guild only results, all for all guilds) metric(dps(default), or hps) difficulty(lfr, flex, normal, heroic(default), or mythic) ");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** list");
                sb.AppendLine($"Get a list of all encounters and shortcut IDs");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** 1");
                sb.AppendLine($"The above command would get all top 10 **dps** results for **Garothi Worldbreaker** on **{realmName}**.");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** 1, guild");
                sb.AppendLine($"The above command would get the top 10 **dps** results for **Garothi Worldbreaker** on **{realmName}** for **{guildName}**.");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** 1, guild, hps");
                sb.AppendLine($"The above command would get the top 10 **hps** results for **Garothi Worldbreaker** on **{realmName}** for **{guildName}**.");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** 1, all, hps");
                sb.AppendLine($"The above command would get all top 10 **hps** results for **Garothi Worldbreaker** on **{realmName}**.");
                sb.AppendLine();
                sb.AppendLine($"**{_prefix}top10** 1, guild, dps, mythic");
                sb.AppendLine($"The above command would get the top 10 **dps** results for **Garothi Worldbreaker** on **{realmName}** for **{guildName}** on **mythic** difficulty.");
                embed.Title = $"{Context.User.Username}, here are some examples for **{_prefix}top10**";
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
                return;
            }
            else
            {
                if (args.Split(' ')[0].ToLower() == "list")
                {
                    //list fights here
                    if (fightList != null)
                    {
                        embed.Title = $"__Fight names for **{raidName}**__";
                        int j = 1;
                        foreach (var fight in fightList)
                        {
                            sb.AppendLine($"[**{j}**] {fight.name}");
                            j++;
                        }
                        embed.Description = sb.ToString();
                        await _cc.Reply(Context, embed);
                    }
                    return;
                }
                difficulty = "heroic";
                int argCount = args.Split(',').Count();
                string[] splitArgs = args.Split(',');
                switch (argCount)
                {
                    //Just name
                    case 1:
                        {
                            fightName = splitArgs[0].Trim();
                            break;
                        }
                    //Name + metric
                    case 2:
                        {
                            fightName = splitArgs[0].Trim();
                            metric = splitArgs[1].Trim();
                            break;
                        }
                    //Name + metric + guild/all
                    case 3:
                        {
                            fightName = splitArgs[0].Trim();
                            guildOnly = splitArgs[1].Trim();
                            metric = splitArgs[2].Trim();
                            break;
                        }
                    //Name + metric + guild/all + difficulty
                    case 4:
                        {
                            fightName = splitArgs[0].Trim();
                            guildOnly = splitArgs[1].Trim();
                            metric = splitArgs[2].Trim();
                            difficulty = splitArgs[3].Trim();
                            break;
                        }
                }
                //Difficulty logic
                int difficultyID = 4;
                switch (difficulty.ToLower())
                {
                    case "lfr":
                        {
                            difficultyID = 1;
                            break;
                        }
                    case "flex":
                        {
                            difficultyID = 2;
                            break;
                        }
                    case "normal":
                        {
                            difficultyID = 3;
                            break;
                        }
                    case "heroic":
                        {
                            difficultyID = 4;
                            break;
                        }
                    case "mythic":
                        {
                            difficultyID = 5;
                            break;
                        }
                }
                //End difficulty logic
                //Get the list of fights that pertain to the specific zone id. 11 == Nighthold (True)
                //Begin fight logic                
                WarcraftlogRankings.RankingObject l = new WarcraftlogRankings.RankingObject();
                if (fightName.Length <= 2)
                {
                    switch (fightName)
                    {
                        case "1":
                            {
                                encounterID = fightList[0].id;
                                break;
                            }
                        case "2":
                            {
                                encounterID = fightList[1].id;
                                break;
                            }
                        case "3":
                            {
                                encounterID = fightList[2].id;
                                break;
                            }
                        case "4":
                            {
                                encounterID = fightList[3].id;
                                break;
                            }
                        case "5":
                            {
                                encounterID = fightList[4].id;
                                break;
                            }
                        case "6":
                            {
                                encounterID = fightList[5].id;
                                break;
                            }
                        case "7":
                            {
                                encounterID = fightList[6].id;
                                break;
                            }
                        case "8":
                            {
                                encounterID = fightList[7].id;
                                break;
                            }
                        case "9":
                            {
                                encounterID = fightList[8].id;
                                break;
                            }
                        case "10":
                            {
                                encounterID = fightList[9].id;
                                break;
                            }
                        case "11":
                            {
                                encounterID = fightList[10].id;
                                break;
                            }
                    }
                }
                else
                {
                    encounterID = GetEncounterID(fightName);
                }
                //End fight logic               
                //Begin metric set
                string metricEmoji = string.Empty;
                if (string.IsNullOrEmpty(metric))
                {
                    metric = "dps";
                }
                switch (metric.ToLower())
                {
                    case "hps":
                        {
                            embed.WithColor(new Color(0, 255, 0));
                            metricEmoji = ":green_heart:";
                            break;
                        }
                    case "dps":
                        {
                            embed.WithColor(new Color(255, 0, 0));
                            metricEmoji = ":dagger: ";
                            break;
                        }
                    default:
                        {
                            embed.WithColor(new Color(255, 0, 0));
                            metricEmoji = ":dagger: ";
                            metric = "dps";
                            break;
                        }
                }
                //End metric set
                if (string.IsNullOrEmpty(fightName))
                {
                    sb.AppendLine($"{Context.User.Username}, please specify a fight name/number!");
                    sb.AppendLine($"**Example:** {_prefix}top10 1");
                    sb.AppendLine($"**Encounter Lists:** {_prefix}top10 list");
                    await _cc.Reply(Context, sb.ToString());
                    return;
                }

                if (!(string.IsNullOrEmpty(guildOnly) || guildOnly.ToLower() != "guild"))
                {
                    if (!string.IsNullOrEmpty(guildObject.realmSlug))
                    {
                        l = await _logsApi.GetRankingsByEncounterGuildSlug(encounterID, guildObject.realmSlug, guildObject.guildName, metric, difficultyID, region);
                    }
                    else
                    {
                        l = await _logsApi.GetRankingsByEncounterGuild(encounterID, guildObject.realmName, guildObject.guildName, metric, difficultyID, region);                   
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(guildObject.realmSlug))
                    {
                        l = await _logsApi.GetRankingsByEncounterSlug(encounterID, guildObject.realmSlug, metric, difficultyID, region);
                    }
                    else
                    {
                        l = await _logsApi.GetRankingsByEncounter(encounterID, realmName, metric, difficultyID, region);
                    }

                }
                string fightNameFromEncounterID = fightList.Where(f => f.id == encounterID).Select(f => f.name).FirstOrDefault();
                var top10 = l.rankings.OrderByDescending(a => a.total).Take(10);
                string difficultyName = string.Empty;
                switch (difficultyID)
                {
                    case 1:
                        {
                            difficultyName = "LFR";
                            break;
                        }
                    case 2:
                        {
                            difficultyName = "Flex";
                            break;
                        }
                    case 3:
                        {
                            difficultyName = "Normal";
                            break;
                        }
                    case 4:
                        {
                            difficultyName = "Heroic";
                            break;
                        }
                    case 5:
                        {
                            difficultyName = "Mythic";
                            break;
                        }
                }
                embed.Title = $"__Top 10 for fight [**{fightNameFromEncounterID}** (Metric [**{metric.ToUpper()}**] Difficulty [**{difficultyName}**]) Realm [**{guildObject.realmName}**]]__";
                int i = 1;
                foreach (var rank in top10)
                {
                    var classInfo = WarcraftLogs.CharClasses.Where(c => c.id == rank._class).FirstOrDefault();
                    sb.AppendLine($"**{i}** [{rank.name}](http://{region}.battle.net/wow/en/character/{rank.serverName.Replace(" ","-")}/{rank.name}/advanced) ilvl **{rank.itemLevel}** {classInfo.name} from *[{rank.guildName}]*");
                    sb.AppendLine($"\t{metricEmoji}[**{rank.total.ToString("###,###")}** {metric.ToLower()}]");
                    i++;
                }
                sb.AppendLine($"Data gathered from **https://www.warcraftlogs.com**");
                embed.Description = sb.ToString();
                embed.ThumbnailUrl = thumbUrl;
                try
                {
                    await _cc.Reply(Context, embed);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        [Command("realm-info", RunMode = RunMode.Async)]
        [Summary("Return WoW realm information")]
        public async Task GetRealmInfo([Remainder] string args = null)
        {
            var embed = new EmbedBuilder();
            StringBuilder sb = new StringBuilder();
            var guildInfo = await GetGuildName();
            string region = string.Empty;
            string findMe = string.Empty;
            findMe = args;

            if (!string.IsNullOrEmpty(guildInfo.regionName))
            {
                region = guildInfo.regionName;
            }
            else
            {
                region = "us";
            }

            if (!string.IsNullOrEmpty(guildInfo.realmName) && string.IsNullOrEmpty(findMe))
            {
                findMe = guildInfo.realmName;
            }
            var getRealmList = _wowApi.GetRealmStatus(region);
            var foundRealm = getRealmList.realms.Where(r => r.name.ToLower().Contains(findMe.ToLower())).FirstOrDefault();
            if (foundRealm != null)
            {
                embed.Title = $"Realm Information for {foundRealm.name}!";
                sb.AppendLine($":black_small_square: Battlegroup: **{foundRealm.battlegroup}**");
                sb.AppendLine($":black_small_square: Type: **{foundRealm.type}**");
                sb.AppendLine($":black_small_square: Locale: **{foundRealm.locale}**");
                sb.AppendLine($":black_small_square: Population: **{foundRealm.population}**");
                sb.AppendLine($":black_small_square: Status: **{foundRealm.status}**");
                sb.AppendLine($":black_small_square: TimeZone: **{foundRealm.timezone}**");
                sb.AppendLine($":black_small_square: Queue: **{foundRealm.queue}**");
                sb.AppendLine($":black_small_square: Connected Realms:");
                foreach (var realm in foundRealm.connected_realms)
                {
                    sb.AppendLine($"\t :black_small_square: **{realm}**");
                }
            }
            if (foundRealm.status)
            {
                embed.WithColor(new Color(0, 255, 0));
            }
            else
            {
                embed.WithColor(new Color(255, 0, 0));
            }
            embed.Description = sb.ToString();
            await _cc.Reply(Context, embed);
        }

        private async Task SearchWowChars(string args)
        {
            if (args.Split(' ').Count() > 1)
            {
                await _cc.Reply(Context, $"Please specify only a character name for the search!");
                return;
            }
            StringBuilder sb = new StringBuilder();
            string charName = args;
            List<FoundChar> found = _wowApi.SearchArmory(charName);
            var embed = new EmbedBuilder();
            embed.Title = $"__WoW Armory Search Results For: **{charName}**__";

            foreach (FoundChar searchFound in found)
            {
                sb.AppendLine($":black_small_square: **{searchFound.charName}** (**{searchFound.level}**) *{searchFound.realmName}*");
            }
            embed.WithColor(new Color(255, 0, 0));
            embed.Description = sb.ToString();
            await _cc.Reply(Context, embed);
        }

        private async Task GetAuctionItems(string args)
        {
            try
            {
                NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
                string realmName = string.Empty;
                string regionName = "us";
                if (string.IsNullOrEmpty(args))
                {
                    guildObject = await GetGuildName();
                    realmName = guildObject.realmName;
                    regionName = guildObject.regionName;
                }
                else
                {
                    int i = 0;
                    do
                    {
                        if (i == args.Split(' ').Count() - 1)
                        {
                            realmName += $"{args.Split(' ')[i]}".Replace("\"", "");
                        }
                        else
                        {
                            realmName += $"{args.Split(' ')[i]} ".Replace("\"", "");
                        }
                        i++;
                    }
                    while (i <= args.Split(' ').Count() - 1);
                    realmName = realmName.Trim();
                }
                if (string.IsNullOrEmpty(realmName))
                {
                    await _cc.Reply(Context, $"Unable to find realm \nTry {_prefix}wow auctions realmName");
                    return;
                }
                Console.WriteLine($"Looking up auctions for realm {realmName.ToUpper()}");
                List<WowAuctions> auctions = await _wowApi.GetAuctionsByRealm(realmName.ToLower(), guildObject.regionName);
                StringBuilder sb = new StringBuilder();
                var auctionList = GetAuctionItemIDs();
                var embed = new EmbedBuilder();
                embed.WithColor(new Color(0, 255, 0));
                embed.Title = $":scales:Auction prices on **{realmName.ToUpper()}**";
                foreach (var item in auctionList)
                {
                    var auction = auctions.Where(auc => auc.AuctionItemId == item.ItemID).ToList();
                    long lowestPrice = GetLowestBuyoutPrice(auction.Where(r => r.AuctionBuyout != 0));
                    if (auction.Count() != 0)
                    {
                        sb.AppendLine($":black_small_square:__**{item.Name}**__ / Found **{auction.Count()}** / Lowest price **{ lowestPrice / 10000}g**");
                    }
                    else
                    {
                        sb.AppendLine($":black_small_square:__**{item.Name}**__ / None found :(");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"Last updated: **{auctions[0].DateModified}**");
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
                
                using (var db = new NinjaBotEntities())
                {
                    foreach (var item in auctionList)
                    {
                        Console.WriteLine(item.ItemID);
                        var items = auctions.Where(auc => auc.AuctionItemId == item.ItemID).ToList();
                        long lowestPrice = GetLowestBuyoutPrice(items.Where(r => r.AuctionBuyout != 0));
                        long highestPrice = GetHighestBuyoutPrice(items.Where(r => r.AuctionBuyout != 0));
                        long averagePrice = GetAveragePrice(items.Where(r => r.AuctionBuyout != 0));
                        if (items.Count() > 0)
                        {
                            db.WowAuctionPrices.Add(new WowAuctionPrice
                            {
                                AuctionItemId = (long)items[0].AuctionItemId,
                                AuctionRealm = items[0].RealmSlug,
                                AvgPrice = averagePrice,
                                MinPrice = lowestPrice,
                                MaxPrice = highestPrice,
                                Seen = items.Count()
                            });
                        }
                    }
                    await db.SaveChangesAsync();
                }                                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auction error: [{ex.Message}]");
                await _cc.Reply(Context, "Error getting auctions :(");
            }
        }

        private List<AuctionList> GetAuctionItemIDs()
        {
            List<AuctionList> auctionItems = new List<AuctionList>();
            using (var db = new NinjaBotEntities())
            {
                var auctionItemList = db.AuctionItemMappings.ToList();
                foreach (var item in auctionItemList)
                {
                    auctionItems.Add(new AuctionList
                    {
                        ItemID = (int)item.ItemId,
                        Name = item.Name
                    });
                }
            }
            return auctionItems;
        }

        private long GetLowestBuyoutPrice(IEnumerable<WowAuctions> auctions)
        {
            long lowestBuyoutPrice = long.MaxValue;
            try
            {
                foreach (var item in auctions)
                {
                    if ((item.AuctionBuyout / item.AuctionQuantity) < lowestBuyoutPrice)
                    {
                        lowestBuyoutPrice = Math.Min(lowestBuyoutPrice, ((long)item.AuctionBuyout / (long)item.AuctionQuantity));
                    }
                }
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine($"Get Lowest Buyout Error -> [{ex.Message}]");
            }
            return lowestBuyoutPrice;
        }

        private long GetHighestBuyoutPrice(IEnumerable<WowAuctions> auctions)
        {
            long highestBuyoutPrice = long.MinValue;
            try
            {
                foreach (var item in auctions)
                {
                    if ((item.AuctionBuyout / item.AuctionQuantity) > highestBuyoutPrice)
                    {
                        highestBuyoutPrice = Math.Max(highestBuyoutPrice, ((long)item.AuctionBuyout / (long)item.AuctionQuantity));
                    }
                }
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine($"Get Highest Buyout Error -> [{ex.Message}]");
            }
            return highestBuyoutPrice;
        }

        private long GetAveragePrice(IEnumerable<WowAuctions> auctions)
        {
            long averageBuyoutPrice = 0;
            long? total = 0;
            int totalAuctions = auctions.Count();
            try
            {
                foreach (var item in auctions)
                {
                    total += item.AuctionBuyout / (long)item.AuctionQuantity;
                }
                averageBuyoutPrice = (long)total / totalAuctions;
            }
            catch (DivideByZeroException ex)
            {
                Console.WriteLine($"Get Average Buyout Error -> [{ex.Message}]");
            }
            return averageBuyoutPrice;
        }

        private async Task GetWowRankings(string args = "")
        {
            NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
            string guildName = string.Empty;
            string realmName = string.Empty;
            string regionName = "us";
            if (string.IsNullOrEmpty(args))
            {
                guildObject = await GetGuildName();
                guildName = guildObject.guildName;
                realmName = guildObject.realmName;
                regionName = guildObject.regionName;
            }
            else
            {
                if (args.Contains(','))
                {
                    switch (args.Split(',').Count())
                    {
                        case 2:
                            {
                                realmName = args.Split(',')[0].ToString().Trim();
                                guildName = args.Split(',')[1].ToString().Trim();
                                break;
                            }
                        case 3:
                            {
                                realmName = args.Split(',')[0].ToString().Trim();
                                guildName = args.Split(',')[1].ToString().Trim();
                                regionName = args.Split(',')[2].ToString().Trim();
                                break;
                            }
                    }


                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    var embed = new EmbedBuilder();
                    embed.WithColor(new Color(255, 0, 0));
                    embed.Title = $"Unable to find a guild/realm association!\nTry {_prefix}wow rankings Realm Name, Guild Name";
                    sb.AppendLine($"Command syntax: {_prefix}wow rankings realm name, guild name");
                    sb.AppendLine($"Command example: {_prefix}wow rankings azgalor, carebears");
                    embed.Description = sb.ToString();
                    await _cc.Reply(Context, embed);
                    return;
                }
            }
            if (string.IsNullOrEmpty(guildName) || string.IsNullOrEmpty(realmName))
            {
                StringBuilder sb = new StringBuilder();
                var embed = new EmbedBuilder();
                embed.WithColor(new Color(255, 0, 0));
                embed.Title = $"Unable to find a guild/realm association!\nTry {_prefix}wow rankings Realm Name, Guild Name";
                sb.AppendLine($"Command syntax: {_prefix}wow rankings realm name, guild name");
                sb.AppendLine($"Command example: {_prefix}wow rankings azgalor, carebears");
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
                return;
            }
            try
            {
                var guildMembers = _wowApi.GetGuildMembers(realmName, guildName, regionName);
                int memberCount = 0;
                if (guildMembers != null)
                {
                    guildName = guildMembers.name;
                    realmName = guildMembers.realm;
                    memberCount = guildMembers.members.Count();
                }
                var wowProgressApi = new WowProgress();
                StringBuilder sb = new StringBuilder();
                var embed = new EmbedBuilder();
                embed.WithColor(new Color(255, 255, 0));
                var ranking = wowProgressApi.GetGuildRank(guildName, realmName, regionName);
                var realmObject = wowProgressApi.GetRealmObject(realmName, wowProgressApi._links, regionName);
                var topGuilds = realmObject.OrderBy(r => r.realm_rank).Take(3);
                var guild = realmObject.Where(r => r.name.ToLower() == guildName.ToLower()).FirstOrDefault();
                int guildRank = guild.realm_rank;
                var surroundingGuilds = realmObject.Where(r => r.realm_rank > (guild.realm_rank - 2) && r.realm_rank < (guild.realm_rank + 2));

                embed.Title = $"__:straight_ruler:Guild ranking for **{guildName}** [**{memberCount}** members] (Score: **{ranking.score}**):straight_ruler:__";
                sb.AppendLine($"Realm rank: **{ranking.realm_rank}** **|** World rank: **{ranking.world_rank}** **|** Area rank: **{ranking.area_rank}**");
                sb.AppendLine();
                sb.AppendLine($"__Where **{guildName}** fits in on **{realmName}**__");
                foreach (var singleGuild in surroundingGuilds)
                {
                    sb.AppendLine($"\t(**{singleGuild.realm_rank}**) **{singleGuild.name}** **|** World rank: **{singleGuild.world_rank}**");
                }
                sb.AppendLine();
                sb.AppendLine($"__:top:Top 3 guilds on **{realmName}**:top:__");
                foreach (var topGuild in topGuilds)
                {
                    sb.AppendLine($"\t(**{topGuild.realm_rank}**) **{topGuild.name}** **|** World Rank: **{topGuild.world_rank}**");
                }
                sb.AppendLine();
                sb.AppendLine("Ranking data gathered via **WoWProgress.com**");
                embed.WithUrl($"{guild.url}");
                embed.Description = sb.ToString();

                await _cc.Reply(Context, embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} {ex.InnerException} {ex.Data}{ex.Source}{ex.StackTrace}");
                StringBuilder sb = new StringBuilder();
                var embed = new EmbedBuilder();
                embed.WithColor(new Color(255, 0, 0));
                embed.Title = $":frowning: Sorry, {Context.User.Username}, something went wrong! Perhaps check the guild's home realm.:frowning: ";
                sb.AppendLine($"Command syntax: {_prefix}wow rankings realm name, guild name");
                sb.AppendLine($"Command example: {_prefix}wow rankings azgalor, carebears");
                embed.Description = sb.ToString();
                await _cc.Reply(Context, embed);
            }
        }

        private async Task<NinjaObjects.GuildObject> GetGuildName()
        {
            NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
            try
            {
                if (Context.Channel is IDMChannel)
                {
                    guildObject = await GetGuildAssociation(Context.User.Username);
                }
                else if (Context.Channel is IGuildChannel)
                {
                    guildObject = await GetGuildAssociation(Context.Guild.Name);
                }
                Console.WriteLine($"getGuildName: {Context.Channel.Name} : {guildObject.guildName} -> {guildObject.realmName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"getGuildName: {ex.Message}");
            }
            return guildObject;
        }

        private async Task<NinjaObjects.GuildObject> GetGuildAssociation(string discordGuildName)
        {
            NinjaObjects.GuildObject guildObject = new NinjaObjects.GuildObject();
            using (var db = new NinjaBotEntities())
            {
                var foundGuild = db.WowGuildAssociations.FirstOrDefault(g => g.ServerName == discordGuildName);
                if (foundGuild != null)
                {
                    guildObject.guildName = foundGuild.WowGuild;
                    guildObject.realmName = foundGuild.WowRealm;
                    guildObject.regionName = foundGuild.WowRegion;
                    guildObject.locale = foundGuild.Locale;
                    guildObject.realmSlug = foundGuild.LocalRealmSlug;
                }
            }
            return guildObject;
        }

        private async Task SetGuildAssociation(string wowGuildName, string realmName, string locale, string regionName)
        {
            try
            {
                var guildInfo = Context.Guild;

                string guildName = string.Empty;
                string realmSlug = string.Empty;
                string apiRegion = string.Empty;                
                ulong guildId;

                //guild in this context is the Discord server
                //this if statement gets the user information if it is a DM, discord server info otherwise
                if (Context.Channel is IDMChannel)
                {
                    guildName = Context.User.Username;
                    guildId = Context.User.Id;
                }  
                else 
                {
                    guildName = guildInfo.Name;
                    guildId = guildInfo.Id;
                }

                if (regionName.ToLower() == "us")
                {
                    apiRegion = "us";
                }
                else
                {
                    apiRegion = "eu";
                }

                //use locale to determine realm slug
                for (int i = 0; i<500; i++)
                {
                    System.Console.WriteLine("Attempting to find slug!");
                    var slugs = _wowApi.GetRealmStatus(locale: locale, region: apiRegion);                        
                    switch (locale)
                    {
                        case "ru_RU":
                            {                                                        
                                realmSlug = slugs.realms.Where(r => r.name.Replace("'","").ToLower().Contains(realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                                break;
                            }
                        case "en_GB":
                            {                            
                                realmSlug = slugs.realms.Where(r => r.name.Replace("'","").ToLower().Contains(realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                                break;
                            }
                        case "en_US":
                            {                            
                                realmSlug = slugs.realms.Where(r => r.name.Replace("'","").ToLower().Contains(realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                                break;
                            }
                        default: 
                            {                            
                                realmSlug = slugs.realms.Where(r => r.name.Replace("'","").ToLower().Contains(realmName.ToLower())).Select(s => s.slug).FirstOrDefault();
                                break;
                            }
                    }
                    if (!string.IsNullOrEmpty(realmSlug))
                    {
                        System.Console.WriteLine($"Found slug {realmSlug}!");
                        break;
                    }
                }
            
                using (var db = new NinjaBotEntities())
                {
                    var foundGuild = db.WowGuildAssociations.FirstOrDefault(g => g.ServerName == guildName);

                    if (foundGuild == null)
                    {
                        WowGuildAssociations newGuild = new WowGuildAssociations
                        {
                            ServerId = (long)guildId,
                            ServerName = guildName,
                            WowGuild = wowGuildName,
                            WowRealm = realmName,
                            WowRegion = apiRegion,
                            LocalRealmSlug = realmSlug,
                            Locale = locale,
                            SetBy = Context.User.Username,
                            SetById = (long)Context.User.Id,
                            TimeSet = DateTime.Now
                        };
                        db.WowGuildAssociations.Add(newGuild);
                    }
                    else
                    {
                        foundGuild.ServerId = (long)guildId;
                        foundGuild.WowGuild = wowGuildName;
                        foundGuild.WowRealm = realmName;
                        foundGuild.WowRegion = apiRegion;
                        foundGuild.Locale = locale;
                        foundGuild.LocalRealmSlug = realmSlug;
                        foundGuild.SetBy = Context.User.Username;
                        foundGuild.SetById = (long)Context.User.Id;
                        foundGuild.TimeSet = DateTime.Now;
                    }
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting guild association for {Context.Guild.Name} to {wowGuildName}-{realmName} [{ex.Message}]");
            }
        }

        private static string GetLocaleFromRegion(ref string regionName)
        {
            string locale;
            switch (regionName)
            {
                case "na":
                    {
                        locale = "en_US";
                        break;
                    }
                case "us":
                    {
                        locale = "en_US";
                        break;
                    } 
                case "eu":
                    {
                        locale = "en_GB";
                        break;
                    }
                case "gb":
                    {                     
                        locale = "en_GB";
                        break;
                    }
                case "uk":
                    {
                        
                        locale = "en_GB";
                        break;
                    }
                case "ru":
                    {
                        locale = "ru_RU";
                        break;
                    }
                default:
                    {
                        locale = "en_US";
                        break;
                    }
            }
            return locale;
        }

        private string FindAchievements(Character armoryInfo)
        {
            StringBuilder cheevMessage = new StringBuilder();
            List<FindWowCheeve> findCheeves = null;
            var completedCheeves = armoryInfo.achievements.achievementsCompleted;
            using (var db = new NinjaBotEntities())
            {
                findCheeves = db.FindWowCheeves.ToList();
            }
            if (findCheeves != null)
            {
                foreach (int achievement in completedCheeves)
                {
                    var findMe = findCheeves.Where(f => f.AchId == achievement).FirstOrDefault();
                    if (findMe != null)
                    {
                        var matchedCheeve = WowApi.Achievements.Where(c => c.id == findMe.AchId).FirstOrDefault();
                        if (matchedCheeve != null)
                        {
                            cheevMessage.AppendLine($":white_check_mark: {matchedCheeve.title}");
                        }
                    }
                }
            }
            return cheevMessage.ToString();
        }

        private string GetPowerMessage(Character armoryInfo)
        {
            StringBuilder sb = new StringBuilder();
            string powerMessage = string.Empty;
            switch (armoryInfo.stats.powerType)
            {
                case "mana":
                    {
                        powerMessage = $":large_blue_circle:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }

                case "energy":
                    {
                        powerMessage = $":yellow_heart:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }
                case "focus":
                    {
                        powerMessage = $":evergreen_tree:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}]**";
                        break;
                    }
                case "rage":
                    {
                        powerMessage = $":rage:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }
                case "chi":
                    {
                        powerMessage = $":comet:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }
                case "runic-power":
                    {
                        powerMessage = $":red_circle:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }
                case "pain":
                    {
                        powerMessage = $":purple_heart:[**{String.Format("{0:#,##0}", armoryInfo.stats.power)}**]";
                        break;
                    }
            }
            sb.AppendLine($":100:__Statistics__:100:");
            sb.AppendLine($":green_heart:[**{String.Format("{0:#,##0}", armoryInfo.stats.health)}**] / {powerMessage}");
            sb.AppendLine($" Haste **{armoryInfo.stats.hasteRating}**(**{String.Format("{0:0.00}", armoryInfo.stats.haste)}%**) / Crit **{armoryInfo.stats.critRating}**(**{String.Format("{0:0.00}", armoryInfo.stats.crit)}**%)");
            sb.AppendLine($" Mastery **{armoryInfo.stats.masteryRating}**(**{String.Format("{0:0.00}", armoryInfo.stats.mastery)}**%) / Versatility: **{armoryInfo.stats.versatility}**");
            sb.AppendLine($" Stamina **{armoryInfo.stats.sta}** / Intellect **{armoryInfo.stats._int}** / Strength **{armoryInfo.stats.str}** / Agility **{armoryInfo.stats.agi}** / Armor **{armoryInfo.stats.armor}**");
            sb.AppendLine($" Avoidance **{armoryInfo.stats.avoidanceRating}** / Block **{String.Format("{0:0.00}", armoryInfo.stats.block)}**% / Dodge **{String.Format("{0:0.00}", armoryInfo.stats.dodge)}**%/ Parry **{armoryInfo.stats.parryRating}**(**{String.Format("{0:0.00}", armoryInfo.stats.parry)}**%)");
            sb.AppendLine();
            sb.AppendLine($":heavy_division_sign:__Average Item Level__: **{armoryInfo.items.averageItemLevel}** / Equipped: **{armoryInfo.items.averageItemLevelEquipped}**");
            sb.AppendLine($":arrow_down_small:__Lowest Item Level__: **{armoryInfo.lowestItemLevel.itemName}** / **{armoryInfo.lowestItemLevel.itemLevel}**");
            sb.AppendLine($":arrow_up_small:__Highest Item Level__: **{armoryInfo.highestItemLevel.itemName}** / **{armoryInfo.highestItemLevel.itemLevel}**");
            sb.AppendLine($":point_right:__Achievement Points__: **{armoryInfo.achievementPoints}**");
            sb.AppendLine();

            return sb.ToString();
        }

        private int GetEncounterID(string encounterName, string zoneName = "The Nighthold")
        {
            int encounterID = 0;
            var zone = WarcraftLogs.Zones.Where(z => z.name.ToLower() == zoneName.ToLower()).FirstOrDefault();
            if (zone != null)
            {
                encounterID = zone.encounters.Where(e => e.name.ToLower() == encounterName.ToLower()).FirstOrDefault().id;
            }
            return encounterID;
        }

        private string GetNumberEmojiFromString(int number)
        {
            string numberEmoji = string.Empty;
            switch (number)
            {
                case 1:
                {
                    numberEmoji = ":one:";
                    break;
                }
                case 2:
                {
                    numberEmoji = ":two:";
                    break;
                }
                case 3:
                {
                    numberEmoji = ":three:";
                    break;
                }
                case 4:
                {
                    numberEmoji = ":four:";
                    break;
                }
                case 5:
                {
                    numberEmoji = ":five:";
                    break;
                }
                case 6:
                {
                    numberEmoji = ":six:";
                    break;
                }
                case 7:
                {
                    numberEmoji = ":seven:";
                    break;
                }
                case 8:
                {
                    numberEmoji = ":eight:";
                    break;
                }
                case 9:
                {
                    numberEmoji = ":nine:";
                    break;
                }
                case 10:
                {
                    numberEmoji = ":one::zero:";
                    break;
                }
                case 11:
                {
                    numberEmoji = ":one::one:";
                    break;
                }
                case 12:
                {
                    numberEmoji = ":one::two:";
                    break;
                }
                case 13:
                {
                    numberEmoji = ":one::three:";
                    break;
                }
                case 14:
                {
                    numberEmoji = ":one::four:";
                    break;
                }
                default:
                {
                    numberEmoji = ":zero:";
                    break;
                }                                                                                                                                                                                
            }
            return numberEmoji;
        }
    }
}