using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using DrawingGraphics = System.Drawing.Graphics;

namespace PromptBar
{
    public sealed class OverlayWindow : Window
    {
        private const double ScreenTopBleed = 2.0;
        private readonly PrompterModel model;
        private readonly Action showSettings;
        private readonly Action showHotkeyHelp;
        private TextBlock playbackIcon;
        private TextBlock resizeIcon;
        private DockPanel controlsPanel;
        private ScrollingTextControl scroller;
        private TextBox inlineEditor;
        private Grid resizeLayer;
        private bool isResizeMode;
        private bool isUpdatingInlineEditor;
        private IntPtr handle;

        public OverlayWindow(PrompterModel model, Action showSettings, Action showHotkeyHelp)
        {
            this.model = model;
            this.showSettings = showSettings;
            this.showHotkeyHelp = showHotkeyHelp;

            Title = "PromptBar Overlay";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = false;
            SnapsToDevicePixels = false;
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            Width = model.OverlayWidth;
            Height = model.OverlayHeight;

            Content = BuildContent();

            SourceInitialized += OverlayWindowOnSourceInitialized;
            Loaded += delegate { Reposition(); };
            model.PropertyChanged += ModelOnPropertyChanged;
            SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
        }

        public void ShowOrHide(bool visible)
        {
            if (visible)
            {
                Reposition();
                Show();
                Topmost = false;
                Topmost = true;
            }
            else
            {
                Hide();
            }
        }

        public void Reposition()
        {
            Width = model.OverlayWidth;
            Height = model.OverlayHeight;

            System.Windows.Forms.Screen screen = ResolveTargetScreen();
            double scaleX;
            double scaleY;
            GetSystemDpiScale(out scaleX, out scaleY);

            double screenLeft = screen.Bounds.Left / scaleX;
            double screenTop = screen.Bounds.Top / scaleY;
            double screenWidth = screen.Bounds.Width / scaleX;

            Left = Math.Round(screenLeft + ((screenWidth - Width) / 2.0));
            Top = Math.Floor(screenTop - ScreenTopBleed);
        }

        public void ApplyPrivacyMode()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (model.PrivacyModeEnabled)
            {
                bool ok = NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
                if (!ok)
                {
                    NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_MONITOR);
                }
            }
            else
            {
                NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_NONE);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;
            model.PropertyChanged -= ModelOnPropertyChanged;
            base.OnClosed(e);
        }

        private UIElement BuildContent()
        {
            CornerRadius shellRadius = new CornerRadius(0, 0, 36, 36);

            Border shell = new Border();
            shell.Background = ShellBrush();
            shell.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, 255, 255, 255));
            shell.BorderThickness = new Thickness(1);
            shell.CornerRadius = shellRadius;
            shell.SnapsToDevicePixels = false;
            shell.Effect = new DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0, 0, 0),
                BlurRadius = 24,
                ShadowDepth = 6,
                Opacity = 0.34
            };

            Grid root = new Grid();
            shell.Child = root;

            Border aeroWash = new Border();
            aeroWash.CornerRadius = shellRadius;
            aeroWash.Background = ShellAeroBrush();
            aeroWash.Opacity = 0.14;
            aeroWash.IsHitTestVisible = false;
            root.Children.Add(aeroWash);

            Border glassWash = new Border();
            glassWash.CornerRadius = shellRadius;
            glassWash.Background = ShellShineBrush();
            glassWash.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255));
            glassWash.BorderThickness = new Thickness(0, 0, 0, 1);
            glassWash.IsHitTestVisible = false;
            root.Children.Add(glassWash);

            Grid textHost = new Grid();
            textHost.Margin = new Thickness(18, 58, 18, 16);
            textHost.Background = Brushes.Transparent;
            textHost.MouseLeftButtonDown += delegate
            {
                BeginInlineEdit();
            };
            root.Children.Add(textHost);

            scroller = new ScrollingTextControl(model);
            textHost.Children.Add(scroller);

            inlineEditor = InlineEditor();
            textHost.Children.Add(inlineEditor);

            controlsPanel = new DockPanel();
            controlsPanel.Margin = new Thickness(10, 8, 10, 0);
            controlsPanel.VerticalAlignment = VerticalAlignment.Top;
            controlsPanel.LastChildFill = true;
            controlsPanel.Opacity = 0.78;
            controlsPanel.MouseEnter += delegate { FadeControlsTo(1.0); };
            controlsPanel.MouseLeave += delegate { FadeControlsTo(0.78); };
            root.Children.Add(controlsPanel);

            StackPanel left = ControlStack();
            Border leftGroup = ControlGroup(left);
            leftGroup.HorizontalAlignment = HorizontalAlignment.Left;
            DockPanel.SetDock(leftGroup, Dock.Left);
            controlsPanel.Children.Add(leftGroup);

            Button playbackButton = IconButton(PlaybackGlyph(), T("StartPause"), delegate { model.SwitchPlaybackModeFromOverlayControl(); });
            playbackIcon = playbackButton.Content as TextBlock;
            left.Children.Add(playbackButton);
            left.Children.Add(IconButton("\uE72B", T("JumpBack5s"), delegate { model.JumpBack(5); }));
            Button resizeButton = IconButton("\uE740", T("OverlayResize"), delegate { ToggleResizeMode(); });
            resizeIcon = resizeButton.Content as TextBlock;
            left.Children.Add(resizeButton);
            left.Children.Add(IconButton("\uE713", T("OverlaySettings"), delegate
            {
                if (this.showSettings != null)
                {
                    this.showSettings();
                }
            }));
            left.Children.Add(IconButton("\uE897", T("OverlayHotkeyHelp"), delegate
            {
                if (this.showHotkeyHelp != null)
                {
                    this.showHotkeyHelp();
                }
            }));

            StackPanel right = ControlStack();
            Border rightGroup = ControlGroup(right);
            rightGroup.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(rightGroup, Dock.Right);
            controlsPanel.Children.Add(rightGroup);

            right.Children.Add(IconButton("\uE77F", T("OverlayPaste"), delegate
            {
                if (Clipboard.ContainsText())
                {
                    model.PasteScript(Clipboard.GetText());
                }
            }));
            right.Children.Add(IconButton("\uE8C8", T("OverlayCopy"), delegate
            {
                Clipboard.SetText(model.Script ?? "");
            }));
            right.Children.Add(IconButton("\uE74D", T("OverlayClear"), delegate { model.Script = ""; }));
            right.Children.Add(RepeatIconButton("\uE738", T("DecreaseSpeed"), delegate { model.AdjustSpeed(-PrompterModel.SpeedStep); }));
            right.Children.Add(RepeatIconButton("\uE710", T("IncreaseSpeed"), delegate { model.AdjustSpeed(PrompterModel.SpeedStep); }));
            right.Children.Add(RepeatTextButton("A-", T("DecreaseFontSize"), delegate { model.AdjustFontSize(-1); }));
            right.Children.Add(RepeatTextButton("A+", T("IncreaseFontSize"), delegate { model.AdjustFontSize(1); }));
            right.Children.Add(IconButton("\uE711", T("OverlayQuit"), delegate { Application.Current.Shutdown(); }));

            resizeLayer = BuildResizeLayer();
            root.Children.Add(resizeLayer);

            return shell;
        }

        private TextBox InlineEditor()
        {
            TextBox editor = new TextBox();
            editor.Visibility = Visibility.Collapsed;
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.TextWrapping = TextWrapping.Wrap;
            editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editor.BorderThickness = new Thickness(1);
            editor.Padding = new Thickness(14, 11, 14, 11);
            editor.Background = EditorBrush();
            editor.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 255, 255, 255));
            editor.Foreground = Brushes.White;
            editor.CaretBrush = Brushes.White;
            editor.SelectionBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(110, 76, 194, 255));
            editor.FocusVisualStyle = null;
            editor.FontFamily = new FontFamily(model.FontFamilyName);
            editor.FontSize = model.FontSize;
            editor.FontWeight = FontWeights.SemiBold;
            editor.Style = GlassEditorStyle();
            SpellCheck.SetIsEnabled(editor, false);
            editor.TextChanged += delegate
            {
                if (isUpdatingInlineEditor)
                {
                    return;
                }

                model.Script = editor.Text;
            };
            editor.LostKeyboardFocus += delegate
            {
                EndInlineEdit();
            };
            editor.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (HandleTextEditorShortcut(editor, e))
                {
                    return;
                }

                if (e.Key == Key.Escape)
                {
                    EndInlineEdit();
                    e.Handled = true;
                }
            };
            return editor;
        }

        private bool HandleTextEditorShortcut(TextBox editor, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return false;
            }

            if (key == Key.A)
            {
                editor.SelectAll();
                e.Handled = true;
                return true;
            }

            if (key == Key.C)
            {
                editor.Copy();
                e.Handled = true;
                return true;
            }

            if (key == Key.V)
            {
                if (Clipboard.ContainsText())
                {
                    editor.Paste();
                }

                e.Handled = true;
                return true;
            }

            return false;
        }

        private string T(string key)
        {
            return Localization.Text(model.LanguageCode, key);
        }

        private Brush EditorBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(142, 39, 43, 49), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(104, 10, 12, 18), 1));
            return brush;
        }

        private StackPanel ControlStack()
        {
            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Horizontal;
            panel.Margin = new Thickness(0);
            return panel;
        }

        private Border ControlGroup(StackPanel content)
        {
            Border group = new Border();
            group.Child = content;
            group.Padding = new Thickness(4, 3, 4, 3);
            group.CornerRadius = new CornerRadius(18);
            group.Background = ControlGroupBrush();
            group.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
            group.BorderThickness = new Thickness(1);
            group.SnapsToDevicePixels = false;
            group.Effect = new DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(0, 0, 0),
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.28
            };
            return group;
        }

        private Button IconButton(string glyph, string tooltip, RoutedEventHandler click)
        {
            Button button = new Button();
            button.Content = IconText(glyph);
            button.ToolTip = tooltip;
            button.Width = 25;
            button.Height = 25;
            button.Margin = new Thickness(2);
            button.Padding = new Thickness(0);
            button.Background = GlassButtonBrush();
            button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
            button.Foreground = Brushes.White;
            button.Cursor = Cursors.Hand;
            button.Style = GlassButtonStyle();
            button.Click += click;
            return button;
        }

        private RepeatButton RepeatIconButton(string glyph, string tooltip, RoutedEventHandler click)
        {
            RepeatButton button = new RepeatButton();
            button.Content = IconText(glyph);
            button.ToolTip = tooltip;
            button.Width = 25;
            button.Height = 25;
            button.Margin = new Thickness(2);
            button.Padding = new Thickness(0);
            button.Delay = 280;
            button.Interval = 85;
            button.Background = GlassButtonBrush();
            button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
            button.Foreground = Brushes.White;
            button.Cursor = Cursors.Hand;
            button.Style = GlassRepeatButtonStyle();
            button.Click += click;
            return button;
        }

        private RepeatButton RepeatTextButton(string label, string tooltip, RoutedEventHandler click)
        {
            RepeatButton button = new RepeatButton();
            TextBlock text = new TextBlock();
            text.Text = label;
            text.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            text.FontSize = 9.5;
            text.FontWeight = FontWeights.SemiBold;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextAlignment = TextAlignment.Center;

            button.Content = text;
            button.ToolTip = tooltip;
            button.Width = 25;
            button.Height = 25;
            button.Margin = new Thickness(2);
            button.Padding = new Thickness(0);
            button.Delay = 280;
            button.Interval = 85;
            button.Background = GlassButtonBrush();
            button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255));
            button.Foreground = Brushes.White;
            button.Cursor = Cursors.Hand;
            button.Style = GlassRepeatButtonStyle();
            button.Click += click;
            return button;
        }

        private TextBlock IconText(string glyph)
        {
            TextBlock text = new TextBlock();
            text.Text = glyph;
            text.FontFamily = new FontFamily("Segoe MDL2 Assets");
            text.FontSize = 11.5;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            return text;
        }

        private Brush ShellBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(246, 14, 16, 22), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(248, 7, 8, 13), 0.62));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(252, 1, 2, 5), 1));
            return brush;
        }

        private Brush ShellAeroBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(8, 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(4, 76, 194, 255), 0.46));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 0.74));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 1));
            return brush;
        }

        private Brush ShellShineBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(16, 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(4, 255, 255, 255), 0.3));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 255, 255, 255), 0.7));
            return brush;
        }

        private Brush ControlGroupBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(70, 38, 40, 49), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(50, 20, 22, 29), 0.56));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(34, 8, 9, 14), 1));
            return brush;
        }

        private Brush GlassButtonBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(28, 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(14, 255, 255, 255), 1));
            return brush;
        }

        private Style GlassButtonStyle()
        {
            return GlassButtonStyleFor(typeof(Button));
        }

        private Style GlassRepeatButtonStyle()
        {
            return GlassButtonStyleFor(typeof(RepeatButton));
        }

        private Style GlassButtonStyleFor(Type targetType)
        {
            Style style = new Style(targetType);
            style.Setters.Add(new Setter(Control.BackgroundProperty, GlassButtonBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(34, 255, 255, 255))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            chrome.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(targetType);
            template.VisualTree = chrome;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(46, 255, 255, 255)), "Chrome"));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(62, 255, 255, 255)), "Chrome"));
            template.Triggers.Add(hover);

            Trigger pressed = new Trigger();
            pressed.Property = ButtonBase.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(78, 76, 194, 255)), "Chrome"));
            pressed.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(92, 255, 255, 255)), "Chrome"));
            template.Triggers.Add(pressed);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style GlassEditorStyle()
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, EditorBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 255, 255, 255))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "EditorChrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory contentHost = new FrameworkElementFactory(typeof(ScrollViewer));
            contentHost.Name = "PART_ContentHost";
            contentHost.SetValue(UIElement.SnapsToDevicePixelsProperty, false);
            chrome.AppendChild(contentHost);

            ControlTemplate template = new ControlTemplate(typeof(TextBox));
            template.VisualTree = chrome;

            Trigger focused = new Trigger();
            focused.Property = UIElement.IsKeyboardFocusWithinProperty;
            focused.Value = true;
            focused.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 76, 194, 255)), "EditorChrome"));
            focused.Setters.Add(new Setter(Control.BackgroundProperty, EditorBrush(), "EditorChrome"));
            template.Triggers.Add(focused);

            Trigger disabled = new Trigger();
            disabled.Property = UIElement.IsEnabledProperty;
            disabled.Value = false;
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "EditorChrome"));
            template.Triggers.Add(disabled);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Grid BuildResizeLayer()
        {
            Grid layer = new Grid();
            layer.Visibility = Visibility.Collapsed;

            Thumb right = ResizeHitThumb(Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Stretch, 14, Double.NaN);
            right.DragDelta += delegate(object sender, DragDeltaEventArgs e)
            {
                model.OverlayWidth = model.OverlayWidth + (e.HorizontalChange * 2);
            };
            layer.Children.Add(right);

            Thumb bottom = ResizeHitThumb(Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, Double.NaN, 14);
            bottom.DragDelta += delegate(object sender, DragDeltaEventArgs e)
            {
                model.OverlayHeight = model.OverlayHeight + e.VerticalChange;
            };
            layer.Children.Add(bottom);

            Thumb corner = ResizeCornerThumb();
            corner.DragDelta += delegate(object sender, DragDeltaEventArgs e)
            {
                model.OverlayWidth = model.OverlayWidth + (e.HorizontalChange * 2);
                model.OverlayHeight = model.OverlayHeight + e.VerticalChange;
            };
            layer.Children.Add(corner);

            return layer;
        }

        private Thumb ResizeHitThumb(Cursor cursor, HorizontalAlignment horizontal, VerticalAlignment vertical, double width, double height)
        {
            Thumb thumb = new Thumb();
            thumb.HorizontalAlignment = horizontal;
            thumb.VerticalAlignment = vertical;
            thumb.Width = width;
            thumb.Height = height;
            thumb.Cursor = cursor;
            thumb.Background = Brushes.Transparent;
            thumb.BorderThickness = new Thickness(0);
            thumb.FocusVisualStyle = null;
            thumb.Style = InvisibleResizeThumbStyle();
            return thumb;
        }

        private Thumb ResizeCornerThumb()
        {
            Thumb thumb = new Thumb();
            thumb.HorizontalAlignment = HorizontalAlignment.Right;
            thumb.VerticalAlignment = VerticalAlignment.Bottom;
            thumb.Width = 34;
            thumb.Height = 34;
            thumb.Cursor = Cursors.SizeNWSE;
            thumb.Background = Brushes.Transparent;
            thumb.BorderThickness = new Thickness(0);
            thumb.FocusVisualStyle = null;
            thumb.Style = CornerResizeThumbStyle();
            return thumb;
        }

        private Style InvisibleResizeThumbStyle()
        {
            Style style = new Style(typeof(Thumb));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));

            FrameworkElementFactory hit = new FrameworkElementFactory(typeof(Border));
            hit.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));

            ControlTemplate template = new ControlTemplate(typeof(Thumb));
            template.VisualTree = hit;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style CornerResizeThumbStyle()
        {
            Style style = new Style(typeof(Thumb));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(FrameworkElement.OverridesDefaultStyleProperty, true));

            FrameworkElementFactory root = new FrameworkElementFactory(typeof(Grid));
            root.SetValue(Panel.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory grip = new FrameworkElementFactory(typeof(Border));
            grip.Name = "Grip";
            grip.SetValue(FrameworkElement.WidthProperty, 12.0);
            grip.SetValue(FrameworkElement.HeightProperty, 12.0);
            grip.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            grip.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Bottom);
            grip.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 8));
            grip.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            grip.SetValue(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(72, 255, 255, 255)));
            grip.SetValue(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 255, 255, 255)));
            grip.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            grip.SetValue(UIElement.OpacityProperty, 0.72);
            root.AppendChild(grip);

            ControlTemplate template = new ControlTemplate(typeof(Thumb));
            template.VisualTree = root;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "Grip"));
            hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(108, 255, 255, 255)), "Grip"));
            template.Triggers.Add(hover);

            Trigger dragging = new Trigger();
            dragging.Property = Thumb.IsDraggingProperty;
            dragging.Value = true;
            dragging.Setters.Add(new Setter(UIElement.OpacityProperty, 1.0, "Grip"));
            dragging.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(138, 255, 255, 255)), "Grip"));
            template.Triggers.Add(dragging);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private void ToggleResizeMode()
        {
            isResizeMode = !isResizeMode;
            if (resizeLayer != null)
            {
                resizeLayer.Visibility = isResizeMode ? Visibility.Visible : Visibility.Collapsed;
            }

            if (resizeIcon != null)
            {
                resizeIcon.Opacity = isResizeMode ? 1.0 : 0.78;
            }
        }

        private void BeginInlineEdit()
        {
            if (inlineEditor == null || inlineEditor.Visibility == Visibility.Visible)
            {
                return;
            }

            model.Stop();
            isUpdatingInlineEditor = true;
            inlineEditor.Text = model.Script ?? "";
            inlineEditor.FontFamily = new FontFamily(model.FontFamilyName);
            inlineEditor.FontSize = model.FontSize;
            isUpdatingInlineEditor = false;
            scroller.Visibility = Visibility.Hidden;
            inlineEditor.Visibility = Visibility.Visible;
            inlineEditor.Focus();
            inlineEditor.CaretIndex = inlineEditor.Text.Length;
        }

        private void EndInlineEdit()
        {
            if (inlineEditor == null || inlineEditor.Visibility != Visibility.Visible)
            {
                return;
            }

            model.Script = inlineEditor.Text;
            inlineEditor.Visibility = Visibility.Collapsed;
            scroller.Visibility = Visibility.Visible;
        }

        private void FadeControlsTo(double opacity)
        {
            if (controlsPanel == null)
            {
                return;
            }

            DoubleAnimation animation = new DoubleAnimation();
            animation.To = opacity;
            animation.Duration = TimeSpan.FromMilliseconds(140);
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            controlsPanel.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void OverlayWindowOnSourceInitialized(object sender, EventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            handle = helper.Handle;

            int style = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
            style = style | NativeMethods.WS_EX_TOOLWINDOW;
            NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, style);

            ApplyPrivacyMode();
        }

        private void ModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsOverlayVisible")
            {
                ShowOrHide(model.IsOverlayVisible);
            }
            else if (e.PropertyName == "PrivacyModeEnabled")
            {
                ApplyPrivacyMode();
            }
            else if (e.PropertyName == "OverlayWidth" ||
                     e.PropertyName == "OverlayHeight" ||
                     e.PropertyName == "SelectedScreenDeviceName")
            {
                Reposition();
            }
            else if (e.PropertyName == "IsRunning")
            {
                UpdatePlaybackGlyph();
            }
            else if (e.PropertyName == "FontSize")
            {
                if (inlineEditor != null)
                {
                    inlineEditor.FontSize = model.FontSize;
                }
            }
            else if (e.PropertyName == "FontFamilyName")
            {
                if (inlineEditor != null)
                {
                    inlineEditor.FontFamily = new FontFamily(model.FontFamilyName);
                }
            }
        }

        private string PlaybackGlyph()
        {
            return model.IsRunning ? "\uE769" : "\uE768";
        }

        private void UpdatePlaybackGlyph()
        {
            if (playbackIcon != null)
            {
                playbackIcon.Text = PlaybackGlyph();
            }
        }

        private void SystemEventsOnDisplaySettingsChanged(object sender, EventArgs e)
        {
            Reposition();
        }

        private System.Windows.Forms.Screen ResolveTargetScreen()
        {
            string selected = model.SelectedScreenDeviceName ?? "";
            if (selected.Length > 0)
            {
                System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
                for (int i = 0; i < screens.Length; i++)
                {
                    if (String.Equals(screens[i].DeviceName, selected, StringComparison.OrdinalIgnoreCase))
                    {
                        return screens[i];
                    }
                }
            }

            return System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        }

        private void GetSystemDpiScale(out double scaleX, out double scaleY)
        {
            using (DrawingGraphics graphics = DrawingGraphics.FromHwnd(IntPtr.Zero))
            {
                scaleX = graphics.DpiX / 96.0;
                scaleY = graphics.DpiY / 96.0;
            }

            if (scaleX <= 0)
            {
                scaleX = 1;
            }

            if (scaleY <= 0)
            {
                scaleY = 1;
            }
        }
    }
}
