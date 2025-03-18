﻿using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using SoftEther.VPNServerRpc;
using System.Globalization;
using System.Text;
using System.Timers;

namespace FaustBot.Services
{
    public class VpnMonitor : InteractionModuleBase<SocketInteractionContext>
    {
        public InteractionService Commands { get; set; }
        private CommandHandler _handler;

        private static System.Timers.Timer countdownTimer;

        //VpnServerRpc api;
        //string hubName;
        IConfigurationSection hubListConfig;
        IConfigurationSection ignoreListConfig;
        string serverIp;
        string serverPassword;
        List<Hub> hubList = new List<Hub>();
        List<Hub> prevHubList = new List<Hub>();
        ulong guildId;
        ulong logChannelId;
        int delay;
        bool enableLogs;
        ulong embedChannelId;
        bool virtualHubMode;
        List<string> ignoreList = new List<string>();
        string terminalName;
        string selectedTimeZone;
        bool displaySessionTime;
        string titleText;
        string footerText;
        bool mentionUserIds;
        bool useCustomEmojis;
        string hubOnlineEmoji;
        string hubOfflineEmoji;

        //private static Dictionary<string, UserSessionInfo> _currentUsernames = new Dictionary<string, UserSessionInfo>();

        public VpnMonitor(CommandHandler handler, IServiceProvider services, IConfiguration config)
        {
            _handler = handler;
            serverIp = config["VpnServerIp"];
            if (!virtualHubMode)
            {
                serverPassword = config["VpnServerPassword"];
            }
            //hubName = config["VpnHubName"];
            hubListConfig = config.GetSection("VpnHubList");
            virtualHubMode = bool.Parse(config["VirtualHubMode"]);
            for (int i = 0; i < hubListConfig.GetChildren().Count(); i++)
            {
                string hubName = config[$"VpnHubList:{i}"];
                if (virtualHubMode)
                {
                    string hubPassword = config[$"VpnHubPasswords:{i}"];
                    hubList.Add(new Hub(hubName, hubPassword));
                }
                else
                {
                    hubList.Add(new Hub(hubName));
                }
            }
            //prevHubList = hubList;
            guildId = ulong.Parse(config["GuildId"]);
            logChannelId = ulong.Parse(config["LogChannelId"]);
            embedChannelId = ulong.Parse(config["EmbedChannelId"]);
            delay = int.Parse(config["UpdateDelay"]) * 1000;
            enableLogs = bool.Parse(config["EnableLogs"]);
            ignoreListConfig = config.GetSection("IgnoreList");
            for (int i = 0; i < ignoreListConfig.GetChildren().Count(); i++)
            {
                string ignoreItem = config[$"IgnoreList:{i}"];
                ignoreList.Add(ignoreItem);
            }

            terminalName = config["TerminalName"];
            selectedTimeZone = config["TimeZone"];
            displaySessionTime = bool.Parse(config["DisplaySessionTime"]);
            titleText = config["TitleText"];
            footerText = config["FooterText"];
            mentionUserIds = bool.Parse(config["MentionUserIds"]);
            useCustomEmojis = bool.Parse(config["CustomEmojis"]);
            if (useCustomEmojis)
            {
                hubOnlineEmoji = config["HubOnlineEmoji"];
                hubOfflineEmoji = config["HubOfflineEmoji"];
            }

            //api = new VpnServerRpc(serverIp, 443, serverPassword, "");
        }


        [RequireOwner]
        [SlashCommand("start", "Start VPN monitoring service.")]
        public async Task StartVpnMonitor()
        {
            if (countdownTimer != null)
            {
                await RespondAsync("VPN monitoring service is already running.");
                return;
            }

            Console.WriteLine("Starting VPN monitoring service.");

            try
            {
                UpdateHubList();
                await UpdateEmbed();
                SetTimer();
                await RespondAsync("VPN monitoring service started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                await RespondAsync("An error occurred. See console for details.");
            }
        }

        [RequireOwner]
        [SlashCommand("stop", "Stop VPN monitoring service.")]
        public async Task StopVpnMonitor()
        {
            Console.WriteLine("Stopping VPN monitoring service...");
            if (countdownTimer == null)
            {
                await RespondAsync("VPN monitoring service is not running.");
                return;
            }
            DisposeTimer();
            await DeleteEmbed();
            await RespondAsync("VPN monitoring service stopped.");
        }

        [SlashCommand("list", "List current VPN sessions on one hub.")]
        public async Task ListVpnSessions(string hubName)
        {
            Console.WriteLine("Listing current VPN sessions...");
            try
            {
                Hub foundHub = hubList.FirstOrDefault(h => string.Equals(h.HubName, hubName, StringComparison.OrdinalIgnoreCase));
                string output;
                if (foundHub != null)
                {
                    VpnRpcEnumSession out_rpc_enum_session = Get_EnumSession(foundHub);

                    var usernameAndCreatedTimePairs = out_rpc_enum_session.SessionList
                        .Where(session => !ignoreList.Contains(session.Username_str, StringComparer.OrdinalIgnoreCase))
                        .Select(session => new
                        {
                            Username = session.Username_str,
                            CreatedTime = session.CreatedTime_dt
                        })
                        .ToList();

                    TimeZoneInfo sessionTimeZone = TimeZoneInfo.FindSystemTimeZoneById(selectedTimeZone);

                    if (usernameAndCreatedTimePairs.Count == 0)
                    {
                        output = $"No users are currently connected to {foundHub.HubName}.";
                    }
                    else
                    {
                        output = string.Join(Environment.NewLine,
                            usernameAndCreatedTimePairs.Select(pair =>
                            {
                                DateTime sessionTime = TimeZoneInfo.ConvertTimeFromUtc(pair.CreatedTime, sessionTimeZone);
                                string humanReadableTime = sessionTime.ToString("dddd, MMMM dd, h:mm:ss tt", CultureInfo.InvariantCulture);
                                return $"Username: {pair.Username}, Session Created: {humanReadableTime}";
                            }));
                    }

                }
                else
                {
                    output = "Hub not found.";
                }
                Console.WriteLine(output);
                await RespondAsync(output);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                await RespondAsync("An error occurred. See console for details.");
            }
        }

        [SlashCommand("status", "Print VPN hub status.")]
        public async Task VpnStatus(string hubName)
        {
            Console.WriteLine("Printing VPN hub status...");
            try
            {
                Hub foundHub = hubList.FirstOrDefault(h => string.Equals(h.HubName, hubName, StringComparison.OrdinalIgnoreCase));
                if (foundHub != null)
                {
                    VpnRpcHubStatus out_rpc_hub_status = Test_GetHubStatus(foundHub);
                    bool onlineStatus = out_rpc_hub_status.Online_bool;
                    string serverStatus = onlineStatus ? "online" : "offline";
                    string message = $"The {foundHub.HubName} hub is currently {serverStatus}.";
                    Console.WriteLine(message);
                    await RespondAsync(message);
                }
                else
                {
                    Console.WriteLine("Hub not found.");
                    await RespondAsync("Hub not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                await RespondAsync("An error occurred. See console for details.");
            }
        }

        public void UpdateHubList()
        {
            prevHubList = hubList.Select(hub => hub.DeepCopy()).ToList();

            foreach (Hub hub in hubList)
            {
                hub.ClearAllUsernames();
                bool hubStatus = Test_GetHubStatus(hub).Online_bool;
                hub.OnlineStatus = hubStatus;
                VpnRpcEnumSession newVpnRpcEnumSession = Get_EnumSession(hub);

                foreach (var session in newVpnRpcEnumSession.SessionList)
                {
                    UserSessionInfo userSessionInfo = new UserSessionInfo
                    {
                        CreatedTime = session.CreatedTime_dt,
                        LastCommTime = session.LastCommTime_dt
                    };

                    hub.AddUsername(session.Username_str, userSessionInfo);
                }
            }
        }


        public async Task CheckForUserChanges()
        {
            Console.WriteLine("Checking for user changes...");

            TimeZoneInfo sessionTimeZone;
            try
            {
                sessionTimeZone = TimeZoneInfo.FindSystemTimeZoneById(selectedTimeZone);
            }
            catch (Exception)
            {
                Console.WriteLine($"Invalid or missing timezone {selectedTimeZone}. Defaulting to UTC.");
                sessionTimeZone = TimeZoneInfo.Utc;
            }

            var tasks = new List<Task>();

            foreach (Hub hub in hubList)
            {
                string hubName = hub.HubName;
                var currentUsers = hub._userSessions;

                var prevHub = prevHubList.Find(hub => hub.HubName == hubName);
                if (prevHub == null)
                {
                    Console.WriteLine($"No previous hub found for {hubName}, skipping.");
                    continue;
                }
                var prevUsers = prevHub._userSessions;

                foreach (var pair in currentUsers.Where(pair => !prevUsers.ContainsKey(pair.Key)))
                {
                    if (ignoreList.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string message;
                    DateTime sessionTime = TimeZoneInfo.ConvertTimeFromUtc(pair.Value.CreatedTime, sessionTimeZone);
                    string humanReadableTime = sessionTime.ToString("dddd, MMMM dd, h:mm:ss tt", CultureInfo.InvariantCulture);
                    if (mentionUserIds)
                    {
                        message = $"User <@{pair.Key}> has joined the {hubName} hub at {humanReadableTime}.";
                    }
                    else
                    {
                        message = $"User {pair.Key} has joined the {hubName} hub at {humanReadableTime}.";
                    }
                    Console.WriteLine(message);
                    tasks.Add(Context.Client.GetGuild(guildId).GetTextChannel(logChannelId).SendMessageAsync(message));
                }

                foreach (var pair in prevUsers.Where(pair => !currentUsers.ContainsKey(pair.Key)))
                {
                    if (ignoreList.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string message;
                    DateTime sessionTime = TimeZoneInfo.ConvertTimeFromUtc(pair.Value.LastCommTime, sessionTimeZone);
                    string humanReadableTime = sessionTime.ToString("dddd, MMMM dd, h:mm:ss tt", CultureInfo.InvariantCulture);
                    if (mentionUserIds)
                    {
                        message = $"User <@{pair.Key}> has left the {hubName} hub. Last seen at {humanReadableTime}.";
                    }
                    else
                    {
                        message = $"User {pair.Key} has left the {hubName} hub. Last seen at {humanReadableTime}.";
                    }
                    Console.WriteLine(message);
                    tasks.Add(Context.Client.GetGuild(guildId).GetTextChannel(logChannelId).SendMessageAsync(message));
                }
            }

            await Task.WhenAll(tasks);
        }



        public async Task UpdateEmbed()
        {
            //List<Hub> hubList = new List<Hub>();

            var embed = new EmbedBuilder
            {
                // Embed property can be set within object initializer
                Title = titleText,
            };

            foreach (Hub hub in hubList)
            {
                string hubName = hub.HubName;
                string serverStatus;
                if (useCustomEmojis)
                {
                    serverStatus = hub.OnlineStatus ? hubOnlineEmoji : hubOfflineEmoji;
                }
                else
                {
                    serverStatus = hub.OnlineStatus ? "[Online]" : "[Offline]";
                }
                var usernames = hub.Usernames;

                StringBuilder userList = new StringBuilder("", 200);

                if (hub.OnlineStatus)
                {
                    foreach (var username in usernames)
                    {
                        if (!ignoreList.Contains(username, StringComparer.OrdinalIgnoreCase))
                        {
                            if (mentionUserIds)
                            {
                                userList.Append("<@");
                                userList.Append(username);
                                userList.Append('>');
                            }
                            else
                            {
                                userList.Append(username);

                            }
                            if (displaySessionTime)
                            {
                                DateTime utcDate = DateTime.UtcNow;
                                DateTime sessionTime = hub._userSessions[username].CreatedTime;
                                TimeSpan duration = utcDate.Subtract(sessionTime);
                                string humanReadableDuration = string.Format(CultureInfo.InvariantCulture, "{0}:{1:D2}:{2:D2}",
                                    (int)duration.TotalHours, duration.Minutes, duration.Seconds);
                                userList.Append(" - ");
                                userList.Append(humanReadableDuration);

                            }
                            userList.Append('\n');
                        }
                    }
                    if (usernames.Count == 0 || userList.Length == 0)
                    {
                        userList.Append("No Players");
                    }
                }
                else
                {
                    userList.Append("Hub Offline");
                }

                StringBuilder fieldName = new StringBuilder("", 50);
                fieldName.Append(serverStatus);
                fieldName.Append(' ');
                fieldName.Append(hubName);
                fieldName.Append(": ");
                fieldName.Append(usernames.Where(username => !ignoreList.Contains(username, StringComparer.OrdinalIgnoreCase)).Count().ToString());
                fieldName.Append("/4 Players");
                if (usernames.Contains(terminalName, StringComparer.OrdinalIgnoreCase))
                {
                    fieldName.Append(" :regional_indicator_d::regional_indicator_t:");
                }
                embed.AddField(fieldName.ToString(), userList.ToString());
            }

            // Or with methods
            //embed.AddField("Field title",
            //    "Field value. I also support [hyperlink markdown](https://example.com)!")
            //    .WithAuthor(Context.Client.CurrentUser)
            //    .WithFooter(footer => footer.Text = "I am a footer.")
            //    .WithColor(Color.Blue)
            //    .WithTitle("I overwrote \"Hello world!\"")
            //    .WithDescription("I am a description.")
            //    .WithUrl("https://example.com")
            //    .WithCurrentTimestamp();

            embed.WithColor(Color.Green);
            embed.WithCurrentTimestamp();
            embed.WithFooter(footer => footer.Text = footerText);

            Console.WriteLine($"Sending embed to guild {guildId}, channel {embedChannelId}");

            await Context.Client.GetGuild(guildId).GetTextChannel(embedChannelId).SendMessageAsync(embed: embed.Build());
        }

        public async Task DeleteEmbed()
        {
            var messageToDelete = await Context.Client.GetGuild(guildId).GetTextChannel(embedChannelId).GetMessagesAsync(limit: 1).FlattenAsync();
            await Context.Client.GetGuild(guildId).GetTextChannel(embedChannelId).DeleteMessagesAsync(messageToDelete);
        }


        private void SetTimer()
        {
            countdownTimer = new System.Timers.Timer
            {
                Interval = delay,
                AutoReset = true,
                Enabled = true
            };
            countdownTimer.Elapsed += OnTimedEvent;
        }

        private void DisposeTimer()
        {
            countdownTimer.Stop();
            countdownTimer.Dispose();
            countdownTimer = null;
        }

        private async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            //VpnRpcEnumSession in_rpc_enum_session = new VpnRpcEnumSession()
            //{
            //    HubName_str = hubName,
            //};
            //VpnRpcEnumSession out_rpc_enum_session = api.EnumSession(in_rpc_enum_session);

            try
            {
                UpdateHubList();
                if (enableLogs)
                {
                    await CheckForUserChanges();
                }

                await DeleteEmbed();
                await UpdateEmbed();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        public VpnRpcEnumSession Get_EnumSession(Hub hub)
        {
            //Console.WriteLine("Begin: Test_EnumSession");

            VpnRpcEnumSession in_rpc_enum_session = new VpnRpcEnumSession()
            {
                HubName_str = hub.HubName,
            };
            VpnServerRpc api = GetApi(hub);
            VpnRpcEnumSession out_rpc_enum_session = api.EnumSession(in_rpc_enum_session);

            //print_object(out_rpc_enum_session);

            //Console.WriteLine("End: Test_EnumSession");
            //Console.WriteLine("-----");
            //Console.WriteLine();

            return out_rpc_enum_session;
        }

        public VpnRpcHubStatus Test_GetHubStatus(Hub hub)
        {
            //Console.WriteLine("Begin: Test_GetHubStatus");

            VpnRpcHubStatus in_rpc_hub_status = new VpnRpcHubStatus()
            {
                HubName_str = hub.HubName,
            };
            VpnServerRpc api = GetApi(hub);
            VpnRpcHubStatus out_rpc_hub_status = api.GetHubStatus(in_rpc_hub_status);

            return(out_rpc_hub_status);

            //Console.WriteLine("End: Test_GetHubStatus");
            //Console.WriteLine("-----");
            //Console.WriteLine();
        }

        public VpnServerRpc GetApi(Hub hub)
        {
            VpnServerRpc api;
            if (virtualHubMode)
            {
                api = new VpnServerRpc(serverIp, 443, hub.HubPassword, hub.HubName);
            }
            else
            {
                api = new VpnServerRpc(serverIp, 443, serverPassword, "");
            }
            return api;
        }

        public void print_object(object obj)
        {
            var setting = new Newtonsoft.Json.JsonSerializerSettings()
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Include,
                ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Error,
            };
            string str = Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented, setting);
            Console.WriteLine(str);
        }
    }

    public class UserSessionInfo
    {
        public DateTime CreatedTime { get; set; }
        public DateTime LastCommTime { get; set; }
    }

    public class Hub
    {
        public string HubName { get; set; }
        public string HubPassword { get; set; }
        public bool OnlineStatus { get; set; }
        public Dictionary<string, UserSessionInfo> _userSessions;

        // Constructor
        public Hub(string hubName, string hubPassword = "")
        {
            HubName = hubName;
            HubPassword = hubPassword;
            OnlineStatus = false;
            _userSessions = new Dictionary<string, UserSessionInfo>();
        }

        // Property to access usernames as a list
        public List<string> Usernames => _userSessions.Keys.ToList();

        // Method to add a username with session info
        public void AddUsername(string username, UserSessionInfo session)
        {
            if (!string.IsNullOrEmpty(username) && !_userSessions.ContainsKey(username))
            {
                _userSessions.Add(username, session);
            }
        }

        // Method to clear all usernames and their associated session info
        public void ClearAllUsernames()
        {
            _userSessions.Clear();
        }

        // Method to create a deep copy of the Hub object
        public Hub DeepCopy()
        {
            var newHub = new Hub(this.HubName, this.HubPassword)
            {
                OnlineStatus = this.OnlineStatus,
                _userSessions = this._userSessions.ToDictionary(
                    entry => entry.Key,
                    entry => new UserSessionInfo
                    {
                        CreatedTime = entry.Value.CreatedTime,
                        LastCommTime = entry.Value.LastCommTime
                    }
                )
            };
            return newHub;
        }
    }
}
