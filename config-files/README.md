# TsundeRE Config Files

These are sample configuration files.

### tsundere.properties

For the client. Goes in the Ghidra root directory.

```
api.url=http://localhost:5000/users
username=myUsername
x.passphrase=mySecretPassphrase
```

`api.url` - URL of the API being served by your TsundeRE server, with hostname, port, and `/users` at the end.

`username` - Your Ghidra server username.

`x.passphrase` - The API passphrase you set in `log-server.py`.

### config.json

Goes in the same directory as `tsunderebot.exe`.

```
{
    "Token": "PutYourBotTokenHere",
    "GuildId": "1234567890",
    "LogChannelId": "1234567890",
    "EmbedChannelId": "1234567890",
    "EnableLogs": "false",
    "UpdateDelay": "60",
    "GhidraServerName": "TsundeRE-A",
    "GhidraServerIp": "123.456.789.10",
    "GhidraServerPort": "5000",
    "GhidraServerPassword": "mySecretPassphrase",
    "IgnoreList": [],
    "TitleText": "Ghidra Server Status",
    "FooterText": "Updates every minute!",
    "CustomEmojis": "false",
    "HubOnlineEmoji": "<:emoji_ok:1234567890>",
    "HubOfflineEmoji": "<:emoji_ng:1234567890>"
    "UseTsundereLogs": "false",
    "TsundereJsonPath": "tsundere.json"
}
```

`Token` - Your bot token.

`GuildId` - Your server ID. (Get it by using Developer Mode)

`LogChannelId` - Channel ID to send the logs to.

`EmbedChannelId` - Channel ID to send the persistent embed to.

`EnableLogs` - Whether to print Ghidra server event logs.

`UpdateDelay` - How many seconds to wait before updating the embed.

`GhidraServerName` - Nickname for the server, displayed in the embed.

`GhidraServerIp` - Ghidra server IP/hostname.

`GhidraServerPort` - Should match the port you set in `log-server.py`.

`GhidraServerPassword` - The API passphrase you set in `log-server.py`.

`IgnoreList` - List of usernames to exclude from logging.

`TitleText` - The persistent embed's title text.

`FooterText` - The persistent embed's footer text.

`CustomEmojis` - Whether to use custom emojis to display the Ghidra server online/offline status.

`HubOnlineEmoji` - Emoji to use for an online server. (If `CustomEmojis` is enabled)

`HubOfflineEmoji` - Emoji to use for an offline server. (If `CustomEmojis` is enabled)

`UseTsundereLogs` - Whether to use random custom log messages.

`TsundereJsonPath` - Path to the json containing all custom log messages. Only required if `UseTsundereLogs` is true.

### tsundere.json

Goes in the same directory as `tsunderebot.exe`. You can edit it if you want different custom messages.