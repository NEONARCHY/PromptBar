using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace PromptBar
{
    public sealed class HotkeyProfile
    {
        public HotkeyProfile()
        {
            StartPause = HotkeyGesture.FromVirtualKey(NativeMethods.VK_P, true, true, false, false);
            Reset = HotkeyGesture.FromVirtualKey(NativeMethods.VK_R, true, true, false, false);
            JumpBack = HotkeyGesture.FromVirtualKey(NativeMethods.VK_J, true, true, false, false);
            TogglePrivacy = HotkeyGesture.FromVirtualKey(NativeMethods.VK_H, true, true, false, false);
            ToggleOverlay = HotkeyGesture.FromVirtualKey(NativeMethods.VK_O, true, true, false, false);
            SpeedUp = HotkeyGesture.FromVirtualKey(NativeMethods.VK_OEM_PLUS, true, true, false, false);
            SpeedDown = HotkeyGesture.FromVirtualKey(NativeMethods.VK_OEM_MINUS, true, true, false, false);
            FontSizeUp = HotkeyGesture.FromVirtualKey(NativeMethods.VK_OEM_6, true, true, false, false);
            FontSizeDown = HotkeyGesture.FromVirtualKey(NativeMethods.VK_OEM_4, true, true, false, false);
        }

        public HotkeyGesture StartPause { get; set; }
        public HotkeyGesture Reset { get; set; }
        public HotkeyGesture JumpBack { get; set; }
        public HotkeyGesture TogglePrivacy { get; set; }
        public HotkeyGesture ToggleOverlay { get; set; }
        public HotkeyGesture SpeedUp { get; set; }
        public HotkeyGesture SpeedDown { get; set; }
        public HotkeyGesture FontSizeUp { get; set; }
        public HotkeyGesture FontSizeDown { get; set; }

        public HotkeyGesture Get(ShortcutCommand command)
        {
            if (command == ShortcutCommand.StartPause) return StartPause;
            if (command == ShortcutCommand.Reset) return Reset;
            if (command == ShortcutCommand.JumpBack) return JumpBack;
            if (command == ShortcutCommand.TogglePrivacy) return TogglePrivacy;
            if (command == ShortcutCommand.ToggleOverlay) return ToggleOverlay;
            if (command == ShortcutCommand.SpeedUp) return SpeedUp;
            if (command == ShortcutCommand.SpeedDown) return SpeedDown;
            if (command == ShortcutCommand.FontSizeUp) return FontSizeUp;
            return FontSizeDown;
        }

        public void Set(ShortcutCommand command, HotkeyGesture gesture)
        {
            if (command == ShortcutCommand.StartPause) StartPause = gesture;
            else if (command == ShortcutCommand.Reset) Reset = gesture;
            else if (command == ShortcutCommand.JumpBack) JumpBack = gesture;
            else if (command == ShortcutCommand.TogglePrivacy) TogglePrivacy = gesture;
            else if (command == ShortcutCommand.ToggleOverlay) ToggleOverlay = gesture;
            else if (command == ShortcutCommand.SpeedUp) SpeedUp = gesture;
            else if (command == ShortcutCommand.SpeedDown) SpeedDown = gesture;
            else if (command == ShortcutCommand.FontSizeUp) FontSizeUp = gesture;
            else if (command == ShortcutCommand.FontSizeDown) FontSizeDown = gesture;
        }

        public IEnumerable<ShortcutCommand> Commands
        {
            get
            {
                yield return ShortcutCommand.StartPause;
                yield return ShortcutCommand.Reset;
                yield return ShortcutCommand.JumpBack;
                yield return ShortcutCommand.TogglePrivacy;
                yield return ShortcutCommand.ToggleOverlay;
                yield return ShortcutCommand.SpeedUp;
                yield return ShortcutCommand.SpeedDown;
                yield return ShortcutCommand.FontSizeUp;
                yield return ShortcutCommand.FontSizeDown;
            }
        }

        public string HelpText()
        {
            return HelpText(Localization.DefaultLanguageCode);
        }

        public string HelpText(string languageCode)
        {
            return
                Get(ShortcutCommand.StartPause).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.StartPause) + "\r\n" +
                Get(ShortcutCommand.Reset).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.Reset) + "\r\n" +
                Get(ShortcutCommand.JumpBack).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.JumpBack) + "\r\n" +
                Get(ShortcutCommand.TogglePrivacy).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.TogglePrivacy) + "\r\n" +
                Get(ShortcutCommand.ToggleOverlay).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.ToggleOverlay) + "\r\n" +
                Get(ShortcutCommand.SpeedUp).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.SpeedUp) + "\r\n" +
                Get(ShortcutCommand.SpeedDown).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.SpeedDown) + "\r\n" +
                Get(ShortcutCommand.FontSizeUp).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.FontSizeUp) + "\r\n" +
                Get(ShortcutCommand.FontSizeDown).DisplayTextFor(languageCode) + "  " + Localization.CommandText(languageCode, ShortcutCommand.FontSizeDown) + "\r\n\r\n" +
                Localization.Text(languageCode, "TextEditingShortcutsHeader") + "\r\n" +
                "Ctrl+A  " + Localization.Text(languageCode, "SelectAll") + "\r\n" +
                "Ctrl+C  " + Localization.Text(languageCode, "Copy") + "\r\n" +
                "Ctrl+V  " + Localization.Text(languageCode, "Paste");
        }
    }

    public sealed class HotkeyGesture
    {
        public HotkeyGesture()
        {
        }

        public int VirtualKey { get; set; }
        public bool Control { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }

        public bool IsEmpty
        {
            get { return VirtualKey == 0; }
        }

        public uint Modifiers
        {
            get
            {
                uint modifiers = NativeMethods.MOD_NOREPEAT;
                if (Alt) modifiers |= NativeMethods.MOD_ALT;
                if (Control) modifiers |= NativeMethods.MOD_CONTROL;
                if (Shift) modifiers |= NativeMethods.MOD_SHIFT;
                if (Win) modifiers |= NativeMethods.MOD_WIN;
                return modifiers;
            }
        }

        public string DisplayText
        {
            get
            {
                return DisplayTextFor(Localization.DefaultLanguageCode);
            }
        }

        public string DisplayTextFor(string languageCode)
        {
            if (IsEmpty)
            {
                return Localization.Text(languageCode, "HotkeyUnassigned");
            }

            List<string> parts = new List<string>();
            if (Control) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            parts.Add(KeyDisplayName(VirtualKey));
            return String.Join("+", parts.ToArray());
        }

        public static HotkeyGesture FromVirtualKey(int virtualKey, bool control, bool alt, bool shift, bool win)
        {
            HotkeyGesture gesture = new HotkeyGesture();
            gesture.VirtualKey = virtualKey;
            gesture.Control = control;
            gesture.Alt = alt;
            gesture.Shift = shift;
            gesture.Win = win;
            return gesture;
        }

        public static HotkeyGesture FromInput(Key key, ModifierKeys modifiers)
        {
            Key actualKey = key;
            if (actualKey == Key.None ||
                actualKey == Key.LeftCtrl || actualKey == Key.RightCtrl ||
                actualKey == Key.LeftAlt || actualKey == Key.RightAlt ||
                actualKey == Key.LeftShift || actualKey == Key.RightShift ||
                actualKey == Key.LWin || actualKey == Key.RWin)
            {
                return null;
            }

            int virtualKey = KeyInterop.VirtualKeyFromKey(actualKey);
            if (virtualKey == 0)
            {
                return null;
            }

            return FromVirtualKey(
                virtualKey,
                (modifiers & ModifierKeys.Control) == ModifierKeys.Control,
                (modifiers & ModifierKeys.Alt) == ModifierKeys.Alt,
                (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift,
                (modifiers & ModifierKeys.Windows) == ModifierKeys.Windows);
        }

        public bool SameAs(HotkeyGesture other)
        {
            if (other == null)
            {
                return false;
            }

            return VirtualKey == other.VirtualKey &&
                Control == other.Control &&
                Alt == other.Alt &&
                Shift == other.Shift &&
                Win == other.Win;
        }

        private static string KeyDisplayName(int virtualKey)
        {
            if (virtualKey >= 0x30 && virtualKey <= 0x39)
            {
                return ((char)virtualKey).ToString();
            }

            if (virtualKey >= 0x41 && virtualKey <= 0x5A)
            {
                return ((char)virtualKey).ToString();
            }

            if (virtualKey >= 0x70 && virtualKey <= 0x87)
            {
                return "F" + (virtualKey - 0x6F).ToString();
            }

            if (virtualKey == NativeMethods.VK_OEM_PLUS) return "=";
            if (virtualKey == NativeMethods.VK_OEM_MINUS) return "-";
            if (virtualKey == NativeMethods.VK_OEM_4) return "[";
            if (virtualKey == NativeMethods.VK_OEM_6) return "]";
            if (virtualKey == NativeMethods.VK_OEM_1) return ";";
            if (virtualKey == NativeMethods.VK_OEM_2) return "/";
            if (virtualKey == NativeMethods.VK_OEM_3) return "`";
            if (virtualKey == NativeMethods.VK_OEM_5) return "\\";
            if (virtualKey == NativeMethods.VK_OEM_7) return "'";
            if (virtualKey == NativeMethods.VK_OEM_COMMA) return ",";
            if (virtualKey == NativeMethods.VK_OEM_PERIOD) return ".";

            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            return key.ToString();
        }
    }
}
