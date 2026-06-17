using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace PromptBar
{
    public sealed class HotkeyManager : IDisposable
    {
        private readonly Action<ShortcutCommand> onCommand;
        private readonly Dictionary<int, ShortcutCommand> commandById = new Dictionary<int, ShortcutCommand>();
        private readonly List<ShortcutCommand> failedRegistrations = new List<ShortcutCommand>();
        private HotkeyProfile profile;
        private HwndSource source;

        public HotkeyManager(HotkeyProfile profile, Action<ShortcutCommand> onCommand)
        {
            this.profile = profile ?? new HotkeyProfile();
            this.onCommand = onCommand;
        }

        public IList<ShortcutCommand> FailedRegistrations
        {
            get { return failedRegistrations.AsReadOnly(); }
        }

        public void RegisterAll()
        {
            UnregisterAll();

            HwndSourceParameters parameters = new HwndSourceParameters("PromptBarHotkeys");
            parameters.Width = 0;
            parameters.Height = 0;
            parameters.WindowStyle = 0x800000;
            source = new HwndSource(parameters);
            source.AddHook(WndProc);

            Register(ShortcutCommand.StartPause, 1);
            Register(ShortcutCommand.Reset, 2);
            Register(ShortcutCommand.JumpBack, 3);
            Register(ShortcutCommand.TogglePrivacy, 4);
            Register(ShortcutCommand.ToggleOverlay, 5);
            Register(ShortcutCommand.SpeedUp, 6);
            Register(ShortcutCommand.SpeedDown, 7);
            Register(ShortcutCommand.FontSizeUp, 8);
            Register(ShortcutCommand.FontSizeDown, 9);
        }

        public void ApplyProfile(HotkeyProfile newProfile)
        {
            profile = newProfile ?? new HotkeyProfile();
            RegisterAll();
        }

        public void UnregisterAll()
        {
            if (source != null)
            {
                foreach (int id in commandById.Keys)
                {
                    NativeMethods.UnregisterHotKey(source.Handle, id);
                }

                source.RemoveHook(WndProc);
                source.Dispose();
                source = null;
            }

            commandById.Clear();
            failedRegistrations.Clear();
        }

        public void Dispose()
        {
            UnregisterAll();
        }

        public static string DisplayShortcut(ShortcutCommand command)
        {
            return new HotkeyProfile().Get(command).DisplayText;
        }

        private void Register(ShortcutCommand command, int id)
        {
            HotkeyGesture gesture = profile.Get(command);
            if (gesture == null || gesture.IsEmpty)
            {
                return;
            }

            bool ok = source != null && NativeMethods.RegisterHotKey(source.Handle, id, gesture.Modifiers, (uint)gesture.VirtualKey);
            if (ok)
            {
                commandById[id] = command;
            }
            else
            {
                failedRegistrations.Add(command);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != NativeMethods.WM_HOTKEY)
            {
                return IntPtr.Zero;
            }

            int id = wParam.ToInt32();
            ShortcutCommand command;
            if (commandById.TryGetValue(id, out command))
            {
                handled = true;
                if (onCommand != null)
                {
                    onCommand(command);
                }
            }

            return IntPtr.Zero;
        }
    }
}
