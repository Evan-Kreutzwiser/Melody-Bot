using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Melody.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Victoria;

namespace Melody
{
    public sealed class Program
    {
        private readonly Logger _logger = new("Discord");

        private ServiceProvider _services;

        public static void Main()
        {
            // Read the bot's settings from the configuration file
            ConfigurationService.PrepareConfigurationService();

            // Run the bot
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            // Dispose of the service provider when we're done using it
            // at the end of the programs lifetime
            _services = ConfigureServices();

            // Create the client object
            var client = _services.GetRequiredService<DiscordSocketClient>();

            // Register the console log printer with the Discord.Net client
            client.Log += LogAsync;
            client.Ready += OnClientReady;

            // Get the discord bot token
            string token = null;
            try
            {
                token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error getting bot token from environment variable!\n" + exception.Message);
                Environment.Exit(-1); // Quit because the bot cannot be run without an api token
            }

            // Quit if the token was not successfully read
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Error! Bot token not found.\nPlease set the value of the \"DISCORD_TOKEN\" environment variable to the bot user's token.");
                Environment.Exit(-1); // Quit because the bot cannot be run without an api token
            }

            // Login to discord as a bot
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await _services.GetRequiredService<CommandHandlingService>().InstallCommandsAsync();

            // Display the default status, containing the help command
            await client.SetActivityAsync(new Game("-help", ActivityType.Listening));

            // Block this task until the program is closed.
            await Task.Delay(Timeout.Infinite);
        }

        /// <summary>
        /// Runs when the discord client is ready to connect the lavalink client to the server
        /// </summary>
        /// <returns></returns>
        private async Task OnClientReady()
        {
            // Avoid calling ConnectAsync again if it's already connected 
            // (It throws InvalidOperationException if it's already connected).
            var lavaNode = _services.GetRequiredService<LavaNode>();
            if (!lavaNode.IsConnected)
            {
                await _services.GetRequiredService<LavaNode>().ConnectAsync();
                _services.GetRequiredService<AudioService>().AddEventHandlers();
            }
        }

        /// <summary>
        /// Log messages emitted by the discord connection manager
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private Task LogAsync(LogMessage message)
        {
            _logger.Log(message.Message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Prepare a service provider for dependency injection
        /// </summary>
        /// <returns></returns>
        private static ServiceProvider ConfigureServices()
        {
            // Add every service and the lavalink connection handler to a service provider
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<AudioService>()
                .AddLavaNode(x =>
                {
                    // Use the lavalink connection information read from the configuration file
                    x.Port = ConfigurationService.LavalinkPort;
                    x.Hostname = ConfigurationService.LavalinkHostname;
                    x.Authorization = ConfigurationService.LavalinkPassword;
                })
                .BuildServiceProvider();
        }
    }
}