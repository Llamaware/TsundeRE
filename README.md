# TsundeRE

Full Discord integration for Ghidra servers. Can display all users currently connected to the server on both a Discord bot and Rich Presence.

### TsundeRE Server

Required for the Client or Bot to function. Serves an API on port `5000` (by default) that allows you to get `/users` and `/events` from the Ghidra server. Requires Flask to run.

Change the passphrase and log file location in `log-server.py`. Sudo is required to read from the `server.log` file.

Usage:

```
sudo python3 log-server.py
```

### TsundeRE Client

Ghidra plugin that provides Discord Rich Presence and displays other users connected to the server. Fork of [Ghidracord](https://github.com/KawaiiFiveO/ghidracord).

You need to set your API URL, username, and passphrase in `tsundere.properties` for full functionality. Place the file in your Ghidra root directory.

Note: Other users connecting to the server **do not** need to have TsundeRE Client installed in order for it to function for you.

### TsundeRE Bot

Discord bot that provides status updates on the server and maybe even more functionality in the future. Fork of FaustBot.

Usage and functionality are similar to [FaustBot](https://github.com/Llamaware/FaustBot). Read FaustBot's README for more details.

`config.json` needs to be in the same directory as the bot.

### Licenses

TsundeRE Server and TsundeRE Bot are licensed under the Tsundere Public License, but TsundeRE Client is licensed under the Apache License 2.0.