using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace FaustBot.Services
{
    public class VpnMonitor : InteractionModuleBase<SocketInteractionContext>
    {
        public InteractionService Commands { get; set; }
        private CommandHandler _handler;

        private static System.Timers.Timer countdownTimer;

        string serverName;
        IConfigurationSection ignoreListConfig;
        string serverIp;
        string serverPassword;
        int serverPort;
        ulong guildId;
        ulong logChannelId;
        int delay;
        bool enableLogs;
        ulong embedChannelId;
        List<string> ignoreList = new List<string>();
        string titleText;
        string footerText;
        bool useCustomEmojis;
        string hubOnlineEmoji;
        string hubOfflineEmoji;
        GhidraServer serverInstance;

        public VpnMonitor(CommandHandler handler, IServiceProvider services, IConfiguration config)
        {
            _handler = handler;
            serverName = config["GhidraServerName"];
            serverIp = config["GhidraServerIp"];
            serverPassword = config["GhidraServerPassword"];
            serverPort = int.Parse(config["GhidraServerPort"]);
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

            titleText = config["TitleText"];
            footerText = config["FooterText"];
            useCustomEmojis = bool.Parse(config["CustomEmojis"]);
            if (useCustomEmojis)
            {
                hubOnlineEmoji = config["HubOnlineEmoji"];
                hubOfflineEmoji = config["HubOfflineEmoji"];
            }

            serverInstance = new GhidraServer(serverName, serverIp, serverPort, serverPassword);
        }


        [RequireOwner]
        [SlashCommand("start", "Start Ghidra server monitoring service.")]
        public async Task StartVpnMonitor()
        {
            if (countdownTimer != null)
            {
                await RespondAsync("Ghidra server monitoring service is already running.");
                return;
            }

            Console.WriteLine("Starting Ghidra server monitoring service.");

            try
            {
                await UpdateServerStatus(serverInstance);
                await UpdateEmbed(serverInstance);
                SetTimer();
                await RespondAsync("Ghidra server monitoring service started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                await RespondAsync("An error occurred. See console for details.");
            }
        }

        [RequireOwner]
        [SlashCommand("stop", "Stop Ghidra server monitoring service.")]
        public async Task StopVpnMonitor()
        {
            Console.WriteLine("Stopping Ghidra server monitoring service...");
            if (countdownTimer == null)
            {
                await RespondAsync("Ghidra server monitoring service is not running.");
                return;
            }
            DisposeTimer();
            await DeleteEmbed();
            await RespondAsync("Ghidra server monitoring service stopped.");
        }

        [SlashCommand("list", "List current Ghidra sessions on the server.")]
        public async Task ListVpnSessions()
        {
            GhidraServer server = serverInstance;
            Console.WriteLine("Listing current Ghidra sessions...");

            try
            {
                string output;
                server.ClearAllUsernames();
                server._userSessions = await GetUsersAsync(server);
                List<string> usernames = server._userSessions;
                if (usernames.Count == 0)
                {
                    output = $"No users are currently connected to {server.ServerName}.";
                }
                else
                {
                    output = $"Users currently connected to {server.ServerName}: {string.Join(", ", usernames)}";
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

        public async Task UpdateServerStatus(GhidraServer server)
        {
            server.ClearAllUsernames();
            server._userSessions = await GetUsersAsync(server);
        }


        public async Task CheckForEvents(GhidraServer server)
        {
            Console.WriteLine("Checking for events...");

            var tasks = new List<Task>();

            var events = await GetEventsAsync(server);

            foreach (var evt in events)
            {
                Match match = Regex.Match(evt.message, @"\((\S+?)@");
                if (match.Success)
                {
                    string username = match.Groups[1].Value;
                    if (ignoreList.Contains(username))
                    {
                        continue; // Skip this event if the username is in the ignore list.
                    }
                }

                string message = $"{evt.timestamp} - {evt.message}";
                Console.WriteLine(message);
                tasks.Add(Context.Client.GetGuild(guildId).GetTextChannel(logChannelId).SendMessageAsync(message));
            }
            await Task.WhenAll(tasks);
        }

        public async Task UpdateEmbed(GhidraServer server)
        {
            //List<Hub> hubList = new List<Hub>();

            var embed = new EmbedBuilder
            {
                // Embed property can be set within object initializer
                Title = titleText,
            };


            string hubName = server.ServerName;
            string serverStatus;
            if (useCustomEmojis)
            {
                serverStatus = server.OnlineStatus ? hubOnlineEmoji : hubOfflineEmoji;
            }
            else
            {
                serverStatus = server.OnlineStatus ? "[Online]" : "[Offline]";
            }
            var usernames = server._userSessions;

            StringBuilder userList = new StringBuilder("", 200);

            if (server.OnlineStatus)
            {
                foreach (var username in usernames)
                {
                    if (!ignoreList.Contains(username, StringComparer.OrdinalIgnoreCase))
                    {
                        userList.Append(username);
                        userList.Append('\n');
                    }
                }
                if (usernames.Count == 0 || userList.Length == 0)
                {
                    userList.Append("No Users");
                }
            }
            else
            {
                userList.Append("Server Offline");
            }

            StringBuilder fieldName = new StringBuilder("", 50);
            fieldName.Append(serverStatus);
            fieldName.Append(' ');
            fieldName.Append(hubName);
            fieldName.Append(": ");
            fieldName.Append(usernames.Where(username => !ignoreList.Contains(username, StringComparer.OrdinalIgnoreCase)).Count().ToString());
            fieldName.Append(" User(s)");

            embed.AddField(fieldName.ToString(), userList.ToString());


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

            try
            {
                await UpdateServerStatus(serverInstance);
                if (enableLogs)
                {
                    await CheckForEvents(serverInstance);
                }
                await DeleteEmbed();
                await UpdateEmbed(serverInstance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }


        // Function to call the /users endpoint and return a list of users.
        public static async Task<List<string>> GetUsersAsync(GhidraServer server)
        {
            string ip = server.ServerIp;
            int port = server.ServerPort;
            string passphrase = server.ServerPassword;
            string url = $"http://{ip}:{port}/users";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Passphrase", passphrase);
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    server.OnlineStatus = true;
                    Console.WriteLine("API users call succeeded.");
                    string json = await response.Content.ReadAsStringAsync();
                    var usersResponse = JsonConvert.DeserializeObject<UsersResponse>(json);
                    return usersResponse?.users ?? new List<string>();
                }
                else
                {
                    server.OnlineStatus = false;
                    Console.WriteLine("API users call failed with status code: " + response.StatusCode);
                    return new List<string>();
                }
            }
        }

        // Function to call the /events endpoint and return a list of events.
        public static async Task<List<EventItem>> GetEventsAsync(GhidraServer server)
        {
            string ip = server.ServerIp;
            int port = server.ServerPort;
            string passphrase = server.ServerPassword;
            string url = $"http://{ip}:{port}/events";
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Passphrase", passphrase);
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("API events call succeeded.");
                    string json = await response.Content.ReadAsStringAsync();
                    var eventsResponse = JsonConvert.DeserializeObject<EventsResponse>(json);
                    return eventsResponse?.events ?? new List<EventItem>();
                }
                else
                {
                    Console.WriteLine("API events call failed with status code: " + response.StatusCode);
                    return new List<EventItem>();
                }
            }
        }
    }



    public class GhidraServer
    {
        public string ServerName { get; set; }
        public string ServerIp { get; set; }
        public int ServerPort { get; set; }
        public string ServerPassword { get; set; }
        public bool OnlineStatus { get; set; }

        public List<string> _userSessions;

        // Constructor
        public GhidraServer(string name, string ip, int port, string password)
        {
            ServerName = name;
            ServerPort = port;
            ServerIp = ip;
            ServerPassword = password;
            OnlineStatus = false;
            _userSessions = new List<string>();
        }

        // Method to add a username with session info
        public void AddUsername(string username)
        {
            if (!string.IsNullOrEmpty(username) && !_userSessions.Contains(username))
            {
                _userSessions.Add(username);
            }
        }

        // Method to clear all usernames and their associated session info
        public void ClearAllUsernames()
        {
            _userSessions.Clear();
        }
    }

    // Class representing an event item from the API response
    public class EventItem
    {
        public string timestamp { get; set; }
        public string message { get; set; }
    }

    // Class representing the /users API response
    public class UsersResponse
    {
        public List<string> users { get; set; }
    }

    // Class representing the /events API response
    public class EventsResponse
    {
        public List<EventItem> events { get; set; }
    }
}
