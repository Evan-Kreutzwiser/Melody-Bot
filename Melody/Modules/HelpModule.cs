using Discord;
using Discord.Commands;
using Melody.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Melody.Modules
{
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;

        public HelpModule(CommandService commandService)
            => _commandService = commandService;

        /// <summary>
        /// Sometimes when a bot crashes it can stay in a voice channel and/or continue to appear online.
        /// This command is for checking if the bo is still running if it becomes unresponive or stops playing audio.
        /// </summary>
        /// <returns></returns>
        [Command("Ping")]
        [Summary("Check if I'm online.")]
        public async Task PingAsync()
        {
            await ReplyAsync("I'm alive :)");
        }

        /// <summary>
        /// Generate and display a list of every command, their alias, and their descriptions
        /// </summary>
        /// <returns></returns>
        [Command("Help")]
        [Summary("Display this help message.")]
        public async Task HelpAsync()
        {
            // Obtain a list of all registered commands and start creating an embedded message
            var commandList = _commandService.Commands.ToList();
            var embedBuilder = new EmbedBuilder().WithColor(ConfigurationService.EmbedColor);

            // Add each registered command to the list in the embedded message
            foreach (CommandInfo command in commandList)
            {
                // Get the command Summary attribute information
                var embedFieldText = command.Summary ?? "No description available\n";
                var embedFieldTitle = command.Name;
                // Every command with an alias seems to also be given one that is just the normal command in lowercase
                // So index 1 in used instead, since it contains the actual alias
                if (command.Aliases.Count > 1)
                {
                    // Add the alias to the list field if present
                    embedFieldTitle += $" ({command.Aliases[1]})";
                }
                embedBuilder.AddField(embedFieldTitle, embedFieldText);
            }

            // Send the embedded message
            await ReplyAsync("Here's a list of commands and their descriptions: ", false, embedBuilder.Build());
        }
    }
}