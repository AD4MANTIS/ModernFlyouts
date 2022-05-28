#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

using iTunesLib;

namespace ModernFlyouts.Core.Utilities
{
    /// <summary>
    /// A wrapper for http://www.joshkunz.com/iTunesControl/main.html
    /// </summary>
    public class ITunesService : IDisposable
    {
        #region Static

        private static ITunesService? _instance;
        public static ITunesService Instance => _instance ??= new ITunesService();

        #endregion

        #region App and Process
        private iTunesApp? _app;
        private Process? iTunesProcess;

        public iTunesApp? App
        {
            get
            {
                if (_app != null)
                    return _app;

                InitApp();

                return _app;
            }
        }

        public Process? ITunesProcess
        {
            get => iTunesProcess;
            private set
            {
                if (ITunesProcess != null)
                    ITunesProcess.Exited -= ITunesProcess_Exited;

                iTunesProcess = value;

                if (ITunesProcess != null)
                    ITunesProcess.Exited += ITunesProcess_Exited;
            }
        }

        private void InitApp()
        {
            // only set app if iTunes is running
            ITunesProcess = Process.GetProcessesByName("iTunes").FirstOrDefault();
            var newApp = ITunesProcess == null ? null : new iTunesAppClass();
            SetApp(newApp);
        }

        private void SetApp(iTunesApp? newApp)
        {
            // Close current App
            if (_app != null)
            {
                _app.OnAboutToPromptUserToQuitEvent -= App_OnAboutToPromptUserToQuitEvent;

                AppClosing?.Invoke();

                Debug.WriteLine("Closing iTunes Service");
                ReleaseUnmanagedResources();
            }

            _app = newApp;

            if (_app == null)
            {
                ITunesProcess = null;
            }
            else
            {
                Debug.WriteLine("Started iTunes Service");

                _app.OnAboutToPromptUserToQuitEvent += App_OnAboutToPromptUserToQuitEvent;

                AppStarted?.Invoke();
            }
        }

        public void CloseService() => SetApp(null);

        private void App_OnAboutToPromptUserToQuitEvent() => CloseService();

        private void ITunesProcess_Exited(object? _, EventArgs _1) => CloseService();

        #endregion

        /// <summary>
        /// Gets a value indicating whether this instance is active.
        /// </summary>
        /// <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
        public bool IsActive => App != null;

        public event Action? AppStarted;
        public event Action? AppClosing;

        #region IDisposable

        private void ReleaseUnmanagedResources()
        {
            if (_app != null)
            {
                Marshal.FinalReleaseComObject(_app);
                GC.Collect();
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            CloseService();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}