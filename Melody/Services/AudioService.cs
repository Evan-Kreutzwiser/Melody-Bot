using Discord;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Melody.Services
{
    public enum LoopingState
    {
        None,
        Track,
        Playlist
    }

    public sealed class AudioService
    {
        private readonly LavaNode _lavaNode;
        private readonly Logger _logger = new("AudioService");

        /// <summary>
        /// Tracks whether or not each connected voice channel is looping a track or playlist
        /// </summary>
        private readonly ConcurrentDictionary<ulong, LoopingState> _loopTokens = new();

        /// <summary>
        /// Contains a record of every track loaded in each voice channel since the last join or stop command
        /// </summary>
        private readonly ConcurrentDictionary<ulong, List<LavaTrack>> _playlists = new();

        /// <summary>
        /// Tokens responsible for making the bot automatically leave a voice channel after not playing music in it for a set period of time
        /// </summary>
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _discconectTokens = new();

        public AudioService(LavaNode lavaNode)
            => _lavaNode = lavaNode;

        /// <summary>
        /// Register this service's event handlers with the lavalink node. 
        /// Calling this method is also used to create the service instance with the dependancy injection.
        /// </summary>
        public void AddEventHandlers()
        {
            // Register track event handlers
            _lavaNode.OnTrackStarted += OnTrackStarted; // Cancles automatic disconnects when new tracks are started
            _lavaNode.OnTrackEnded += OnTrackEnded; // Play the next track in the queue when a track finishes,
                                                    // and at the end of the queue start the autodisconnection timer
        }

        /// <summary>
        /// Stops a voice channel's disconect timer when the bot starts playing another track
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task OnTrackStarted(TrackStartEventArgs args)
        {
            // If the channel has a token and has not already been requested to cancel disconnecting
            if (_discconectTokens.TryGetValue(args.Player.VoiceChannel.Id, out var value)
                && !value.IsCancellationRequested)
            {
                // Cancel the disconnect token to keep the bot in the voice channel
                value.Cancel();
                _logger.Log("Autodisconnect cancled");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles starting a new track when the current one ends and when no other tracks can be played trigger the auotmatic disconnection timer
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task OnTrackEnded(TrackEndedEventArgs args)
        {
            var player = args.Player;
            var voiceChannel = player.VoiceChannel;

            // When the track finishes or is skipped
            if (args.Reason == TrackEndReason.Finished)
            {
                _logger.Log($"Finished playing {args.Track.Title} (Looping state {GetLoopingState(voiceChannel)})");

                // If the song should repeat, play it again
                if (GetLoopingState(voiceChannel) == LoopingState.Track)
                {
                    await player.PlayAsync(args.Track);
                    _logger.Log($"Repeating \"{args.Track.Title}\"");
                    return;
                }

                // Play the next track if possible
                if (player.Queue.TryDequeue(out var track))
                {
                    await player.PlayAsync(track);
                    _logger.Log($"Playing \"{track.Title}\"; the next track in the queue");
                    return;
                }
                // If there are no more tracks to play and playlist looping is enabled, requeue the playlist
                else if (GetLoopingState(voiceChannel) == LoopingState.Playlist)
                {
                    await RequeuePlaylistAsync(player);
                }
                // If there are no more tracks and it is not looping a playlist, start the autodisconnect timer
                else
                {
                    _logger.Log($"End of queue reached");
                    // Start the automatic disconnection timer
                    _ = BeginDisconnectTimerAsync(voiceChannel);
                }
            }

            // When a user requests to stop playback
            if (args.Reason == TrackEndReason.Stopped)
            {
                // If there are no more tracks to play and playlist looping is enabled, requeue the playlist so it can be played again
                if (player.Queue.Count == 0 && GetLoopingState(voiceChannel) == LoopingState.Playlist)
                {
                    await RequeuePlaylistAsync(player);
                    return;
                }

                // Clear any unplayed songs in the queue and don't start any more music until requested
                // When leaving a channel the track is ended with the reason "Stopped" but the queue has already been disposed of
                // so don't try to clear the queue unless it is actually present
                player.Queue?.Clear();
                _logger.Log($"Stopped playing {args.Track.Title} and cleared the queue");

                // Start the automatic disconnection timer
                _ = BeginDisconnectTimerAsync(voiceChannel);
                // Don't await, since this will block until the automatic disconnect occurs or is cancled
            }
        }

        /// <summary>
        /// Requeue all the tracks of the most recently loaded playlist
        /// </summary>
        /// <param name="player"></param>
        private async Task RequeuePlaylistAsync(LavaPlayer player)
        {
            // Make sure there is a playlist to loop
            if (!_playlists.TryGetValue(player.VoiceChannel.Id, out var tracks) || tracks.Count == 0) // The playlist has tracks to play)
            {
                _logger.Log("No playlist to loop");
                return;
            }

            // Queue every song in the playlist
            foreach (var playlistTrack in tracks)
            {
                player.Queue.Enqueue(playlistTrack);
            }

            // Start playing the requeued playlist
            player.Queue.TryDequeue(out var playlistFirstTrack);
            await player.PlayAsync(playlistFirstTrack);

            // Log the number of tracks queued
            _logger.Log($"Requeued {tracks.Count} tracks from the playlist. Playing {playlistFirstTrack.Title}");
        }

        /// <summary>
        /// Start the (cancelable) automatic disconnection timer that makes the bot leave a voice channel it is inactive in
        /// </summary>
        /// <param name="voiceChannel">The voice channel to disconnect from</param>
        /// <returns></returns>
        private async Task BeginDisconnectTimerAsync(IVoiceChannel voiceChannel)
        {
            var timeSpan = ConfigurationService.AutoDisconnectTimespan;

            // Return right away if autodisconnecting is disabled
            if (timeSpan.Equals(Timeout.InfiniteTimeSpan)) return;

            // If there is no token source, create one
            if (!_discconectTokens.TryGetValue(voiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                _discconectTokens.TryAdd(voiceChannel.Id, value);
            }
            // If a cancled token is present, renew it to start the timer again
            else if (value.IsCancellationRequested)
            {
                _discconectTokens.TryUpdate(voiceChannel.Id, new CancellationTokenSource(), value);
                value = _discconectTokens[voiceChannel.Id];
            }

            // If the disconnect is cancled before the timespan elapses, return right away
            var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);
            if (isCancelled) return;

            // Disconnect from the voice channel due to inactivity
            _logger.Log($"Automatically disconnecting from \"{voiceChannel.Name}\" due to inactivity");
            await _lavaNode.LeaveAsync(voiceChannel);
        }

        /// <summary>
        /// Set whether a voice channel should loop it's currently playing track
        /// </summary>
        /// <param name="voiceChannel">The voice channel to set the looping state of</param>
        /// <param name="loopingState">Whether the current track or playlist should loop</param>
        public void SetLoopingState(IVoiceChannel voiceChannel, LoopingState loopingState)
        {
            _loopTokens[voiceChannel.Id] = loopingState;
        }

        /// <summary>
        /// Get whether a track is looping on a voice channel
        /// </summary>
        /// <param name="voiceChannel">The voice channel to check in</param>
        /// <returns></returns>
        public LoopingState GetLoopingState(IVoiceChannel voiceChannel)
        {
            return _loopTokens.GetOrAdd(voiceChannel.Id, LoopingState.None);
        }

        /// <summary>
        /// Add tracks to the list of tracks that get requeued when playlist looping is enabled
        /// </summary>
        /// <param name="voiceChannel"></param>
        /// <param name="tracks"></param>
        public void AddPlaylistTracks(IVoiceChannel voiceChannel, List<LavaTrack> tracks)
        {
            // If the voice channel doesn't have a track list yet, create one
            if (!_playlists.TryGetValue(voiceChannel.Id, out var list))
            {
                list = new List<LavaTrack>();
                _playlists.TryAdd(voiceChannel.Id, list);
            }

            // Add the tracks to the list
            list.AddRange(tracks);
        }

        /// <summary>
        /// Return the length of a channel's list of stored tracks
        /// </summary>
        /// <param name="voiceChannel"></param>
        /// <returns></returns>
        public int GetPlaylistLength(IVoiceChannel voiceChannel)
        {
            // Return the number of tracks, or if the list does not exist return 0 because in that case no tracks are currently stored
            return _playlists.TryGetValue(voiceChannel.Id, out var playlist) ? playlist.Count : 0;
        }

        /// <summary>
        /// Clear the stored track list for a voice channel
        /// </summary>
        /// <param name="voiceChannel"></param>
        public void ClearPlaylist(IVoiceChannel voiceChannel)
        {
            // If a playlist is present for the channel, safely remove it
            _playlists.TryRemove(voiceChannel.Id, out _);
        }
    }
}