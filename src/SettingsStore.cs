using System;
using System.IO;
using System.Xml.Serialization;

namespace PromptBar
{
    public sealed class SettingsData
    {
        public SettingsData()
        {
            LanguageCode = Localization.DefaultLanguageCode;
            Script = Localization.Text(LanguageCode, "ScriptDefault");
            IsOverlayVisible = true;
            PrivacyModeEnabled = true;
            SpeedPointsPerSecond = 80;
            FontSize = 20;
            FontFamilyName = PrompterModel.DefaultFontFamilyName;
            OverlayWidth = 600;
            OverlayHeight = 150;
            ScrollMode = ScrollMode.Infinite;
            SelectedScreenDeviceName = "";
            Hotkeys = new HotkeyProfile();
        }

        public string Script { get; set; }
        public string LanguageCode { get; set; }
        public bool IsOverlayVisible { get; set; }
        public bool PrivacyModeEnabled { get; set; }
        public double SpeedPointsPerSecond { get; set; }
        public double FontSize { get; set; }
        public string FontFamilyName { get; set; }
        public double OverlayWidth { get; set; }
        public double OverlayHeight { get; set; }
        public ScrollMode ScrollMode { get; set; }
        public string SelectedScreenDeviceName { get; set; }
        public HotkeyProfile Hotkeys { get; set; }
    }

    public sealed class SettingsStore
    {
        private readonly string settingsPath;
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(SettingsData));

        public SettingsStore()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directory = Path.Combine(appData, "PromptBar");
            settingsPath = Path.Combine(directory, "settings.xml");

            string legacyPath = Path.Combine(appData, "Notchprompt", "settings.xml");
            if (!File.Exists(settingsPath) && File.Exists(legacyPath))
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.Copy(legacyPath, settingsPath, false);
                }
                catch
                {
                    // Migration is best effort; default settings are fine if it fails.
                }
            }
        }

        public SettingsData Load()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return new SettingsData();
                }

                using (FileStream stream = File.OpenRead(settingsPath))
                {
                    SettingsData data = serializer.Deserialize(stream) as SettingsData;
                    return data ?? new SettingsData();
                }
            }
            catch
            {
                return new SettingsData();
            }
        }

        public void Save(SettingsData data)
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (FileStream stream = File.Create(settingsPath))
                {
                    serializer.Serialize(stream, data);
                }
            }
            catch
            {
                // Settings should never prevent the prompter from running.
            }
        }
    }
}
