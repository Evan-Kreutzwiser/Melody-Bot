using Discord;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;

namespace Melody.Services
{
    public static class EmbedService
    {

        /// <summary>
        /// Create an embed message for a song containing the link and the song's artwork
        /// </summary>
        /// <param name="embedTitle">The title of the embed message (Eg. "Currently Playing")</param>
        /// <param name="track">The track that the art, title, and url are obtained from</param>
        /// <returns></returns>
        public async static Task<Embed> EmbedSongAsync(string embedTitle, LavaTrack track)
        {
            // Obtain track information
            var trackTitle = track.Title;
            var trackUrl = track.Url;
            var imageUrl = await track.FetchArtworkAsync();

            // Create the embed message
            return new EmbedBuilder()
                .WithColor(ConfigurationService.EmbedColor)
                .WithTitle(embedTitle)
                .WithDescription($"**[{trackTitle}]({trackUrl})**")
                .WithThumbnailUrl(imageUrl)
                .Build();
        }

        /// <summary>
        /// Create an embed message displaying a list of songs. Also accepts a queue length used to add a footer showing how many songs are in the list that aren't shown
        /// </summary>
        /// <param name="embedTitle">The title of the embed message</param>
        /// <param name="tracks">The list of tracks to display</param>
        /// <param name="queueLength">When larger the the length of the tracks list adds a footer showing how many items aren't included in that list</param>
        /// <returns></returns>
        public async static Task<Embed> EmbedSongListAsync(string embedTitle, List<LavaTrack> tracks, int queueLength)
        {
            var firstTrack = tracks.First();
            var firstTrackImageUrl = await firstTrack.FetchArtworkAsync();

            // Create an embed with information about the first track
            var embed = new EmbedBuilder()
                .WithColor(ConfigurationService.EmbedColor)
                .WithTitle(embedTitle)
                .WithThumbnailUrl(firstTrackImageUrl);

            // Add information about the other tracks
            var list = new StringBuilder();
            for (var index = 0; index < tracks.Count; index++)
            {
                // Add each track as a line in a list
                list.AppendFormat("**{0}.** {1}\n", index + 1, tracks[index].Title);
            }
            embed.WithDescription(list.ToString());

            // If there are tracks not included in the list because of the list's length
            if (queueLength > tracks.Count)
            {
                // Add the number of tracks not named above to the footer
                var nonListedTracksCount = queueLength - tracks.Count;
                // Condtionally include the 's' in others to maintain proper grammar when there is only 1 other track
                var footerString = $"+ {nonListedTracksCount} other{((nonListedTracksCount > 1) ? "s" : "")}";
                embed.WithFooter(footerString);
            }

            return embed.Build();
        }
    }
}