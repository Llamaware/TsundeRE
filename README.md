# TsundeRE

Full Discord integration for Ghidra servers.

### TsundRE Server

Required for the Client or Bot to function. Serves an API on port 5000 (by default) that allows you to get users and recent events from the Ghidra server.

### TsundRE Client

Plugin that provides Discord Rich Presence and displays other users connected to the server. Fork of Ghidracord.

You need to set your API url, username, and passphrase in `tsundere.properties` for full functionality. Place the file in your Ghidra root directory.

### TsundRE Bot

Discord bot that provides status updates on the server and maybe even more functionality. Fork of FaustBot.