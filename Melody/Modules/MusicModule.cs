using Discord;
using Discord.Commands;
using Melody.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace Melody.Modules
{
    public class MusicModule : ModuleBase<SocketCommandContext>
    {

        private const int _queueTracksToList = 10; // Controls how many songs -queue lists by name

        private readonly LavaNode _lavaNode;
        private readonly AudioService _audioService;
        private readonly Logger _logger;

        public MusicModule(LavaNode lavaNode, AudioService audioService)
        {
            _lavaNode = lavaNode;
            _audioService = audioService;
            _logger = new("Music");
        }

        [Command("Join")]
        [Alias("j")]
        [Summary("Make me join the voice channel you are connected to.")]
        [RequireContext(ContextType.Guild)]
        public async Task JoinAsync()
        {
            var voiceState = Context.User as IVoiceState;

            // If the user is not in a voice channel
            if (voiceState.VoiceChannel == null)
            {
                await ReplyAsync("Please join a voice channel and try again");
                return;
            }

            _logger.Log($"Joining channel \"{voiceState.VoiceChannel.Name}\"");
            await _lavaNode.JoinAsync(voiceState.VoiceChannel);
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }

        [Command("Leave")]
        [Alias("l")]
        [Summary("Disconnect me from a voice channel")]
        [RequireContext(ContextType.Guild)]
        public async Task LeaveAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not in a voice channel");
                return;
            }

            // Ensure looping gets disabled for the voice channel
            _audioService.SetLoopingState(player.VoiceChannel, LoopingState.None);
            _audioService.ClearPlaylist(player.VoiceChannel);

            // Leave the voice channel
            _logger.Log($"Leaving channel \"{player.VoiceChannel.Name}\"");
            await _lavaNode.LeaveAsync(player.VoiceChannel);
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }

        [Command("NowPlaying")]
        [Alias("np")]
        [Summary("Display the currently playing song")]
        [RequireContext(ContextType.Guild)]
        public async Task NowPlayingAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || (player.PlayerState != PlayerState.Playing && player.PlayerState != PlayerState.Paused))
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }
            // Display information about the current track in an embed message
            await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Currently Playing", player.Track));
        }

        [Command("Play")]
        [Alias("p")]
        [Summary("Play/queue a song in a voice channel")]
        [RequireContext(ContextType.Guild)]
        public async Task PlayAsync([Remainder] string identifier)
        {
            var voiceState = Context.User as IVoiceState;

            // If the bot is not in a voice channel in this server (or worse, has no lavalink 
            // player), attempt to join the one the caller is in.
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                // if the caller is not in a voice channel stop and request that they join one
                if (voiceState.VoiceChannel == null)
                {
                    // Log the request for debugging
                    _logger.Log($"{Context.Message.Author.Username} requested to play \"{identifier}\" without a voice channel");
                    // Request that the user joins a voice channel
                    await ReplyAsync("Please join a voice channel first and try again");
                    return;
                }

                player = await _lavaNode.JoinAsync(voiceState.VoiceChannel);
            }
            // Log the request in the console output for debugging
            _logger.Log($"{Context.Message.Author.Username} requested to play \"{identifier}\"");

            // If the request if a youtube playlist url
            if (identifier.Contains("/playlist?list="))
            {
                SearchResponse playlistResponse = await _lavaNode.SearchAsync(SearchType.Direct, identifier);

                // If it couldn't find the playlist report that nothing was found
                if (playlistResponse.Tracks.Count == 0)
                {
                    await ReplyAsync("I couldn't find the playlist you requested");
                    return;
                }

                // Store the list of tracks for repeating later if requested
                _audioService.AddPlaylistTracks(player.VoiceChannel, playlistResponse.Tracks.ToList());

                // Queue every song returned by the playlist
                foreach (var playlistTrack in playlistResponse.Tracks)
                {
                    player.Queue.Enqueue(playlistTrack);
                }

                // Log and display the number of tracks queued to the user
                _logger.Log($"Queued {playlistResponse.Tracks.Count} tracks");
                await ReplyAsync(embed: new EmbedBuilder().WithTitle($"Queued {playlistResponse.Tracks.Count} tracks").WithColor(ConfigurationService.EmbedColor).Build());

                // If nothing is playing yet, start the queue
                if (player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.None)
                {
                    player.Queue.TryDequeue(out var firstTrack);
                    await player.PlayAsync(firstTrack);
                    await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Now Playing", player.Track));
                    _logger.Log($"Playing \"{firstTrack.Title}\"");
                }

                // Indicate that the request was fufilled successfully
                await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
                return;
            }

            // Try loading the audio track from youtube
            SearchResponse response = await _lavaNode.SearchAsync(SearchType.YouTube, identifier);

            // If the youtube search didn't work, try directly using the given identifier as a link
            if (response.Tracks.Count == 0)
            {
                response = await _lavaNode.SearchAsync(SearchType.Direct, identifier);

                // If it still couldn't find anything report that nothing was found
                if (response.Tracks.Count == 0)
                {
                    await ReplyAsync("I couldn't find the song you requested");
                    return;
                }
            }

            // Get the first track returned by either search method
            var track = response.Tracks.First();

            // Store the loaded track for queue looping
            _audioService.AddPlaylistTracks(player.VoiceChannel, new List<LavaTrack> { track });

            // If nothing is playing yet, play the loaded track
            if (player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.None)
            {
                await player.PlayAsync(track);
                await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Now Playing", player.Track));
                _logger.Log($"Playing \"{track.Title}\"");
            }
            // If music is already playing then queue the song to play later
            else
            {
                // Add the track to the queue
                player.Queue.Enqueue(track);
                _logger.Log($"Added \"{track.Title}\" to the queue");
                await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Queued", track));
            }

            // Indicate that the request was fufilled successfully
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }

        [Command("Skip")]
        [Alias("s")]
        [Summary("Skip to the end of the current song and play the next one in the queue")]
        [RequireContext(ContextType.Guild)]
        public async Task SkipAsync()
        {
            // Make sure something is playing before trying to skip it
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped || player.PlayerState == PlayerState.None)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // If looping the current track, stop looping it since a different one will be playing now
            if (_audioService.GetLoopingState(player.VoiceChannel) == LoopingState.Track)
            {
                _audioService.SetLoopingState(player.VoiceChannel, LoopingState.None);
            }

            // If there is no next track
            if (player.Queue.Count == 0)
            {
                // Display that nothing is available to skip to and stop the current track
                _logger.Log("Tried to skip but end of queue reached. Stopping current track");
                await ReplyAsync("I've reached the end of the queue");
                await player.StopAsync();
                return;
            }

            // Skip to the next track
            // The skipped track reference isn't needed here
            (_, var currentTrack) = await player.SkipAsync();
            // Display information about the new track
            _logger.Log($"Skipped the rest of the current track, starting \"{currentTrack.Title}\"");
            await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Now Playing", currentTrack));
        }

        [Command("Stop")]
        [Summary("Stop playing the current song and clear the queue")]
        [RequireContext(ContextType.Guild)]
        public async Task StopAsync()
        {
            // Make sure something is actually playing before trying to stop it
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // The audio service event handler will clear the queue when this stops the current track

            // Prevent tracks from trying to loop
            _audioService.SetLoopingState(player.VoiceChannel, LoopingState.None); 
            
            // Stop the current track
            await player.StopAsync();
            // Indicate it was stopped successfully
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);

        }

        [Command("Pause")]
        [Summary("Pause the currently playing song")]
        [RequireContext(ContextType.Guild)]
        public async Task PauseAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // Pause the track
            await player.PauseAsync();
            // Indicate it was paused successfully
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }

        [Command("Resume")]
        [Summary("Resume the music if it was paused")]
        [RequireContext(ContextType.Guild)]
        public async Task ResumeAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // Resume the track
            await player.ResumeAsync();
            // Display what track was resumed
            await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Resumed", player.Track));
        }

        [Command("Loop")]
        [Summary("Loop the current song")]
        [RequireContext(ContextType.Guild)]
        public async Task LoopAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // Start looping only the current track
            _logger.Log($"Looping track \"{player.Track.Title}\"");
            _audioService.SetLoopingState(player.VoiceChannel, LoopingState.Track);
            // Display what track is being looped
            await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Looping Track", player.Track));
        }

        [Command("LoopQueue")]
        [Alias("loopq")]
        [Summary("Loop a playlist of every track loaded since joining the channel")]
        [RequireContext(ContextType.Guild)]
        public async Task LoopPlaylistAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            var voiceChannel = player.VoiceChannel;

            // Start looping the whole queue and tell the user hw long it currently is
            _audioService.SetLoopingState(voiceChannel, LoopingState.Playlist);
            await ReplyAsync($"Ok, I'll Loop the queue. It is currently {_audioService.GetPlaylistLength(voiceChannel)} tracks long");
        }

        [Command("UnLoop")]
        [Alias("uloop")]
        [Summary("Stop looping music")]
        [RequireContext(ContextType.Guild)]
        public async Task UnLoopAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            // Turn of looping for the active voice channel
            var voiceChannel = player.VoiceChannel;
            _audioService.SetLoopingState(voiceChannel, LoopingState.None);

            // Indicate success with a reaction emoji
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }

        [Command("Queue")]
        [Alias("q")]
        [Summary("Display a list of queued tracks")]
        [RequireContext(ContextType.Guild)]
        public async Task QueueAsync()
        {
            // Ensure there is something playing
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player) || player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync("I'm not playing anything at the moment");
                return;
            }

            var queueLength = player.Queue.Count;

            // When the queue is empty don't display any track details
            if (queueLength == 0)
            {
                await ReplyAsync("No other tracks are queued");
            }
            // Display a single song when the queue is one item long
            else if (queueLength == 1)
            {
                await ReplyAsync(embed: await EmbedService.EmbedSongAsync("Up Next", player.Queue.First()));
            }
            // Display a list of upcoming tracks when the queue is longer
            else
            {
                // Get some of the first tracks from the queue to list by name
                // The embed will contain a footer listing how many others there are
                // Also handle the list being shorter than the limit on displayed tracks
                var tracks = player.Queue.ToList().GetRange(0, Math.Min(_queueTracksToList, queueLength));
                await ReplyAsync(embed: await EmbedService.EmbedSongListAsync("Up Next", tracks, queueLength));
            }
        }

        [Command("Volume")]
        [Alias("v")]
        [Summary("Change the sound volume")]
        [RequireContext(ContextType.Guild)]
        public async Task VolumeAsync(int newVolume)
        {
            // Check that the bot is in a voice channel
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync("I'm not in a voice channel at the moment");
                return;
            }

            // Set the volume of the player
            await player.UpdateVolumeAsync((ushort)Math.Clamp(newVolume, 0, 1000)); // LavaPlayer internals limit volume range to 0% - 1000%

            // Indicate success with a reaction emoji
            await Context.Message.AddReactionAsync(ConfigurationService.SuccessReaction);
        }
    }
}