using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Net;
using Discord;
using Discord.Commands;
using NinjaBotCore.Database;
using NinjaBotCore.Models.Steam;
using NinjaBotCore.Modules.Steam;
using Discord.WebSocket;
using NinjaBotCore.Models.RocketLeague;
using NinjaBotCore.Modules.RocketLeague;
using Microsoft.Extensions.Configuration;
using NinjaBotCore.Services;
using RLSApi;
using RLSApi.Data;
using RLSApi.Net.Requests;
using static NinjaBotCore.Services.NinjaExtensions;

namespace NinjaBotCore.Modules.RocketLeague
{
    public class RocketLeagueCommands : ModuleBase
    {
        private SteamApi _steam;
        private static ChannelCheck _cc;
        private string _rlStatsKey;
        private readonly IConfigurationRoot _config;
        private string _prefix;
        private RLSClient _rlsClient; 

        public RocketLeagueCommands(SteamApi steam, ChannelCheck cc, IConfigurationRoot config)
        {          
            _steam = steam;                           
            _cc = cc;                            
            _config = config;
            _rlStatsKey = $"{_config["RlStatsApi"]}";
            _prefix = _config["prefix"];
            _rlsClient = new RLSClient(_rlStatsKey);
        }

        [Command("rlstats-set", RunMode = RunMode.Async)]
        public async Task RlStatsSet([Remainder] string args)
        {
            string rlUserName = string.Empty;
            string platform = string.Empty;

            //check if we have a url
            if (Uri.IsWellFormedUriString(args, UriKind.Absolute))
            {
                rlUserName = args.TrimEnd('/');
                rlUserName = rlUserName.Substring(rlUserName.LastIndexOf('/') + 1);
                platform = "steam";
                await _cc.Reply(Context,rlUserName);
                await SetStats(rlUserName, platform);
                return;
            }
            else 
            {
                //split up the arguments and see how many there are
                if (args.Split(" ").Count() >= 2)
                {
                    platform = args.FirstFromSplit(' ');
                    rlUserName = args.OmitFirstFromSplit(" ");
                    await SetStats(rlUserName, platform);
                    await _cc.Reply(Context,$"platform: {platform} user: {rlUserName}");
                }
                else 
                {
                    await _cc.Reply(Context,$"Please enter a valid steam URL -or- use the syntax {_prefix}rlstats-set platform(steam, ps4, xbox) playername");
                }
            }
        }

        public async Task SetStats(string name, string platform)
        {
            try
            {            
                string channel = Context.Channel.Name;
                string userName = Context.User.Username;
                ulong discordUserId = Context.User.Id;
                StringBuilder sb = new StringBuilder();

                using (var db = new NinjaBotEntities())
                {
                    switch (platform.ToLower())
                    {                    
                        case "steam":
                        {
                            var getPlayer = await _rlsClient.GetPlayerAsync(RlsPlatform.Steam, name);
                            try
                            {
                                var addUser = new RlStat();
                                var rlUser = db.RlStats.Where(r => (ulong)r.DiscordUserID == discordUserId).FirstOrDefault();
                                if (rlUser == null)
                                {
                                    addUser.DiscordUserName = userName;
                                    addUser.DiscordUserID = (long)discordUserId;                            
                                    addUser.SteamID = long.Parse(getPlayer.UniqueId);
                                    addUser.DiscordUserID = (long)Context.User.Id;
                                    addUser.Platform = RlsPlatform.Steam.ToString();
                                    addUser.RlPlayerName = getPlayer.DisplayName;
                                    db.RlStats.Add(addUser);
                                }
                                else
                                {
                                    rlUser.SteamID = long.Parse(getPlayer.UniqueId);
                                    rlUser.DiscordUserID = (long)discordUserId;
                                    rlUser.RlPlayerName = getPlayer.DisplayName;
                                    rlUser.Platform = RlsPlatform.Steam.ToString();
                                }
                                db.SaveChanges();
                                sb.AppendLine($"{Context.User.Mention}, you've associated [**{getPlayer.DisplayName}**(steam)] with your Discord name!");
                                await _cc.Reply(Context, sb.ToString());
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"RL Stats: Error setting name -> {ex.Message}");
                                sb.AppendLine($"{Context.User.Mention}, something went wrong, sorry :(");
                                await _cc.Reply(Context, sb.ToString());
                            }
                            break;
                        }
                    }                                        
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }
    }

}