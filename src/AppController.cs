using System;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;

namespace PromptBar
{
    public sealed class AppController : IDisposable
    {
        private readonly WpfApplication app;
        private readonly SettingsStore settingsStore;
        private readonly PrompterModel model;
        private readonly HotkeyProfile hotkeyProfile;
        private readonly DispatcherTimer saveTimer;
        private OverlayWindow overlayWindow;
        private SettingsWindow settingsWindow;
        private TrayController trayController;
        private HotkeyManager hotkeyManager;
        private bool disposed;

        public AppController(WpfApplication app)
        {
            this.app = app;
            settingsStore = new SettingsStore();
            model = new PrompterModel();
            SettingsData settingsData = settingsStore.Load();
            model.Load(settingsData);
            hotkeyProfile = settingsData.Hotkeys ?? new HotkeyProfile();

            saveTimer = new DispatcherTimer();
            saveTimer.Interval = TimeSpan.FromMilliseconds(250);
            saveTimer.Tick += SaveTimerOnTick;
            model.SaveSuggested += ModelOnSaveSuggested;
        }

        public void Start()
        {
            overlayWindow = new OverlayWindow(model, ShowSettings, ShowHotkeyHelp);
            overlayWindow.ShowOrHide(model.IsOverlayVisible);

            hotkeyManager = new HotkeyManager(hotkeyProfile, ExecuteShortcut);
            hotkeyManager.RegisterAll();

            trayController = new TrayController(model, this, hotkeyProfile, hotkeyManager.FailedRegistrations);

            // Persist sanitized legacy settings after loading older builds.
            SaveNow();
        }

        public void ShowSettings()
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow(model, hotkeyProfile, HotkeysChanged);
            }

            settingsWindow.ShowSettings();
        }

        public void ShowHotkeyHelp()
        {
            System.Windows.MessageBox.Show(
                hotkeyProfile.HelpText(model.LanguageCode),
                Localization.Text(model.LanguageCode, "HotkeyDialogTitle"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        public void Quit()
        {
            app.Shutdown();
        }

        public void ExecuteShortcut(ShortcutCommand command)
        {
            if (command == ShortcutCommand.StartPause)
            {
                model.ToggleRunning();
            }
            else if (command == ShortcutCommand.Reset)
            {
                model.ResetScroll();
            }
            else if (command == ShortcutCommand.JumpBack)
            {
                model.JumpBack(5);
            }
            else if (command == ShortcutCommand.TogglePrivacy)
            {
                model.PrivacyModeEnabled = !model.PrivacyModeEnabled;
            }
            else if (command == ShortcutCommand.ToggleOverlay)
            {
                model.IsOverlayVisible = !model.IsOverlayVisible;
            }
            else if (command == ShortcutCommand.SpeedUp)
            {
                model.AdjustSpeed(PrompterModel.SpeedStep);
            }
            else if (command == ShortcutCommand.SpeedDown)
            {
                model.AdjustSpeed(-PrompterModel.SpeedStep);
            }
            else if (command == ShortcutCommand.FontSizeUp)
            {
                model.AdjustFontSize(1);
            }
            else if (command == ShortcutCommand.FontSizeDown)
            {
                model.AdjustFontSize(-1);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            saveTimer.Stop();
            SaveNow();

            if (hotkeyManager != null)
            {
                hotkeyManager.Dispose();
            }

            if (trayController != null)
            {
                trayController.Dispose();
            }

            if (settingsWindow != null)
            {
                settingsWindow.ReallyClose();
            }
        }

        private void ModelOnSaveSuggested(object sender, EventArgs e)
        {
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveTimerOnTick(object sender, EventArgs e)
        {
            saveTimer.Stop();
            SaveNow();
        }

        private void HotkeysChanged()
        {
            if (hotkeyManager != null)
            {
                hotkeyManager.ApplyProfile(hotkeyProfile);
            }

            if (trayController != null)
            {
                trayController.RefreshHotkeyProfile(hotkeyProfile, hotkeyManager.FailedRegistrations);
            }

            SaveNow();
        }

        private void SaveNow()
        {
            SettingsData data = model.ToSettingsData();
            data.Hotkeys = hotkeyProfile;
            settingsStore.Save(data);
        }
    }
}
