# TsundeRE

Full Discord integration for Ghidra servers. Can display all users currently connected to the server on both a Discord bot and Rich Presence.

### TsundeRE Server

Required for the Client or Bot to function. Serves an API on port `5000` (by default) that allows you to get `/users` and `/events` from the Ghidra server. Requires Flask to run.

Change the passphrase in `log-server.py`.

Usage:

```
sudo python3 log-server.py
```

### TsundeRE Client

Ghidra plugin that provides Discord Rich Presence and displays other users connected to the server. Fork of Ghidracord.

You need to set your API url, username, and passphrase in `tsundere.properties` for full functionality. Place the file in your Ghidra root directory.

Note: Other users connecting to the server **do not** need to have TsundeRE Client installed in order for it to function for you.

### TsundeRE Bot

Discord bot that provides status updates on the server and maybe even more functionality. Fork of FaustBot.

**Not implemented yet.**