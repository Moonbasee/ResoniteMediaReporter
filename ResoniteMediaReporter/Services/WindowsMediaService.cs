using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace ResoniteMediaReporter.Services
{
    public class WindowsMediaService
    {
        private string NowPlaying = "Not Currently Playing Anything";

        public GlobalSystemMediaTransportControlsSessionManager MediaTransportControlsSessionManager;
        public GlobalSystemMediaTransportControlsSession CurrentMediaSession;
        public GlobalSystemMediaTransportControlsSessionMediaProperties CurrentMediaProperties;

        public ResoniteWSSession WSSession { get; private set; }
        public ResoniteWSServer Server { get; private set; }

        public WindowsMediaService(ResoniteWSSession session, ResoniteWSServer server)
        {
            WSSession = session;
            Server = server;

            Console.WriteLine("Initializing Windows Media Service For Client...");
            SetSystemMediaTransportControlsSessionManager().GetAwaiter().GetResult();

            MediaTransportControlsSessionManager.CurrentSessionChanged += MediaTransportControlsSessionManager_CurrentSessionChanged;

            try
            {
                // try and get current session
                CurrentMediaSession = MediaTransportControlsSessionManager.GetCurrentSession();
                if (CurrentMediaSession != null)
                {
                    CurrentMediaProperties = CurrentMediaSession.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
                    CurrentMediaSession.MediaPropertiesChanged += CurrentMediaSession_MediaPropertiesChanged;
                    CurrentMediaSession.PlaybackInfoChanged += CurrentMediaSession_PlaybackInfoChanged;

                    UpdateAndGetCurrentlyPlayingMedia();
                }
                else WSSession.SendText("Not Currently Playing Anything");
            } catch (Exception ex)
            {
                Console.WriteLine($"Exception In WMS. Try Restarting Your Media Player.\n{ex.Message}\n{ex.StackTrace}");
                return;
            }

            Console.WriteLine("WMS Ready");
        }

        private void MediaTransportControlsSessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            // get and set session and properties
            CurrentMediaSession = MediaTransportControlsSessionManager.GetCurrentSession();

            // reset events
            if (CurrentMediaSession != null)
            {
                CurrentMediaSession.MediaPropertiesChanged += CurrentMediaSession_MediaPropertiesChanged;
                CurrentMediaSession.PlaybackInfoChanged += CurrentMediaSession_PlaybackInfoChanged;

                UpdateAndGetCurrentlyPlayingMedia();

                Console.WriteLine($"[WMS] Current Media Session Changed, Detected Player - {CurrentMediaSession.SourceAppUserModelId}");
            }
            else WSSession.SendText("Not Currently Playing Anything");
        }

        private void CurrentMediaSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            CurrentMediaProperties = CurrentMediaSession.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();

            if (CurrentMediaProperties != null)
            {
                // tigger update function
                UpdateAndGetCurrentlyPlayingMedia();
            }
        }

        private void CurrentMediaSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            // tigger update function
            UpdateAndGetCurrentlyPlayingMedia();
        }

        private async Task SetSystemMediaTransportControlsSessionManager() => MediaTransportControlsSessionManager = await GetSystemMediaTransportControlsSessionManager();

        public void UpdateAndGetCurrentlyPlayingMedia()
        {
            try
            {
                if (CurrentMediaSession != null && CurrentMediaProperties != null)
                {
                    var playbackInfo = CurrentMediaSession.GetPlaybackInfo();

                    foreach(var player in Server.Config.IgnorePlayers)
                    {
                        if (!CurrentMediaSession.SourceAppUserModelId.Contains(player))
                        {
                            if (System.Diagnostics.Debugger.IsAttached) Console.WriteLine($"[DEBUG] Passing Player - {CurrentMediaSession.SourceAppUserModelId}");
                        }
                        else
                        {
                            if (System.Diagnostics.Debugger.IsAttached) Console.WriteLine($"[DEBUG] Ignoring Player - {CurrentMediaSession.SourceAppUserModelId}");
                            NowPlaying = "Media Not Detected";
                        }
                    }

                    if (!string.IsNullOrEmpty(CurrentMediaProperties.Artist) && !string.IsNullOrEmpty(CurrentMediaProperties.Title))
                    {
                        NowPlaying = CurrentMediaProperties.Artist + " - " + CurrentMediaProperties.Title;
                    }
                    else
                    {
                        NowPlaying = "Media Not Detected";
                        if (CurrentMediaSession.SourceAppUserModelId.Contains("Amazon")) NowPlaying = "[Amazon Music] " + NowPlaying;
                    }

                    if (CurrentMediaProperties.Artist == CurrentMediaProperties.Title && CurrentMediaSession.SourceAppUserModelId.Contains("Amazon"))
                    {
                        NowPlaying = "[Amazon Music] " + CurrentMediaProperties.Artist + " - " + CurrentMediaProperties.Title;
                    }

                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    {
                        NowPlaying = "[Paused] " + NowPlaying;
                    }
                }
                else
                {
                    NowPlaying = "Not Currently Playing Anything";
                }
            } catch (Exception ex)
            {
                NowPlaying = "Not Currently Playing Anything";
                Console.WriteLine($"[EXCEPTION CAUGHT IN MEDIA SERVICE] {ex.Message}");
            }

            if (System.Diagnostics.Debugger.IsAttached) Console.WriteLine($"[DEBUG] Now Playing - {NowPlaying}");
            WSSession.SendText(NowPlaying);
        }

        public async Task TryMediaControl(MediaControlType type)
        {
            try
            {
                if (CurrentMediaSession != null)
                {
                    switch(type)
                    {
                        case MediaControlType.Play:
                            Console.WriteLine("[WMS] Toggling Play...");
                            await CurrentMediaSession.TryPlayAsync();
                            break;
                        case MediaControlType.Pause:
                            Console.WriteLine("[WMS] Toggling Pause...");
                            await CurrentMediaSession.TryPauseAsync();
                            break;
                        case MediaControlType.Stop:
                            Console.WriteLine("[WMS] Toggling Stop...");
                            await CurrentMediaSession.TryStopAsync();
                            break;
                        case MediaControlType.Skip:
                            Console.WriteLine("[WMS] Skipping...");
                            await CurrentMediaSession.TrySkipNextAsync();
                            break;
                        case MediaControlType.Previous:
                            Console.WriteLine("[WMS] Going Back To Previous...");
                            await CurrentMediaSession.TrySkipPreviousAsync();
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("[WMS] Nothing Is Currently Detected");
                }
            } catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION CAUGHT IN MEDIA SERVICE] {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing Windows Media Service...");

            // deregister events
            MediaTransportControlsSessionManager.CurrentSessionChanged -= MediaTransportControlsSessionManager_CurrentSessionChanged;
            if (CurrentMediaSession != null)
            {
                CurrentMediaSession.PlaybackInfoChanged -= CurrentMediaSession_PlaybackInfoChanged;
                CurrentMediaSession.MediaPropertiesChanged -= CurrentMediaSession_MediaPropertiesChanged;
            }

            // null out everything
            MediaTransportControlsSessionManager = null;
            CurrentMediaSession = null;
            CurrentMediaProperties = null;
            
            // reset now playing
            NowPlaying = "Not Currently Playing Anything";

            Console.WriteLine("Disposed.");
        }

        private static async Task<GlobalSystemMediaTransportControlsSessionManager> GetSystemMediaTransportControlsSessionManager() => await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

        public enum MediaControlType { Play, Pause, Stop, Skip, Previous }
    }
}
