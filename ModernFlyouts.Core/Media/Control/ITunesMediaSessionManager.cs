using ModernFlyouts.Core.Threading;

using Schober.Felix.ITunes.Controller;

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.ApplicationModel.Core;

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

        ~ITunesMediaSessionManager()
        {
            OnDisabled();
        }

        public override void OnEnabled()
        {
            ITunesService.Instance.AppStarted += ITunes_AppStarted;
            timer.Start();
        }

        private async void ITunes_AppStarted()
        {
            ITunesService.Instance.App.OnAboutToPromptUserToQuitEvent += App_OnAboutToPromptUserToQuitEvent;
            await LoadSessions();
        }
        private async void App_OnAboutToPromptUserToQuitEvent()
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

            ITunesService.Instance.Dispose();
        }

        public override async void OnDisabled()
        {
            timer.Stop();
            ITunesService.Instance.AppStarted -= ITunes_AppStarted;
            await CloseITunesServiceAsync();
        }
    }
}
