#nullable enable

using iTunesLib;
using ModernFlyouts.Core.AppInformation;
using ModernFlyouts.Core.Helpers;
using Schober.Felix.ITunes.Controller;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Storage.Streams;

namespace ModernFlyouts.Core.Media.Control
{
    public class ITunesMediaSession : MediaSession
    {
        private ITunesService ITunesService { get; set; }
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
        private SourceAppInfo? sourceAppInfo = null;

        public IITTrack? CurrentTrack { get; set; }

        public ITunesMediaSession(ITunesService iTunesService)
        {
            ITunesService = iTunesService;
            Initialize();
        }

        private void Initialize()
        {
            if (ITunesService.ITunesProcess != null)
            {
                sourceAppInfo = SourceAppInfo.FromData(new SourceAppInfoData()
                {
                    AppUserModelId   = "",
                    ProcessId        = (uint)ITunesService.ITunesProcess.Id,
                    MainWindowHandle = ITunesService.ITunesProcess.MainWindowHandle,
                    DataType         = SourceAppInfoDataType.FromProcessId
                });

                if (sourceAppInfo != null)
                {
                    sourceAppInfo.InfoFetched += SourceAppInfo_InfoFetched;
                    sourceAppInfo.FetchInfosAsync();
                }
            }

            UpdateSessionInfo();

            timer.Tick += Timer_Tick;
            timer.Start();

            if (ITunesService.App != null)
            {
                ITunesService.App.OnPlayerPlayEvent += App_OnPlayerPlayEvent;
                ITunesService.App.OnPlayerPlayingTrackChangedEvent += App_OnPlayerPlayingTrackChangedEvent;
                ITunesService.App.OnPlayerStopEvent += App_OnPlayerStopEvent;
            }
        }

        private void SourceAppInfo_InfoFetched(object? sender, EventArgs e)
        {
            if (sourceAppInfo != null)
            {
                sourceAppInfo.InfoFetched -= SourceAppInfo_InfoFetched;
                MediaSourceName = sourceAppInfo.DisplayName;

                if (BitmapHelper.TryCreateBitmapImageFromStream(sourceAppInfo.LogoStream, out var bitmap))
                    MediaSourceIcon = bitmap;

                sourceAppInfo = null;
            }
        }

        private void App_OnPlayerStopEvent(object iTrack)
        {
            timer.Start();
            UpdateSessionInfo();
        }

        private void App_OnPlayerPlayEvent(object iTrack)
        {
            timer.Start();
            UpdateSessionInfo();
        }

        private void App_OnPlayerPlayingTrackChangedEvent(object iTrack)
        {
            timer.Start();
            UpdateSessionInfo();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => UpdateTimelineInfo());
        }

        public override void Disconnect()
        {
            base.Disconnect();
            timer.Stop();
            CurrentTrack = null;
        }

        #region Updating Properties

        private void UpdatePlaybackInfo()
        {
            IsPlaying = ITunesService.IsPlaying;

            var playlist    = ITunesService.App?.CurrentPlaylist;
            IsShuffleActive = playlist?.Shuffle ?? false;
            AutoRepeatMode  = playlist?.SongRepeat switch
            {
                null                                         => MediaPlaybackAutoRepeatMode.None,
                ITPlaylistRepeatMode.ITPlaylistRepeatModeOff => MediaPlaybackAutoRepeatMode.None,
                ITPlaylistRepeatMode.ITPlaylistRepeatModeOne => MediaPlaybackAutoRepeatMode.Track,
                ITPlaylistRepeatMode.ITPlaylistRepeatModeAll => MediaPlaybackAutoRepeatMode.List,
                _ => throw new NotImplementedException(),
            };
        }

        private void UpdateTimelineInfo()
        {
            if (IsPlaybackPositionEnabled)
            {
                var track         = ITunesService?.App?.CurrentTrack;
                TimelineStartTime = TimeSpan.Zero;
                TimelineEndTime   = TimeSpan.FromSeconds(track?.Duration ?? 0);
                SetPlaybackPosition(TimeSpan.FromSeconds(track == null ? 0 : ITunesService?.App?.PlayerPosition ?? 0));

                IsTimelinePropertiesEnabled = true;
            }
            else
            {
                TimelineStartTime = TimeSpan.Zero;
                TimelineEndTime   = TimeSpan.Zero;
                PlaybackPosition  = TimeSpan.Zero;

                IsTimelinePropertiesEnabled = false;
            }
        }

        private async void UpdateSessionInfo()
        {
            if (!ITunesService.IsActive)
                return;

            var newTrack = ITunesService.App?.CurrentTrack;
            bool trackChanged = CurrentTrack?.trackID != newTrack?.trackID || newTrack == null;

            CurrentTrack = newTrack;

            if (trackChanged)
                RaiseMediaPropertiesChanging();

            var mediaInfo = CurrentTrack;
            if (trackChanged)
            {
                Title     = mediaInfo?.Name;
                Artist    = mediaInfo?.Artist;
                Album     = mediaInfo?.Album;
                //if      (currentTrackNumber != mediaInfo.TrackNumber)
                //{
                //        TrackChangeDirection = (mediaInfo.TrackNumber - currentTrackNumber) switch
                //        {
                //        > 0 => MediaPlaybackTrackChangeDirection.Forward,
                //        < 0 => MediaPlaybackTrackChangeDirection.Backward,
                //        _ => MediaPlaybackTrackChangeDirection.Unknown
                //        };
                //}

                //currentTrackNumber = mediaInfo.TrackNumber;
            }

            var playbackState = ITunesService.GetPlayerButtonsState();
            
            IsPlayEnabled             = playbackState?.playPauseStopState == ITPlayButtonState.ITPlayButtonStatePlayEnabled;
            IsPauseEnabled            = playbackState?.playPauseStopState == ITPlayButtonState.ITPlayButtonStatePauseEnabled;
            IsPlayOrPauseEnabled      = IsPlayEnabled || IsPauseEnabled;
            IsPreviousEnabled         = playbackState?.previousEnabled ?? true;
            IsNextEnabled             = playbackState?.nextEnabled ?? true;
            IsShuffleEnabled          = mediaInfo != null;
            IsRepeatEnabled           = mediaInfo != null;
            IsStopEnabled             = mediaInfo != null;
            IsPlaybackPositionEnabled = mediaInfo != null;

            PlaybackType = MediaPlaybackType.Music;

            UpdateTimelineInfo();

            UpdatePlaybackInfo();

            if (trackChanged)
            {
                string? path = ITunesService.GetCurrenTrack()?.GetPathToTrackArtwork();
                Thumbnail = !string.IsNullOrEmpty(path)
                    ? await GetThumbnailImageSourceAsync(File.OpenRead(path).AsRandomAccessStream())
                    : null;

                RaiseMediaPropertiesChanged();
            }
        }

        #region Thumbnail fetching

        private async Task<ImageSource?> GetThumbnailImageSourceAsync(IRandomAccessStream thumbnail)
        {
            if (thumbnail != null)
            {
                using var strm = thumbnail;
                if (strm != null && strm.Size > 0)
                {
                    using var dreader = new DataReader(strm);
                    await dreader.LoadAsync((uint)strm.Size);
                    var buffer = new byte[(int)strm.Size];
                    dreader.ReadBytes(buffer);

                    using var nstream = new MemoryStream(buffer);
                    nstream.Seek(0, SeekOrigin.Begin);
                    if (nstream != null && nstream.Length > 0)
                    {
                        return BitmapFrame.Create(nstream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    }
                }
            }

            return null;
        }

        #endregion

        #endregion

        #region Media Controlling

        protected override void PreviousTrack() => ITunesService.PreviousTrack();

        protected override void NextTrack() => ITunesService.SkipTrack();

        protected override void Pause() => ITunesService.Pause();

        protected override void Play() => ITunesService.Play();

        protected override void Stop() => ITunesService.Stop();

        protected override void PlaybackPositionChanged(TimeSpan playbackPosition)
        {
            if (ITunesService.App?.CurrentTrack != null)
                ITunesService.App.PlayerPosition = (int)playbackPosition.TotalSeconds;
        }

        protected override void ChangeAutoRepeatMode()
        {
            throw new NotImplementedException();
        }

        protected override void ChangeShuffleActive()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
