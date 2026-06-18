# Changelog

## 0.6.4 - 2026-06-18

- Restored transparent rounded corners for the settings window shell.
- Changed the floating overlay shape to a straight top edge with rounded bottom corners.

## 0.6.3 - 2026-06-18

- Removed the native DWM/backdrop fill from the floating overlay so transparent rounded corners are preserved.

## 0.6.2 - 2026-06-18

- Rounded the floating overlay shell on all corners.
- Darkened the overlay, settings window, cards, inputs, and controls for a deeper UI tone.

## 0.6.1 - 2026-06-18

- Made the settings navigation rail clickable.
- Split settings into real pages instead of showing every section inside General.
- Added safer per-page refresh logic for settings controls.

## 0.6.0 - 2026-06-18

- Reworked the settings window toward a calmer native Windows 11 Fluent style.
- Added separate portable builds for automatic, Windows 11, and Windows 10 visual modes.
- Made settings rendering stable by avoiding fragile transparent WPF/DWM composition for the main app window.
- Kept safe DWM dark mode and rounded-corner integration for the settings window.

## 0.5.0 - 2026-06-17

- Moved settings to a fuller Windows Fluent style layout with navigation rail, central setting cards, and an about panel.
- Strengthened native backdrop support with DWM glass frame, blur-behind, Acrylic, and Mica fallbacks.
- Applied the stronger Acrylic-style backdrop to the floating overlay.

## 0.4.0 - 2026-06-17

- Restyled the overlay and settings window with a Mica/Aero inspired glass design.
- Added best-effort native Windows backdrop and blur effects.
- Refined glass panels, controls, buttons, toggles, and sliders for a lighter interface.

## 0.3.1 - 2026-06-17

- Fixed a tiny visible top-edge gap by slightly bleeding the overlay above the screen edge.
- Cleaned up Windows executable copyright metadata.

## 0.3.0 - 2026-06-17

- Renamed the Windows app to PromptBar.
- Added single-file portable build output: `dist\PromptBarPortable.exe`.
- Added liquid/glass inspired overlay and settings UI.
- Added inline editing directly inside the overlay.
- Added resize mode with subtle drag handles.
- Added editable global hotkeys and hotkey help.
- Added font size and font family controls.
- Added multi-language UI support.
- Added TXT, MD, RTF, and DOCX import/export.
- Added best-effort screen capture privacy mode.

## 0.1.0 - 2026-06-16

- Initial native Windows/WPF port.
