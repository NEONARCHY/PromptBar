using System;
using System.ComponentModel;

namespace PromptBar
{
    public enum ScrollMode
    {
        Infinite,
        StopAtEnd
    }

    public enum ShortcutCommand
    {
        StartPause,
        Reset,
        JumpBack,
        TogglePrivacy,
        ToggleOverlay,
        SpeedUp,
        SpeedDown,
        FontSizeUp,
        FontSizeDown
    }

    public sealed class ManualScrollEventArgs : EventArgs
    {
        public ManualScrollEventArgs(double deltaPoints)
        {
            DeltaPoints = deltaPoints;
        }

        public double DeltaPoints { get; private set; }
    }

    public sealed class JumpBackEventArgs : EventArgs
    {
        public JumpBackEventArgs(double distancePoints)
        {
            DistancePoints = distancePoints;
        }

        public double DistancePoints { get; private set; }
    }

    public sealed class PrompterModel : INotifyPropertyChanged
    {
        public const double SpeedStep = 5;
        public const double SpeedPresetNormal = 85;
        public const string DefaultFontFamilyName = "SF Pro Display, SF Pro Text, Segoe UI Variable Text, Segoe UI, Arial";

        private bool suppressSave;

        private string languageCode = Localization.DefaultLanguageCode;
        private string script = Localization.Text(Localization.DefaultLanguageCode, "ScriptDefault");
        private bool isRunning;
        private bool manualScrollEnabled;
        private bool isOverlayVisible = true;
        private bool privacyModeEnabled = true;
        private bool hasStartedSession;
        private bool didReachEndInStopMode;
        private double speedPointsPerSecond = 80;
        private double fontSize = 20;
        private string fontFamilyName = DefaultFontFamilyName;
        private double overlayWidth = 600;
        private double overlayHeight = 150;
        private ScrollMode scrollMode = ScrollMode.Infinite;
        private string selectedScreenDeviceName = "";

        public PrompterModel() {}

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler ResetRequested;
        public event EventHandler<JumpBackEventArgs> JumpBackRequested;
        public event EventHandler<ManualScrollEventArgs> ManualScrollRequested;
        public event EventHandler SaveSuggested;

        public string Script
        {
            get { return script; }
            set
            {
                if (value == null)
                {
                    value = "";
                }

                if (script == value)
                {
                    return;
                }

                script = value;
                didReachEndInStopMode = false;
                OnPropertyChanged("Script");
                SuggestSave();
            }
        }

        public string LanguageCode
        {
            get { return languageCode; }
            set
            {
                value = Localization.NormalizeLanguageCode(value);
                if (languageCode == value)
                {
                    return;
                }

                languageCode = value;
                OnPropertyChanged("LanguageCode");
                SuggestSave();
            }
        }

        public bool IsRunning
        {
            get { return isRunning; }
            private set
            {
                if (isRunning == value)
                {
                    return;
                }

                isRunning = value;
                OnPropertyChanged("IsRunning");
                SuggestSave();
            }
        }

        public bool ManualScrollEnabled
        {
            get { return manualScrollEnabled; }
            private set
            {
                if (manualScrollEnabled == value)
                {
                    return;
                }

                manualScrollEnabled = value;
                OnPropertyChanged("ManualScrollEnabled");
            }
        }

        public bool IsOverlayVisible
        {
            get { return isOverlayVisible; }
            set
            {
                if (isOverlayVisible == value)
                {
                    return;
                }

                isOverlayVisible = value;
                OnPropertyChanged("IsOverlayVisible");
                SuggestSave();
            }
        }

        public bool PrivacyModeEnabled
        {
            get { return privacyModeEnabled; }
            set
            {
                if (privacyModeEnabled == value)
                {
                    return;
                }

                privacyModeEnabled = value;
                OnPropertyChanged("PrivacyModeEnabled");
                SuggestSave();
            }
        }

        public bool HasStartedSession
        {
            get { return hasStartedSession; }
            private set
            {
                if (hasStartedSession == value)
                {
                    return;
                }

                hasStartedSession = value;
                OnPropertyChanged("HasStartedSession");
            }
        }

        public bool DidReachEndInStopMode
        {
            get { return didReachEndInStopMode; }
            private set
            {
                if (didReachEndInStopMode == value)
                {
                    return;
                }

                didReachEndInStopMode = value;
                OnPropertyChanged("DidReachEndInStopMode");
            }
        }

        public double SpeedPointsPerSecond
        {
            get { return speedPointsPerSecond; }
            set
            {
                value = ClampedSpeed(value);
                if (Math.Abs(speedPointsPerSecond - value) < 0.001)
                {
                    return;
                }

                speedPointsPerSecond = value;
                OnPropertyChanged("SpeedPointsPerSecond");
                SuggestSave();
            }
        }

        public double FontSize
        {
            get { return fontSize; }
            set
            {
                value = Clamp(value, 12, 40);
                if (Math.Abs(fontSize - value) < 0.001)
                {
                    return;
                }

                fontSize = value;
                OnPropertyChanged("FontSize");
                SuggestSave();
            }
        }

        public string FontFamilyName
        {
            get { return fontFamilyName; }
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                {
                    value = DefaultFontFamilyName;
                }

                if (fontFamilyName == value)
                {
                    return;
                }

                fontFamilyName = value;
                OnPropertyChanged("FontFamilyName");
                SuggestSave();
            }
        }

        public double OverlayWidth
        {
            get { return overlayWidth; }
            set
            {
                value = Clamp(value, 400, 1200);
                if (Math.Abs(overlayWidth - value) < 0.001)
                {
                    return;
                }

                overlayWidth = value;
                OnPropertyChanged("OverlayWidth");
                SuggestSave();
            }
        }

        public double OverlayHeight
        {
            get { return overlayHeight; }
            set
            {
                value = Clamp(value, 120, 300);
                if (Math.Abs(overlayHeight - value) < 0.001)
                {
                    return;
                }

                overlayHeight = value;
                OnPropertyChanged("OverlayHeight");
                SuggestSave();
            }
        }

        public ScrollMode ScrollMode
        {
            get { return scrollMode; }
            set
            {
                if (scrollMode == value)
                {
                    return;
                }

                scrollMode = value;
                if (scrollMode == ScrollMode.Infinite)
                {
                    DidReachEndInStopMode = false;
                }

                OnPropertyChanged("ScrollMode");
                SuggestSave();
            }
        }

        public string SelectedScreenDeviceName
        {
            get { return selectedScreenDeviceName; }
            set
            {
                if (value == null)
                {
                    value = "";
                }

                if (selectedScreenDeviceName == value)
                {
                    return;
                }

                selectedScreenDeviceName = value;
                OnPropertyChanged("SelectedScreenDeviceName");
                SuggestSave();
            }
        }

        public TimeSpan EstimatedReadDuration
        {
            get
            {
                string trimmed = (Script ?? "").Trim();
                if (trimmed.Length == 0)
                {
                    return TimeSpan.Zero;
                }

                int words = Math.Max(1, trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length);
                double baselineWpm = 160.0;
                double speedFactor = SpeedPointsPerSecond / SpeedPresetNormal;
                double adjustedWpm = Math.Max(60.0, baselineWpm * speedFactor);
                double minutes = words / adjustedWpm;
                return TimeSpan.FromMinutes(minutes);
            }
        }

        public void Load(SettingsData data)
        {
            if (data == null)
            {
                return;
            }

            suppressSave = true;
            LanguageCode = data.LanguageCode;
            Script = ScriptTextSanitizer.Clean(data.Script ?? script);
            IsOverlayVisible = data.IsOverlayVisible;
            PrivacyModeEnabled = data.PrivacyModeEnabled;
            SpeedPointsPerSecond = data.SpeedPointsPerSecond;
            FontSize = data.FontSize;
            FontFamilyName = data.FontFamilyName;
            OverlayWidth = data.OverlayWidth;
            OverlayHeight = data.OverlayHeight;
            ScrollMode = data.ScrollMode;
            SelectedScreenDeviceName = data.SelectedScreenDeviceName ?? "";
            suppressSave = false;

            Stop();
            HasStartedSession = false;
            DidReachEndInStopMode = false;
        }

        public SettingsData ToSettingsData()
        {
            SettingsData data = new SettingsData();
            data.LanguageCode = LanguageCode;
            data.Script = ScriptTextSanitizer.Clean(Script);
            data.IsOverlayVisible = IsOverlayVisible;
            data.PrivacyModeEnabled = PrivacyModeEnabled;
            data.SpeedPointsPerSecond = SpeedPointsPerSecond;
            data.FontSize = FontSize;
            data.FontFamilyName = FontFamilyName;
            data.OverlayWidth = OverlayWidth;
            data.OverlayHeight = OverlayHeight;
            data.ScrollMode = ScrollMode;
            data.SelectedScreenDeviceName = SelectedScreenDeviceName;
            return data;
        }

        public void PasteScript(string text)
        {
            if (text == null || text.Trim().Length == 0)
            {
                return;
            }

            bool wasEmpty = Script.Trim().Length == 0;
            Script = text;
            if (wasEmpty)
            {
                HasStartedSession = true;
            }
        }

        public void ResetScroll()
        {
            DidReachEndInStopMode = false;
            OnResetRequested();
        }

        public void JumpBack(double seconds)
        {
            if (seconds <= 0)
            {
                return;
            }

            DidReachEndInStopMode = false;
            OnJumpBackRequested(SpeedPointsPerSecond * seconds);
        }

        public void SwitchPlaybackModeFromOverlayControl()
        {
            if (IsRunning)
            {
                Stop();
                ManualScrollEnabled = true;
                DidReachEndInStopMode = false;
                HasStartedSession = true;
                return;
            }

            ManualScrollEnabled = false;
            Start();
        }

        public void HandleManualScroll(double deltaPoints)
        {
            if (Math.Abs(deltaPoints) <= 0.01)
            {
                return;
            }

            if (!ManualScrollEnabled)
            {
                ManualScrollEnabled = true;
            }

            if (IsRunning)
            {
                Stop();
            }

            DidReachEndInStopMode = false;
            HasStartedSession = true;
            OnManualScrollRequested(deltaPoints);
        }

        public void ToggleRunning()
        {
            if (IsRunning)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            ManualScrollEnabled = false;

            if (ScrollMode == ScrollMode.StopAtEnd && DidReachEndInStopMode)
            {
                ResetScroll();
            }

            BeginRunningNow();
        }

        public void MarkReachedEndInStopMode()
        {
            if (ScrollMode != ScrollMode.StopAtEnd)
            {
                return;
            }

            DidReachEndInStopMode = true;
            Stop();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void AdjustSpeed(double delta)
        {
            SpeedPointsPerSecond = SpeedPointsPerSecond + delta;
        }

        public void AdjustFontSize(double delta)
        {
            FontSize = FontSize + delta;
        }

        private void BeginRunningNow()
        {
            HasStartedSession = true;
            IsRunning = true;
        }

        private double ClampedSpeed(double value)
        {
            double clamped = Clamp(value, 10, 300);
            return Math.Round(clamped / SpeedStep) * SpeedStep;
        }

        private static double Clamp(double value, double lower, double upper)
        {
            return Math.Min(Math.Max(value, lower), upper);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void OnResetRequested()
        {
            EventHandler handler = ResetRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnJumpBackRequested(double distancePoints)
        {
            EventHandler<JumpBackEventArgs> handler = JumpBackRequested;
            if (handler != null)
            {
                handler(this, new JumpBackEventArgs(distancePoints));
            }
        }

        private void OnManualScrollRequested(double deltaPoints)
        {
            EventHandler<ManualScrollEventArgs> handler = ManualScrollRequested;
            if (handler != null)
            {
                handler(this, new ManualScrollEventArgs(deltaPoints));
            }
        }

        private void SuggestSave()
        {
            if (suppressSave)
            {
                return;
            }

            EventHandler handler = SaveSuggested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
