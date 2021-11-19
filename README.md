# melody-bot

#### Melody is a small Discord music player bot written with C# for personal use.

## Features
* Play direct links and youtube text searches
* Queue youtube playlists from urls
* Loop individual tracks or the entire queue
* Automatic disconnection from voice channels during inactivity
* Display a list of the next queued tracks

## Dependencies
* [Discord.Net](https://github.com/discord-net/Discord.Net): Discord API
* [Victoria](https://github.com/Yucked/Victoria): Lavalink client
* [YamlDotNet](https://github.com/aaubry/YamlDotNet): Yaml parsing

Melody uses [Lavalink](https://github.com/freyacodes/Lavalink) to play audio. Lavalink is a standalone java program that must be running for this bot to function.

## Usage

1. Sign in to the [Discord Developer Portal](https://discord.com/developers/) and create an application and bot user.
2. Copy the bot user's token into an environment variable named `DISCORD_TOKEN`
3. Download and install the DotNet 5.0 runtime
4. Download and run a [Lavalink](https://github.com/freyacodes/Lavalink) server, which the bot will connect to in order to play audio
    * I recommend enabling IP rotation and request retrying in `application.yml` to avoid HTTP 403 errors and unexpected bot behavior
5. Configure the bot to match the lavalink server's connection information (If changed from the default)
6. Invite the bot by creating an OAuth2 url with the `bot` scope and some bot permissions, and copy/paste the link into your broswer. It will need permission to
    * Send messages
    * Add reactions
    * Connect to voice channels, speak in them, and deafen itself
    * Send Embedded messages
7. Run the bot and enjoy some music!

## Configuration

Some settings can be changed using a Yaml file named `melody.yml` in the working directory of the program. The configuration file allows you to change the accent color of embedded messages, set the command prefix, and set how long the bot waits before automatically leaving a voice channel when nothing is playing. This file is also where you specify Lavalink connection information.

## Logs

Melody keeps of log of many of the actions it was asked to perform and some track events, primarily for debugging purposes. It outputs the log to the console and writes it to a file in the working directory named `MelodyLog.txt`. 

The logs do not distinguish between actions performed for different servers, so if you encounter unexpected behavior please try to recreate it in a single server first and submit an issue with the log generated then.
