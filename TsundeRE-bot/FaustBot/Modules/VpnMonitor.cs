using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Globalization;
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
        string tsundereJsonPath;
        bool useTsundereLogs;
        GhidraServer serverInstance;
        MessageData tsundereMessages;

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
            
            tsundereJsonPath = config["TsundereJsonPath"];
            useTsundereLogs = bool.Parse(config["UseTsundereLogs"]);

            if (useTsundereLogs)
            {
                if (File.Exists(tsundereJsonPath))
                {
                    tsundereMessages = MessageReader.ReadMessages(tsundereJsonPath);
                }
                else
                {
                    Console.WriteLine($"Tsundere log file not found: {tsundereJsonPath}");
                    useTsundereLogs = false;
                }
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
            var events = await GetEventsAsync(server);
            var tasks = new List<Task>();

            // Sort events by timestamp (ascending order).
            var sortedEvents = events.OrderBy(evt =>
                DateTime.ParseExact(evt.timestamp, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).ToList();

            // Use a StringBuilder to accumulate all event messages.
            StringBuilder sb = new StringBuilder();
            string lastTimestamp = "";

            foreach (var evt in sortedEvents)
            {
                // Filter events by ignoring those from users in the ignore list.
                Match match = Regex.Match(evt.message, @"\((\S+?)@");
                if (match.Success)
                {
                    string username = match.Groups[1].Value;
                    if (ignoreList.Contains(username))
                    {
                        continue; // Skip this event.
                    }
                }

                string newMessage = HandleEventMessage(evt.message);
                // Check if this event's timestamp is the same as the previous one.
                if (!string.IsNullOrEmpty(lastTimestamp) && evt.timestamp == lastTimestamp)
                {
                    // Omit the timestamp if it's identical.
                    sb.AppendLine(newMessage);
                }
                else
                {
                    // Include the timestamp for a new time value.
                    sb.AppendLine($"{evt.timestamp} - {newMessage}");
                    lastTimestamp = evt.timestamp;
                }
            }

            // Send all the accumulated events as one single message.
            string finalMessage = sb.ToString();
            if (!string.IsNullOrEmpty(finalMessage))
            {
                tasks.Add(Context.Client.GetGuild(guildId).GetTextChannel(logChannelId).SendMessageAsync(finalMessage));
            }
            await Task.WhenAll(tasks);
        }


        public string HandleEventMessage(string message)
        {
            string result;
            if (useTsundereLogs)
            {
                string pattern = @"^(?<filePath>[^:]+):\s*(?<content>.*)\s*\((?<username>[^)]+)\)$";
                Match match = Regex.Match(message, pattern);
                if (match.Success)
                {
                    string filePath = match.Groups["filePath"].Value;
                    string content = match.Groups["content"].Value;
                    string username = match.Groups["username"].Value;
                    string newContent = TsundereMessageHelper.GetTsundereMessage(content, tsundereMessages);
                    result = $"{filePath}: {newContent} ({username})";
                }
                else
                {
                    result = message;
                }
            }
            else
            {
                result = message;
            }
            return result;
        }

        public async Task UpdateEmbed(GhidraServer server)
        {

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
            int numUsers = usernames.Where(username => !ignoreList.Contains(username, StringComparer.OrdinalIgnoreCase)).Count();
            fieldName.Append(numUsers.ToString());

            if (numUsers == 1)
            {
                fieldName.Append(" User");
            }
            else
            {
                fieldName.Append(" Users");
            }

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
                try {
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
                catch (Exception ex)
                {
                    server.OnlineStatus = false;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
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

    // Define a class to represent the message arrays.
    public class MessageData
    {
        public List<string> checkoutGrantedMsgs { get; set; }
        public List<string> fileCreatedMsgs { get; set; }
        public List<string> checkoutEndedMsgs { get; set; }
        public List<string> versionCreatedMsgs { get; set; }
        public List<string> notListeningMsgs { get; set; }
        public List<string> handleDisposedMsgs { get; set; }
        public List<string> generatedHandleMsgs { get; set; }
        public List<string> fileDeletedMsgs { get; set; }
        public List<string> checkInStartedMsgs { get; set; }
        public List<string> versionOpenedReadOnlyMsgs { get; set; }
        public List<string> repositoryCreatedMsgs { get; set; }
    }

    public class MessageReader
    {
        // Reads the JSON file and returns a MessageData object.
        public static MessageData ReadMessages(string filePath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                MessageData messages = JsonConvert.DeserializeObject<MessageData>(jsonContent);
                return messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading messages: " + ex.Message);
                return null;
            }
        }
    }

    public class TsundereMessageHelper
    {
        private static Random rnd = new Random();

        // Returns a tsundere message for the given content based on normalized keywords.
        public static string GetTsundereMessage(string content, MessageData messages)
        {
            // Normalize the content:
            // Remove any numbers in parentheses, e.g. "checkout (8) granted" -> "checkout  granted"
            string normalized = Regex.Replace(content, @"\(\d+\)", "");
            // Remove standalone digits (like "version 1 created" -> "version created")
            normalized = Regex.Replace(normalized, @"\b\d+\b", "");
            normalized = normalized.ToLower().Trim();

            // Now match normalized content to categories.
            if (normalized.Contains("checkout") && normalized.Contains("granted"))
            {
                return GetRandomMessage(messages.checkoutGrantedMsgs);
            }
            else if (normalized.Contains("file") && normalized.Contains("created"))
            {
                return GetRandomMessage(messages.fileCreatedMsgs);
            }
            else if (normalized.Contains("checkout") && normalized.Contains("ended"))
            {
                return GetRandomMessage(messages.checkoutEndedMsgs);
            }
            else if (normalized.Contains("version") && normalized.Contains("created"))
            {
                return GetRandomMessage(messages.versionCreatedMsgs);
            }
            else if (normalized.Contains("not listening"))
            {
                return GetRandomMessage(messages.notListeningMsgs);
            }
            else if (normalized.Contains("handle disposed"))
            {
                return GetRandomMessage(messages.handleDisposedMsgs);
            }
            else if (normalized.Contains("generated handle"))
            {
                return GetRandomMessage(messages.generatedHandleMsgs);
            }
            else if (normalized.Contains("file") && normalized.Contains("deleted"))
            {
                return GetRandomMessage(messages.fileDeletedMsgs);
            }
            else if (normalized.Contains("check-in") && normalized.Contains("started"))
            {
                return GetRandomMessage(messages.checkInStartedMsgs);
            }
            else if (normalized.Contains("version") && normalized.Contains("opened read-only"))
            {
                return GetRandomMessage(messages.versionOpenedReadOnlyMsgs);
            }
            else if (normalized.Contains("repository created"))
            {
                return GetRandomMessage(messages.repositoryCreatedMsgs);
            }
            else
            {
                // If no matching category is found, return the original content or a default message.
                return content;
            }
        }

        // Picks a random message from a list.
        private static string GetRandomMessage(List<string> list)
        {
            if (list == null || list.Count == 0)
                return "";
            return list[rnd.Next(list.Count)];
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
