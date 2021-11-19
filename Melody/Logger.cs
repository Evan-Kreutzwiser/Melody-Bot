using System;
using System.IO;

namespace Melody
{
    public class Logger
    {
        // How many characters wide source names should be padded to
        private const int _sourcePadding = 12;

        private static StreamWriter _logFileWriter;

        // The name of the part of the program printing the message
        private readonly string _sourceName;

        /// <summary>
        /// Create a logger object that perfixes log output with the name of the component logging the message
        /// </summary>
        /// <param name="source"></param>
        public Logger(string source = "")
        {
            // Right-pad the source name with spaces to align the actual messages in the console output
            _sourceName = source.PadRight(_sourcePadding);
            // Make sure the log file handle is open
            OpenLogFile();
        }

        /// <summary>
        /// Opens the log file the bot will print its messages to. If the file is already open this does nothing
        /// </summary>
        private static void OpenLogFile()
        {
            // Only try to open the file if it is not already open
            if (_logFileWriter != null) return;

            // Create a stream writer that writes to the log file
            try
            {
                var logFile = File.Open("MelodyLog.txt", FileMode.Create);
                _logFileWriter = new StreamWriter(logFile);
            }
            catch (Exception exception)
            {
                // If anything goes wrong opening the log file, quit the program
                Console.WriteLine("ERROR OPENING LOG FILE:\n" + exception.Message);
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// Log a message to the standard output and a log file
        /// </summary>
        /// <param name="message"></param>
        public void Log(string message)
        {
            // Get the time and format the message to include the time and source component
            var timeString = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"{timeString} {_sourceName} {message}";

            // Print the message to the standard output
            Console.WriteLine(formattedMessage);

            // Print the message to the log file
            _logFileWriter.WriteLine(formattedMessage);
            _logFileWriter.Flush(); // Make sure it is actually writen to the disk
        }

    }
}