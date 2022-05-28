using ModernFlyouts.Core.Threading;
using ModernFlyouts.Core.Utilities;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ModernFlyouts.Core.Media.Control
{
    public class ITunesMediaSessionManager : MediaSessionManager
    {
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(2000) };
        private readonly DebounceDispatcher debounce = new();

        public ITunesMediaSessionManager()
        {
            timer.Tick += (_, _) =>
            {
                if (timer.Interval.TotalMinutes < 20d)
                    timer.Interval += TimeSpan.FromSeconds(0.5);

                if (ITunesService.Instance.IsActive)
                {
                    timer.Stop();
                    timer.Interval = TimeSpan.FromMilliseconds(2000);
                }
            };
        }

        public override void OnEnabled()
        {
            ITunesService.Instance.AppStarted += ITunes_AppStarted;
            ITunesService.Instance.AppClosing += ITunes_AppClosing;
            timer.Start();
        }

        private async void ITunes_AppStarted()
        {
            await LoadSessions();
        }

        private async void ITunes_AppClosing()
        {
            var p = ITunesService.Instance.ITunesProcess;
            await CloseITunesServiceAsync();
            debounce.Debounce(TimeSpan.FromSeconds(1), () =>
            {
                p.WaitForExit(20000);
                timer.Start();
            });
        }

        private async UiTask ClearSessions()
        {
            foreach (var session in MediaSessions)
            {
                session.Disconnect();
            }

            if (CurrentMediaSession != null)
            {
                CurrentMediaSession.IsCurrent = false;
                CurrentMediaSession = null;
            }

            MediaSessions.Clear();

            RaiseMediaSessionsChanged();
        }

        private async UiTask LoadSessions()
        {
            await ClearSessions();

            if (ITunesService.Instance.IsActive)
            {
                ITunesMediaSession iTunesSession = new(ITunesService.Instance);
                MediaSessions.Add(iTunesSession);
                CurrentMediaSession = iTunesSession;
                CurrentMediaSession.IsCurrent = true;
            }

            RaiseMediaSessionsChanged();
        }

        private async Task CloseITunesServiceAsync()
        {
            await ClearSessions();
        }

        public override async void OnDisabled()
        {
            timer.Stop();
            ITunesService.Instance.AppStarted -= ITunes_AppStarted;
            ITunesService.Instance.AppClosing -= ITunes_AppClosing;
            await CloseITunesServiceAsync();
        }
    }
}
