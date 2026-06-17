using System;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;

namespace PromptBar
{
    public sealed class TrayController : IDisposable
    {
        private readonly PrompterModel model;
        private readonly AppController controller;
        private readonly System.Windows.Forms.NotifyIcon notifyIcon;
        private readonly System.Windows.Forms.ContextMenuStrip menu;
        private readonly System.Windows.Forms.ToolStripSeparator warningSeparator;
        private HotkeyProfile hotkeyProfile;
        private System.Windows.Forms.ToolStripMenuItem warningItem;
        private readonly System.Windows.Forms.ToolStripMenuItem startPauseItem;
        private readonly System.Windows.Forms.ToolStripMenuItem resetItem;
        private readonly System.Windows.Forms.ToolStripMenuItem jumpBackItem;
        private readonly System.Windows.Forms.ToolStripMenuItem privacyItem;
        private readonly System.Windows.Forms.ToolStripMenuItem overlayItem;
        private readonly System.Windows.Forms.ToolStripMenuItem speedUpItem;
        private readonly System.Windows.Forms.ToolStripMenuItem speedDownItem;
        private readonly System.Windows.Forms.ToolStripMenuItem fontSizeUpItem;
        private readonly System.Windows.Forms.ToolStripMenuItem fontSizeDownItem;
        private readonly System.Windows.Forms.ToolStripMenuItem hotkeyHelpItem;
        private readonly System.Windows.Forms.ToolStripMenuItem settingsItem;
        private readonly System.Windows.Forms.ToolStripMenuItem quitItem;

        public TrayController(PrompterModel model, AppController controller, HotkeyProfile hotkeyProfile, IList<ShortcutCommand> failedHotkeys)
        {
            this.model = model;
            this.controller = controller;
            this.hotkeyProfile = hotkeyProfile ?? new HotkeyProfile();

            menu = new System.Windows.Forms.ContextMenuStrip();

            warningItem = new System.Windows.Forms.ToolStripMenuItem("");
            warningItem.Enabled = false;
            menu.Items.Add(warningItem);
            warningSeparator = new System.Windows.Forms.ToolStripSeparator();
            menu.Items.Add(warningSeparator);
            RefreshHotkeyProfile(this.hotkeyProfile, failedHotkeys);

            startPauseItem = AddItem("", delegate { model.ToggleRunning(); });
            resetItem = AddItem("", delegate { model.ResetScroll(); });
            jumpBackItem = AddItem("", delegate { model.JumpBack(5); });
            privacyItem = AddItem("", delegate { model.PrivacyModeEnabled = !model.PrivacyModeEnabled; });
            overlayItem = AddItem("", delegate { model.IsOverlayVisible = !model.IsOverlayVisible; });
            speedUpItem = AddItem("", delegate { model.AdjustSpeed(PrompterModel.SpeedStep); });
            speedDownItem = AddItem("", delegate { model.AdjustSpeed(-PrompterModel.SpeedStep); });
            fontSizeUpItem = AddItem("", delegate { model.AdjustFontSize(1); });
            fontSizeDownItem = AddItem("", delegate { model.AdjustFontSize(-1); });
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            hotkeyHelpItem = AddItem("", delegate { ShowHotkeyHelp(); });
            settingsItem = AddItem("", delegate { controller.ShowSettings(); });
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            quitItem = AddItem("", delegate { controller.Quit(); });

            menu.Opening += delegate { RefreshMenuState(); };
            model.PropertyChanged += ModelOnPropertyChanged;

            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application;
            notifyIcon.Text = "PromptBar";
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { controller.ShowSettings(); };
            RefreshMenuText();
        }

        public void Dispose()
        {
            model.PropertyChanged -= ModelOnPropertyChanged;
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            menu.Dispose();
        }

        public void RefreshHotkeyProfile(HotkeyProfile profile, IList<ShortcutCommand> failedHotkeys)
        {
            hotkeyProfile = profile ?? new HotkeyProfile();
            bool hasFailures = failedHotkeys != null && failedHotkeys.Count > 0;
            warningItem.Visible = hasFailures;
            warningSeparator.Visible = hasFailures;
            if (hasFailures)
            {
                warningItem.Text = BuildHotkeyWarning(failedHotkeys);
            }
        }

        private System.Windows.Forms.ToolStripMenuItem AddItem(string title, EventHandler handler)
        {
            System.Windows.Forms.ToolStripMenuItem item = new System.Windows.Forms.ToolStripMenuItem(title);
            item.Click += handler;
            menu.Items.Add(item);
            return item;
        }

        private void RefreshMenuState()
        {
            RefreshMenuText();
            privacyItem.Checked = model.PrivacyModeEnabled;
            overlayItem.Checked = model.IsOverlayVisible;
        }

        private void RefreshMenuText()
        {
            startPauseItem.Text = model.IsRunning ? T("Pause") : T("Start");
            resetItem.Text = T("ResetScroll");
            jumpBackItem.Text = T("JumpBack5s");
            privacyItem.Text = T("PrivacyMode");
            overlayItem.Text = T("ShowOverlayMenu");
            speedUpItem.Text = T("IncreaseSpeed");
            speedDownItem.Text = T("DecreaseSpeed");
            fontSizeUpItem.Text = T("IncreaseFontSize");
            fontSizeDownItem.Text = T("DecreaseFontSize");
            hotkeyHelpItem.Text = T("HotkeyHelp");
            settingsItem.Text = T("SettingsMenu");
            quitItem.Text = T("QuitPromptBar");
        }

        private string BuildHotkeyWarning(IList<ShortcutCommand> failedHotkeys)
        {
            if (failedHotkeys.Count == 1)
            {
                return T("ShortcutUnavailable") + ": " + HotkeyManager.DisplayShortcut(failedHotkeys[0]);
            }

            return T("ShortcutsUnavailable") + ": " + failedHotkeys.Count.ToString();
        }

        private void ShowHotkeyHelp()
        {
            System.Windows.Forms.MessageBox.Show(
                hotkeyProfile.HelpText(model.LanguageCode),
                T("HotkeyDialogTitle"),
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        private void ModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LanguageCode" || e.PropertyName == "IsRunning")
            {
                RefreshMenuText();
            }
        }

        private string T(string key)
        {
            return Localization.Text(model.LanguageCode, key);
        }
    }
}
