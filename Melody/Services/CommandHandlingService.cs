using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Melody.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        // Retrieve references to the client and command service
        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
        }

        /// <summary>
        /// Register the program's command modules with the Discord.net command service
        /// </summary>
        /// <returns></returns>
        public async Task InstallCommandsAsync()
        {
            // Hook the MessageRecieved event to the command handler
            _client.MessageReceived += HandleCommandAsync;

            await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(), services: _services);
        }

        private async Task HandleCommandAsync(SocketMessage socketMessage)
        {
            var message = (SocketUserMessage)socketMessage;
            // Dont process system messages
            if (message == null) return;

            // Create a number to track where the prefix ends and the command begins
            var prefixEndPosition = 0;

            // Determine if the message is a command
            // Check for the command prefix and don't allow bots trigger commands
            if (!(message.HasStringPrefix(ConfigurationService.CommandPrefix, ref prefixEndPosition) ||
            message.HasMentionPrefix(_client.CurrentUser, ref prefixEndPosition)) ||
            message.Author.IsBot)
            {
                return;
            }

            // Create a command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command
            await _commands.ExecuteAsync(
                context: context,
                argPos: prefixEndPosition,
                services: _services);
        }

    }
}