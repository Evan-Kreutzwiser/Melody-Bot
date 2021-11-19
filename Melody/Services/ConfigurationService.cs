using Discord;
using System;
using System.Drawing;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace Melody.Services
{

    public static class ConfigurationService
    {
        // Reactions for indicating operation status
        public static readonly Emoji SuccessReaction = new("👍🏻"); // Thumbs up light skin tone

        // Lavalink connection information
        public static ushort LavalinkPort { get; private set; } = 2333;
        public static string LavalinkHostname { get; private set; } = "localhost";
        public static string LavalinkPassword { get; private set; } = "youshallnotpass";

        // Bot configuration
        public static Discord.Color EmbedColor { get; private set; } = (Discord.Color)ColorTranslator.FromHtml("#ED9121"); // Accent color for embedded messages
        public static string CommandPrefix { get; private set; } = "-";
        public static TimeSpan AutoDisconnectTimespan { get; private set; } = TimeSpan.FromSeconds(20); // Disconnect after this length of inactivity in a voice channel

        private static readonly Logger _logger = new("Config");

        /// <summary>
        /// Read the configuration file and apply the settings read from it
        /// </summary>
        public static void PrepareConfigurationService()
        {
            // Read the contents of the configuration file
            // If the file cannot be opened, the default values are used and a warning is printed
            string configurationFileContents;
            try
            {
                configurationFileContents = File.ReadAllText("melody.yml");
            }
            catch (Exception exception)
            {
                // If the file wasn't found, print a special message about it
                if (exception is FileNotFoundException)
                {
                    _logger.Log("Configuration file not found! Please make sure the working directory contains \"melody.yml\"");
                }
                // Otherwise print verbose error information
                else
                {
                    // Print information about the error
                    _logger.Log("Error reading configuration file!\n" + exception.Message);
                    _logger.Log("Please make sure that the program has permission to read the configuration file");
                }

                // Dont try to change any of the default settings if the file couldn't be read
                // Just continue running the bot's default settings
                _logger.Log("Using default settings");
                return;
            }

            // Create the yaml stream from the file contents
            var yamlStream = new YamlStream();
            yamlStream.Load(new StringReader(configurationFileContents));

            // Get the root of the file
            var rootNode = (YamlMappingNode)yamlStream.Documents[0].RootNode;

            var usedADefaultValue = false; // Track whether a warning about configuration file errors should be printed

            // I create a temporary variable for each setting to work around the inability to pass properties as reference parameters
            // This way I can maintain the properties access priviledges and still use my `TryLoadSettingsKey` method

            // Check if the file contains lavalink connection information
            if (rootNode.Children.TryGetValue("lavalink", out var lavalinkNode))
            {
                // Try getting the connection port number
                var port = LavalinkPort;
                if (!TryLoadSettingsKey(lavalinkNode, "port", ref port))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default lavalink port number ({LavalinkPort})");
                }

                // Try getting the lavalink server hostname
                var hostname = LavalinkHostname;
                if (!TryLoadSettingsKey(lavalinkNode, "hostname", ref hostname))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default lavalink hostname ({LavalinkHostname})");
                }

                // Try getting the lavalink server password
                var password = LavalinkPassword;
                if (!TryLoadSettingsKey(lavalinkNode, "password", ref password))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default lavalink password ({LavalinkPassword})");
                }

                // Store the loaded settings
                LavalinkPort = port;
                LavalinkHostname = hostname;
                LavalinkPassword = password;
            }
            else
            {
                usedADefaultValue = true;
                _logger.Log($"Using default lavalink connection information");
            }

            // Check if the file contains bot configuration settings
            if (rootNode.Children.TryGetValue("melody", out var botNode))
            {
                // Try getting embedded message color
                var colorHexCode = "";
                if (!TryLoadSettingsKey(botNode, "color", ref colorHexCode))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default color for embedded messages ({EmbedColor})");
                }
                // If the value was read successfully convert the color to a discord color object
                else
                {
                    EmbedColor = (Discord.Color)ColorTranslator.FromHtml(colorHexCode);
                }

                // Try getting the command prefix
                var prefix = CommandPrefix;
                if (!TryLoadSettingsKey(botNode, "prefix", ref prefix))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default command prefix ({CommandPrefix})");
                }
                CommandPrefix = prefix;

                // Try getting the autodisconnect time
                var seconds = 0;
                if (!TryLoadSettingsKey(botNode, "autoDisconnectTime", ref seconds))
                {
                    usedADefaultValue = true;
                    _logger.Log($"Using default autodisconnect time ({AutoDisconnectTimespan.Seconds})");
                }
            }
            // If the Melody settings are not present
            else
            {
                usedADefaultValue = true;
                _logger.Log($"Using default bot configuration");
            }

            // If any settings were not read from the file, print a warning urging the user to fix the file
            if (usedADefaultValue)
            {
                _logger.Log("Warning: Not all settings successfully read from configuration file. Please check and fix the configuration file (\"melody.yml\")");
            }
            // Otherwise report that the settings were applied correctly
            else
            {
                _logger.Log("Successfully applied settings from configuration file");
            }
        }

        /// <summary>
        /// Attempt to load the value of a yaml node's child to a variable. 
        /// This does not overwrite the variable's existing value unless the key exists and was successfully cast to the requested type
        /// </summary>
        /// <typeparam name="T">The type of value to read from the key</typeparam>
        /// <param name="parentNode">The node containing the requested key</param>
        /// <param name="key">The name of the key requested</param>
        /// <param name="output">A reference to store a successully read value in</param>
        /// <returns></returns>
        private static bool TryLoadSettingsKey<T>(YamlNode parentNode, string key, ref T output)
        {
            // Fail to load the value if the parent node is not a mapping node and therefore cant't contain the key requested
            if (parentNode.NodeType != YamlNodeType.Mapping) return false;
            // Get the parent node as a mapping node
            var mappingNode = (YamlMappingNode)parentNode;

            // Try to retrieve the requested node. fail to set the value if the key cannot be found
            if (!mappingNode.Children.TryGetValue(key, out var node)) return false;

            // Ensure the type of this node is allows it to contain the value
            if (node.NodeType != YamlNodeType.Scalar) return false;
            var scalarNode = (YamlScalarNode)node;

            // Try converting the node value to the requested type
            try
            {
                output = (T)Convert.ChangeType(scalarNode.Value, typeof(T));
            }
            // If the cast does not work return that the operation failed
            catch { return false; }

            // If the value was successfully read and all checks passed, return that the operation was successful
            return true;
        }

    }
}