using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace PromptBar
{
    public sealed class SettingsWindow : Window
    {
        private readonly PrompterModel model;
        private readonly HotkeyProfile hotkeyProfile;
        private readonly Action hotkeysChanged;
        private readonly Dictionary<ShortcutCommand, Button> hotkeyButtons = new Dictionary<ShortcutCommand, Button>();
        private bool updating;
        private bool allowClose;
        private ShortcutCommand? recordingHotkeyCommand;
        private SettingsPage selectedPage = SettingsPage.General;
        private TextBlock hotkeyCaptureHint;

        private ComboBox languageCombo;
        private TextBox scriptBox;
        private TextBlock readDurationText;
        private Slider speedSlider;
        private TextBlock speedValue;
        private ComboBox scrollModeCombo;
        private ComboBox fontFamilyCombo;
        private Slider fontSizeSlider;
        private TextBlock fontSizeValue;
        private Slider widthSlider;
        private TextBlock widthValue;
        private Slider heightSlider;
        private TextBlock heightValue;
        private ComboBox screenCombo;
        private CheckBox showOverlayCheck;
        private CheckBox privacyCheck;

        public SettingsWindow(PrompterModel model, HotkeyProfile hotkeyProfile, Action hotkeysChanged)
        {
            this.model = model;
            this.hotkeyProfile = hotkeyProfile ?? new HotkeyProfile();
            this.hotkeysChanged = hotkeysChanged;

            Title = T("SettingsWindowTitle");
            MinWidth = 960;
            MinHeight = 640;
            Width = 1040;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(31, 33, 42));
            Topmost = true;
            FontFamily = new FontFamily(PrompterModel.DefaultFontFamilyName);
            UseLayoutRounding = false;
            SnapsToDevicePixels = false;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            Content = BuildContent();
            SourceInitialized += SettingsWindowOnSourceInitialized;
            PreviewKeyDown += SettingsWindowOnPreviewKeyDown;
            model.PropertyChanged += ModelOnPropertyChanged;
            RefreshScreens();
            RefreshFromModel();
        }

        public void ShowSettings()
        {
            if (!IsVisible)
            {
                Show();
            }

            WindowState = WindowState.Normal;
            Activate();
            Topmost = false;
            Topmost = true;
        }

        private void SettingsWindowOnSourceInitialized(object sender, EventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            WindowBackdrop.ApplySettingsBackdrop(helper.Handle);
        }

        public void ReallyClose()
        {
            allowClose = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!allowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            model.PropertyChanged -= ModelOnPropertyChanged;
            base.OnClosing(e);
        }

        private UIElement BuildContent()
        {
            ResetPageControlReferences();
            Title = T("SettingsWindowTitle");

            Border shell = new Border();
            CornerRadius shellRadius = new CornerRadius(8);
            shell.CornerRadius = shellRadius;
            shell.Background = SettingsShellBrush();
            shell.BorderBrush = SettingsBorderBrush(28);
            shell.BorderThickness = new Thickness(1);
            shell.SnapsToDevicePixels = false;

            Grid backdrop = new Grid();
            shell.Child = backdrop;

            Grid frame = new Grid();
            frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            backdrop.Children.Add(frame);

            DockPanel titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            frame.Children.Add(titleBar);

            Grid body = new Grid();
            body.Margin = new Thickness(16, 0, 16, 16);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(152) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) });
            Grid.SetRow(body, 1);
            frame.Children.Add(body);

            StackPanel navigation = BuildNavigationRail();
            Grid.SetColumn(navigation, 0);
            body.Children.Add(navigation);

            ScrollViewer scrollViewer = new ScrollViewer();
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.Margin = new Thickness(12, 0, 12, 0);
            scrollViewer.Background = Brushes.Transparent;
            Grid.SetColumn(scrollViewer, 1);
            body.Children.Add(scrollViewer);

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(0);
            scrollViewer.Content = root;

            root.Children.Add(BuildSelectedSection());

            Border aboutPanel = BuildAboutPanel();
            Grid.SetColumn(aboutPanel, 2);
            body.Children.Add(aboutPanel);

            return shell;
        }

        private void ResetPageControlReferences()
        {
            languageCombo = null;
            scriptBox = null;
            readDurationText = null;
            speedSlider = null;
            speedValue = null;
            scrollModeCombo = null;
            fontFamilyCombo = null;
            fontSizeSlider = null;
            fontSizeValue = null;
            widthSlider = null;
            widthValue = null;
            heightSlider = null;
            heightValue = null;
            screenCombo = null;
            showOverlayCheck = null;
            privacyCheck = null;
            hotkeyCaptureHint = null;
            hotkeyButtons.Clear();
        }

        private UIElement BuildSelectedSection()
        {
            if (selectedPage == SettingsPage.Script)
            {
                return BuildScriptSection();
            }

            if (selectedPage == SettingsPage.Playback)
            {
                return BuildPlaybackSection();
            }

            if (selectedPage == SettingsPage.Appearance)
            {
                return BuildAppearanceSection();
            }

            if (selectedPage == SettingsPage.Display)
            {
                return BuildDisplaySection();
            }

            if (selectedPage == SettingsPage.Privacy)
            {
                return BuildPrivacySection();
            }

            if (selectedPage == SettingsPage.Shortcuts)
            {
                return BuildShortcutsSection();
            }

            return BuildGeneralSection();
        }

        private void SelectPage(SettingsPage page)
        {
            if (selectedPage == page)
            {
                return;
            }

            selectedPage = page;
            recordingHotkeyCommand = null;
            Content = BuildContent();
            WindowInteropHelper helper = new WindowInteropHelper(this);
            WindowBackdrop.ApplySettingsBackdrop(helper.Handle);
            RefreshScreens();
            RefreshFromModel();
        }

        private DockPanel BuildTitleBar()
        {
            DockPanel titleBar = new DockPanel();
            titleBar.Margin = new Thickness(18, 0, 14, 0);
            titleBar.LastChildFill = true;
            titleBar.MouseLeftButtonDown += delegate
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
            };

            StackPanel controls = new StackPanel();
            controls.Orientation = Orientation.Horizontal;
            controls.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(controls, Dock.Right);
            titleBar.Children.Add(controls);

            Button minimize = TitleBarButton("_");
            minimize.Click += delegate { WindowState = WindowState.Minimized; };
            controls.Children.Add(minimize);

            Button close = TitleBarButton("X");
            close.Click += delegate { Close(); };
            controls.Children.Add(close);

            StackPanel titleStack = new StackPanel();
            titleStack.Orientation = Orientation.Vertical;
            titleStack.VerticalAlignment = VerticalAlignment.Center;
            titleBar.Children.Add(titleStack);

            TextBlock title = new TextBlock();
            title.Text = T("SettingsHeader");
            title.FontSize = 19;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = SettingsTextBrush();
            title.Margin = new Thickness(0, 0, 0, 1);
            titleStack.Children.Add(title);

            TextBlock subtitle = new TextBlock();
            subtitle.Text = "PromptBar";
            subtitle.FontSize = 11;
            subtitle.Foreground = SettingsMutedTextBrush();
            titleStack.Children.Add(subtitle);

            return titleBar;
        }

        private StackPanel BuildNavigationRail()
        {
            StackPanel rail = new StackPanel();
            rail.Margin = new Thickness(0, 0, 0, 0);

            rail.Children.Add(NavigationItem(SettingsPage.General, "\uE713", T("GeneralSection")));
            rail.Children.Add(NavigationItem(SettingsPage.Script, "\uE8A5", T("ScriptSection")));
            rail.Children.Add(NavigationItem(SettingsPage.Playback, "\uE768", T("PlaybackSection")));
            rail.Children.Add(NavigationItem(SettingsPage.Appearance, "\uE771", T("AppearanceSection")));
            rail.Children.Add(NavigationItem(SettingsPage.Display, "\uE7F4", T("DisplaySection")));
            rail.Children.Add(NavigationItem(SettingsPage.Privacy, "\uE72E", T("PrivacySection")));
            rail.Children.Add(NavigationItem(SettingsPage.Shortcuts, "\uE765", T("ShortcutsSection")));

            return rail;
        }

        private Button NavigationItem(SettingsPage page, string glyph, string label)
        {
            bool selected = selectedPage == page;
            DockPanel content = new DockPanel();
            content.LastChildFill = true;

            TextBlock icon = new TextBlock();
            icon.Text = glyph;
            icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            icon.FontSize = 12;
            icon.Width = 22;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Foreground = selected ? SettingsTextBrush() : SettingsMutedTextBrush();
            DockPanel.SetDock(icon, Dock.Left);
            content.Children.Add(icon);

            TextBlock text = new TextBlock();
            text.Text = label;
            text.FontSize = 12;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Foreground = selected ? SettingsTextBrush() : SettingsMutedTextBrush();
            content.Children.Add(text);

            Button item = new Button();
            item.Content = content;
            item.Height = 34;
            item.Margin = new Thickness(0, 0, 0, 5);
            item.Padding = new Thickness(10, 0, 10, 0);
            item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            item.VerticalContentAlignment = VerticalAlignment.Center;
            item.Background = selected
                ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                : Brushes.Transparent;
            item.BorderBrush = selected ? SettingsBorderBrush(20) : Brushes.Transparent;
            item.BorderThickness = new Thickness(1);
            item.Foreground = selected ? SettingsTextBrush() : SettingsMutedTextBrush();
            item.Cursor = Cursors.Hand;
            item.FocusVisualStyle = null;
            item.Style = SettingsNavigationButtonStyle();
            item.Click += delegate { SelectPage(page); };

            return item;
        }

        private Border BuildAboutPanel()
        {
            Border panel = new Border();
            panel.Background = SettingsSidePanelBrush();
            panel.BorderBrush = SettingsBorderBrush(22);
            panel.BorderThickness = new Thickness(1);
            panel.CornerRadius = new CornerRadius(8);
            panel.Padding = new Thickness(14);
            panel.SnapsToDevicePixels = false;

            StackPanel stack = new StackPanel();
            panel.Child = stack;

            TextBlock title = new TextBlock();
            title.Text = "PromptBar";
            title.FontSize = 18;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = SettingsTextBrush();
            title.Margin = new Thickness(0, 0, 0, 4);
            stack.Children.Add(title);

            TextBlock version = new TextBlock();
            version.Text = "Version " + AppVersionText();
            version.FontSize = 12;
            version.Foreground = SettingsMutedTextBrush();
            version.Margin = new Thickness(0, 0, 0, 18);
            stack.Children.Add(version);

            stack.Children.Add(AboutLine("Design", "Windows Fluent surfaces"));
            stack.Children.Add(AboutLine("Build", "Portable single-file exe"));
            stack.Children.Add(AboutLine("Privacy", "Best-effort capture exclusion"));
            stack.Children.Add(AboutLine("GitHub", "NEONARCHY/PromptBar"));

            return panel;
        }

        private UIElement AboutLine(string label, string value)
        {
            StackPanel block = new StackPanel();
            block.Margin = new Thickness(0, 0, 0, 13);

            TextBlock labelBlock = new TextBlock();
            labelBlock.Text = label;
            labelBlock.FontSize = 11;
            labelBlock.Foreground = SettingsMutedTextBrush();
            labelBlock.Margin = new Thickness(0, 0, 0, 3);
            block.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock();
            valueBlock.Text = value;
            valueBlock.FontSize = 12;
            valueBlock.TextWrapping = TextWrapping.Wrap;
            valueBlock.Foreground = SettingsTextBrush();
            block.Children.Add(valueBlock);

            return block;
        }

        private string AppVersionText()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
            {
                return "";
            }

            return version.Major.ToString(CultureInfo.InvariantCulture) + "." +
                version.Minor.ToString(CultureInfo.InvariantCulture) + "." +
                version.Build.ToString(CultureInfo.InvariantCulture);
        }

        private Button TitleBarButton(string label)
        {
            Button button = new Button();
            button.Content = label;
            button.Width = 28;
            button.Height = 28;
            button.Margin = new Thickness(4, 0, 0, 0);
            button.Padding = new Thickness(0);
            button.Foreground = SettingsTextBrush();
            button.Background = SettingsButtonBrush();
            button.BorderBrush = SettingsBorderBrush(24);
            button.Cursor = Cursors.Hand;
            button.FocusVisualStyle = null;
            button.Style = SettingsRoundButtonStyle();
            return button;
        }

        private void StyleSettingsButton(Button button)
        {
            button.MinHeight = 30;
            button.Padding = new Thickness(12, 5, 12, 6);
            button.Foreground = SettingsTextBrush();
            button.Background = SettingsButtonBrush();
            button.BorderBrush = SettingsBorderBrush(28);
            button.FocusVisualStyle = null;
            button.Cursor = Cursors.Hand;
            button.Style = SettingsButtonStyle();
        }

        private void StyleSettingsTextBox(TextBox textBox)
        {
            textBox.Padding = new Thickness(12, 10, 12, 10);
            textBox.Foreground = SettingsTextBrush();
            textBox.Background = SettingsInputBrush();
            textBox.BorderBrush = SettingsBorderBrush(30);
            textBox.BorderThickness = new Thickness(1);
            textBox.CaretBrush = Brushes.White;
            textBox.SelectionBrush = new SolidColorBrush(Color.FromArgb(120, 76, 194, 255));
            textBox.FocusVisualStyle = null;
            textBox.Style = SettingsTextBoxStyle();
        }

        private void StyleSettingsComboBox(ComboBox comboBox)
        {
            comboBox.MinHeight = 30;
            comboBox.Padding = new Thickness(8, 3, 8, 3);
            comboBox.Foreground = SettingsTextBrush();
            comboBox.Background = SettingsInputBrush();
            comboBox.BorderBrush = SettingsBorderBrush(30);
            comboBox.FocusVisualStyle = null;
            comboBox.Resources[SystemColors.WindowBrushKey] = SettingsSectionBrush();
            comboBox.Resources[SystemColors.ControlBrushKey] = SettingsInputBrush();
            comboBox.Resources[SystemColors.HighlightBrushKey] = SettingsAccentBrush();
            comboBox.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
            comboBox.Style = SettingsComboBoxStyle();
            comboBox.ItemContainerStyle = SettingsComboBoxItemStyle();
        }

        private void StyleSettingsCheckBox(CheckBox checkBox)
        {
            checkBox.Foreground = SettingsTextBrush();
            checkBox.FocusVisualStyle = null;
            checkBox.Cursor = Cursors.Hand;
            checkBox.Style = SettingsToggleStyle();
        }

        private Brush SettingsShellBrush()
        {
            return WindowBackdrop.UseWindows11VisualStyle
                ? new SolidColorBrush(Color.FromRgb(31, 33, 42))
                : new SolidColorBrush(Color.FromRgb(27, 29, 36));
        }

        private Brush SettingsAeroWashBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(46, 177, 237, 197), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 85, 147, 255), 0.42));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.72));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            return brush;
        }

        private Brush SettingsTopSheenBrush()
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(0, 1);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(54, 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.3));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.74));
            return brush;
        }

        private Brush SettingsSectionBrush()
        {
            return WindowBackdrop.UseWindows11VisualStyle
                ? new SolidColorBrush(Color.FromRgb(42, 44, 54))
                : new SolidColorBrush(Color.FromRgb(38, 40, 49));
        }

        private Brush SettingsSidePanelBrush()
        {
            return WindowBackdrop.UseWindows11VisualStyle
                ? new SolidColorBrush(Color.FromRgb(35, 37, 46))
                : new SolidColorBrush(Color.FromRgb(31, 33, 41));
        }

        private Brush SettingsInputBrush()
        {
            return new SolidColorBrush(Color.FromRgb(30, 32, 40));
        }

        private Brush SettingsButtonBrush()
        {
            return new SolidColorBrush(Color.FromRgb(57, 59, 70));
        }

        private SolidColorBrush SettingsTextBrush()
        {
            return new SolidColorBrush(Color.FromArgb(232, 244, 247, 252));
        }

        private SolidColorBrush SettingsMutedTextBrush()
        {
            return new SolidColorBrush(Color.FromArgb(176, 210, 213, 222));
        }

        private SolidColorBrush SettingsBorderBrush(byte alpha)
        {
            return new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255));
        }

        private SolidColorBrush SettingsAccentBrush()
        {
            return new SolidColorBrush(Color.FromArgb(230, 76, 194, 255));
        }

        private Style GlassSectionStyle()
        {
            Style style = new Style(typeof(GroupBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SettingsSectionBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(22)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "SectionChrome";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory stack = new FrameworkElementFactory(typeof(StackPanel));
            stack.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            border.AppendChild(stack);

            FrameworkElementFactory header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(HeaderedContentControl.HeaderProperty));
            header.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 12, 14, 6));
            stack.AppendChild(header);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(14, 2, 14, 14));
            stack.AppendChild(content);

            ControlTemplate template = new ControlTemplate(typeof(GroupBox));
            template.VisualTree = border;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsButtonStyle()
        {
            return SettingsButtonStyleFor(typeof(Button), new CornerRadius(8));
        }

        private Style SettingsRoundButtonStyle()
        {
            return SettingsButtonStyleFor(typeof(Button), new CornerRadius(14));
        }

        private Style SettingsNavigationButtonStyle()
        {
            Style style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsMutedTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(7));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            chrome.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = chrome;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), "Chrome"));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(16), "Chrome"));
            template.Triggers.Add(hover);

            Trigger pressed = new Trigger();
            pressed.Property = ButtonBase.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)), "Chrome"));
            pressed.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(22), "Chrome"));
            template.Triggers.Add(pressed);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsButtonStyleFor(Type targetType, CornerRadius radius)
        {
            Style style = new Style(targetType);
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SettingsButtonBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(28)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(Border.CornerRadiusProperty, radius);
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            chrome.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(targetType);
            template.VisualTree = chrome;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), "Chrome"));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(44), "Chrome"));
            template.Triggers.Add(hover);

            Trigger pressed = new Trigger();
            pressed.Property = ButtonBase.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(34, 76, 194, 255)), "Chrome"));
            pressed.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(58), "Chrome"));
            template.Triggers.Add(pressed);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsTextBoxStyle()
        {
            Style style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SettingsInputBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(30)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "TextBoxChrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);

            FrameworkElementFactory host = new FrameworkElementFactory(typeof(ScrollViewer));
            host.Name = "PART_ContentHost";
            host.SetValue(UIElement.SnapsToDevicePixelsProperty, false);
            chrome.AppendChild(host);

            ControlTemplate template = new ControlTemplate(typeof(TextBox));
            template.VisualTree = chrome;

            Trigger focused = new Trigger();
            focused.Property = UIElement.IsKeyboardFocusWithinProperty;
            focused.Value = true;
            focused.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(170, 76, 194, 255)), "TextBoxChrome"));
            template.Triggers.Add(focused);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsComboBoxStyle()
        {
            Style style = new Style(typeof(ComboBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, SettingsInputBrush()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(30)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(ComboBox.MaxDropDownHeightProperty, 260.0));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "ComboChrome";
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, false);
            grid.AppendChild(chrome);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.Name = "ContentSite";
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            content.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            content.SetValue(ContentPresenter.ContentStringFormatProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemStringFormatProperty));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 0, 32, 0));
            content.SetValue(UIElement.IsHitTestVisibleProperty, false);
            grid.AppendChild(content);

            FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(ToggleButton));
            arrow.Name = "DropDownToggle";
            arrow.SetValue(Control.FocusVisualStyleProperty, null);
            arrow.SetValue(Control.BorderThicknessProperty, new Thickness(0));
            arrow.SetValue(Control.BackgroundProperty, Brushes.Transparent);
            arrow.SetValue(Control.ForegroundProperty, SettingsMutedTextBrush());
            arrow.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
            arrow.SetValue(UIElement.FocusableProperty, false);
            arrow.SetValue(FrameworkElement.WidthProperty, 30.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                Mode = BindingMode.TwoWay
            });
            arrow.SetValue(Control.TemplateProperty, ComboArrowTemplate());
            grid.AppendChild(arrow);

            FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(UIElement.FocusableProperty, false);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
                Mode = BindingMode.TwoWay
            });
            popup.SetBinding(Popup.PlacementTargetProperty, new Binding
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            FrameworkElementFactory dropBorder = new FrameworkElementFactory(typeof(Border));
            dropBorder.Name = "DropDownBorder";
            dropBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            dropBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(250, 38, 40, 50)));
            dropBorder.SetValue(Border.BorderBrushProperty, SettingsBorderBrush(40));
            dropBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            dropBorder.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            FrameworkElementFactory scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

            FrameworkElementFactory items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(items);
            dropBorder.AppendChild(scroll);
            popup.AppendChild(dropBorder);
            grid.AppendChild(popup);

            ControlTemplate template = new ControlTemplate(typeof(ComboBox));
            template.VisualTree = grid;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, SettingsBorderBrush(46), "ComboChrome"));
            template.Triggers.Add(hover);

            Trigger focused = new Trigger();
            focused.Property = UIElement.IsKeyboardFocusWithinProperty;
            focused.Value = true;
            focused.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(170, 76, 194, 255)), "ComboChrome"));
            template.Triggers.Add(focused);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private ControlTemplate ComboArrowTemplate()
        {
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "v");
            text.SetValue(TextBlock.FontSizeProperty, 10.0);
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            text.SetValue(TextBlock.ForegroundProperty, SettingsMutedTextBrush());
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(text);

            ControlTemplate template = new ControlTemplate(typeof(ToggleButton));
            template.VisualTree = border;
            return template;
        }

        private Style SettingsComboBoxItemStyle()
        {
            Style style = new Style(typeof(ComboBoxItem));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "ItemChrome";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            ControlTemplate template = new ControlTemplate(typeof(ComboBoxItem));
            template.VisualTree = border;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), "ItemChrome"));
            template.Triggers.Add(hover);

            Trigger selected = new Trigger();
            selected.Property = ComboBoxItem.IsSelectedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(58, 76, 194, 255)), "ItemChrome"));
            template.Triggers.Add(selected);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsToggleStyle()
        {
            Style style = new Style(typeof(CheckBox));
            style.Setters.Add(new Setter(Control.ForegroundProperty, SettingsTextBrush()));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28.0));

            FrameworkElementFactory root = new FrameworkElementFactory(typeof(DockPanel));
            root.SetValue(DockPanel.LastChildFillProperty, true);

            FrameworkElementFactory switchBorder = new FrameworkElementFactory(typeof(Border));
            switchBorder.Name = "Switch";
            switchBorder.SetValue(DockPanel.DockProperty, Dock.Right);
            switchBorder.SetValue(FrameworkElement.WidthProperty, 42.0);
            switchBorder.SetValue(FrameworkElement.HeightProperty, 22.0);
            switchBorder.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            switchBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            switchBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(68, 255, 255, 255)));
            switchBorder.SetValue(Border.BorderBrushProperty, SettingsBorderBrush(72));
            switchBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));

            FrameworkElementFactory switchGrid = new FrameworkElementFactory(typeof(Grid));
            FrameworkElementFactory dot = new FrameworkElementFactory(typeof(Border));
            dot.Name = "Dot";
            dot.SetValue(FrameworkElement.WidthProperty, 16.0);
            dot.SetValue(FrameworkElement.HeightProperty, 16.0);
            dot.SetValue(FrameworkElement.MarginProperty, new Thickness(3));
            dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            dot.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(226, 255, 255, 255)));
            dot.SetValue(Border.BorderBrushProperty, SettingsBorderBrush(64));
            dot.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            switchGrid.AppendChild(dot);
            switchBorder.AppendChild(switchGrid);
            root.AppendChild(switchBorder);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
            root.AppendChild(content);

            ControlTemplate template = new ControlTemplate(typeof(CheckBox));
            template.VisualTree = root;

            Trigger hover = new Trigger();
            hover.Property = UIElement.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, SettingsBorderBrush(58), "Switch"));
            template.Triggers.Add(hover);

            Trigger checkedTrigger = new Trigger();
            checkedTrigger.Property = ToggleButton.IsCheckedProperty;
            checkedTrigger.Value = true;
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(210, 76, 194, 255)), "Switch"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(112, 255, 255, 255)), "Switch"));
            checkedTrigger.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "Dot"));
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White, "Dot"));
            template.Triggers.Add(checkedTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Style SettingsSliderStyle()
        {
            string xaml =
@"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        TargetType=""{x:Type Slider}"">
    <Setter Property=""Height"" Value=""24"" />
    <Setter Property=""FocusVisualStyle"" Value=""{x:Null}"" />
    <Setter Property=""Template"">
        <Setter.Value>
            <ControlTemplate TargetType=""{x:Type Slider}"">
                <Grid Height=""24"" SnapsToDevicePixels=""False"">
                    <Track x:Name=""PART_Track"" VerticalAlignment=""Center"">
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command=""{x:Static Slider.DecreaseLarge}"" Focusable=""False"">
                                <RepeatButton.Template>
                                    <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                        <Border Height=""4"" CornerRadius=""2"" Background=""#D04CC2FF"" />
                                    </ControlTemplate>
                                </RepeatButton.Template>
                            </RepeatButton>
                        </Track.DecreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb Width=""15"" Height=""15"" Focusable=""False"">
                                <Thumb.Template>
                                    <ControlTemplate TargetType=""{x:Type Thumb}"">
                                        <Border CornerRadius=""8"" Background=""#FFF7F9FC"" BorderBrush=""#80FFFFFF"" BorderThickness=""1"" />
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command=""{x:Static Slider.IncreaseLarge}"" Focusable=""False"">
                                <RepeatButton.Template>
                                    <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                        <Border Height=""4"" CornerRadius=""2"" Background=""#78FFFFFF"" />
                                    </ControlTemplate>
                                </RepeatButton.Template>
                            </RepeatButton>
                        </Track.IncreaseRepeatButton>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
            return (Style)XamlReader.Parse(xaml);
        }

        private GroupBox BuildGeneralSection()
        {
            GroupBox group = Section(T("GeneralSection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            languageCombo = new ComboBox();
            StyleSettingsComboBox(languageCombo);
            IList<LanguageOption> languages = Localization.SupportedLanguages;
            for (int i = 0; i < languages.Count; i++)
            {
                languageCombo.Items.Add(new Choice(languages[i].Name, languages[i].Code));
            }

            languageCombo.SelectionChanged += delegate
            {
                if (!updating && languageCombo.SelectedItem is Choice)
                {
                    model.LanguageCode = ((Choice)languageCombo.SelectedItem).Value.ToString();
                }
            };
            panel.Children.Add(ControlRow(T("LanguageLabel"), languageCombo));

            return group;
        }

        private GroupBox BuildScriptSection()
        {
            GroupBox group = Section(T("ScriptSection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            scriptBox = new TextBox();
            scriptBox.AcceptsReturn = true;
            scriptBox.AcceptsTab = true;
            scriptBox.TextWrapping = TextWrapping.Wrap;
            scriptBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scriptBox.Height = 220;
            scriptBox.FontFamily = new FontFamily("Consolas");
            scriptBox.FontSize = 14;
            StyleSettingsTextBox(scriptBox);
            scriptBox.PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                HandleTextEditorShortcut(scriptBox, e);
            };
            scriptBox.TextChanged += delegate
            {
                if (!updating)
                {
                    model.Script = scriptBox.Text;
                    UpdateReadDuration();
                }
            };
            panel.Children.Add(scriptBox);

            DockPanel actions = new DockPanel();
            actions.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(actions);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            DockPanel.SetDock(buttons, Dock.Left);
            actions.Children.Add(buttons);

            Button import = new Button();
            import.Content = T("Import");
            import.Margin = new Thickness(0, 0, 8, 0);
            StyleSettingsButton(import);
            import.Click += delegate { ImportScript(); };
            buttons.Children.Add(import);

            Button export = new Button();
            export.Content = T("Export");
            export.Margin = new Thickness(0, 0, 8, 0);
            StyleSettingsButton(export);
            export.Click += delegate { ExportScript(); };
            buttons.Children.Add(export);

            Button copy = new Button();
            copy.Content = T("Copy");
            copy.Margin = new Thickness(0, 0, 8, 0);
            StyleSettingsButton(copy);
            copy.Click += delegate { CopyScript(); };
            buttons.Children.Add(copy);

            Button paste = new Button();
            paste.Content = T("Paste");
            StyleSettingsButton(paste);
            paste.Click += delegate { PasteScript(); };
            buttons.Children.Add(paste);

            readDurationText = new TextBlock();
            readDurationText.HorizontalAlignment = HorizontalAlignment.Right;
            readDurationText.VerticalAlignment = VerticalAlignment.Center;
            readDurationText.Foreground = SettingsMutedTextBrush();
            actions.Children.Add(readDurationText);

            return group;
        }

        private GroupBox BuildPlaybackSection()
        {
            GroupBox group = Section(T("PlaybackSection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            speedSlider = Slider(10, 300, 5);
            speedValue = ValueText();
            panel.Children.Add(SliderRow(T("SpeedLabel"), speedSlider, speedValue));
            speedSlider.ValueChanged += delegate
            {
                if (!updating)
                {
                    model.SpeedPointsPerSecond = speedSlider.Value;
                }
            };

            scrollModeCombo = new ComboBox();
            StyleSettingsComboBox(scrollModeCombo);
            scrollModeCombo.Items.Add(new Choice(T("ScrollInfinite"), ScrollMode.Infinite));
            scrollModeCombo.Items.Add(new Choice(T("ScrollStopAtEnd"), ScrollMode.StopAtEnd));
            scrollModeCombo.SelectionChanged += delegate
            {
                if (!updating && scrollModeCombo.SelectedItem is Choice)
                {
                    model.ScrollMode = (ScrollMode)((Choice)scrollModeCombo.SelectedItem).Value;
                }
            };
            panel.Children.Add(ControlRow(T("ScrollModeLabel"), scrollModeCombo));

            return group;
        }

        private GroupBox BuildAppearanceSection()
        {
            GroupBox group = Section(T("AppearanceSection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            fontFamilyCombo = new ComboBox();
            StyleSettingsComboBox(fontFamilyCombo);
            PopulateFontFamilyCombo();
            fontFamilyCombo.SelectionChanged += delegate
            {
                if (!updating && fontFamilyCombo.SelectedItem is Choice)
                {
                    model.FontFamilyName = ((Choice)fontFamilyCombo.SelectedItem).Value.ToString();
                }
            };
            panel.Children.Add(ControlRow(T("FontFamilyLabel"), fontFamilyCombo));

            fontSizeSlider = Slider(12, 40, 1);
            fontSizeValue = ValueText();
            panel.Children.Add(SliderRow(T("FontSizeLabel"), fontSizeSlider, fontSizeValue));
            fontSizeSlider.ValueChanged += delegate
            {
                if (!updating)
                {
                    model.FontSize = fontSizeSlider.Value;
                }
            };

            widthSlider = Slider(400, 1200, 10);
            widthValue = ValueText();
            panel.Children.Add(SliderRow(T("OverlayWidthLabel"), widthSlider, widthValue));
            widthSlider.ValueChanged += delegate
            {
                if (!updating)
                {
                    model.OverlayWidth = widthSlider.Value;
                }
            };

            heightSlider = Slider(120, 300, 2);
            heightValue = ValueText();
            panel.Children.Add(SliderRow(T("OverlayHeightLabel"), heightSlider, heightValue));
            heightSlider.ValueChanged += delegate
            {
                if (!updating)
                {
                    model.OverlayHeight = heightSlider.Value;
                }
            };

            return group;
        }

        private GroupBox BuildDisplaySection()
        {
            GroupBox group = Section(T("DisplaySection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            screenCombo = new ComboBox();
            StyleSettingsComboBox(screenCombo);
            screenCombo.SelectionChanged += delegate
            {
                if (!updating && screenCombo.SelectedItem is ScreenChoice)
                {
                    model.SelectedScreenDeviceName = ((ScreenChoice)screenCombo.SelectedItem).DeviceName;
                }
            };
            panel.Children.Add(ControlRow(T("ShowOverlayOnLabel"), screenCombo));

            return group;
        }

        private GroupBox BuildPrivacySection()
        {
            GroupBox group = Section(T("PrivacySection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            showOverlayCheck = new CheckBox();
            showOverlayCheck.Content = T("ShowOverlay");
            showOverlayCheck.Margin = new Thickness(0, 0, 0, 6);
            StyleSettingsCheckBox(showOverlayCheck);
            showOverlayCheck.Checked += delegate { if (!updating) model.IsOverlayVisible = true; };
            showOverlayCheck.Unchecked += delegate { if (!updating) model.IsOverlayVisible = false; };
            panel.Children.Add(showOverlayCheck);

            privacyCheck = new CheckBox();
            privacyCheck.Content = T("LimitScreenSharingCapture");
            privacyCheck.Margin = new Thickness(0, 0, 0, 6);
            StyleSettingsCheckBox(privacyCheck);
            privacyCheck.Checked += delegate { if (!updating) model.PrivacyModeEnabled = true; };
            privacyCheck.Unchecked += delegate { if (!updating) model.PrivacyModeEnabled = false; };
            panel.Children.Add(privacyCheck);

            TextBlock note = new TextBlock();
            note.Text = T("PrivacyNote");
            note.Foreground = SettingsMutedTextBrush();
            note.FontSize = 12;
            panel.Children.Add(note);

            return group;
        }

        private GroupBox BuildShortcutsSection()
        {
            GroupBox group = Section(T("ShortcutsSection"));
            StackPanel panel = SectionPanel();
            group.Content = panel;

            hotkeyButtons.Clear();

            panel.Children.Add(EditableShortcutRow(ShortcutCommand.StartPause));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.Reset));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.JumpBack));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.TogglePrivacy));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.ToggleOverlay));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.SpeedUp));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.SpeedDown));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.FontSizeUp));
            panel.Children.Add(EditableShortcutRow(ShortcutCommand.FontSizeDown));

            DockPanel actions = new DockPanel();
            actions.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(actions);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            DockPanel.SetDock(buttons, Dock.Left);
            actions.Children.Add(buttons);

            Button help = new Button();
            help.Content = T("HotkeyHelp");
            help.Margin = new Thickness(0, 0, 8, 0);
            StyleSettingsButton(help);
            help.Click += delegate { ShowHotkeyHelp(); };
            buttons.Children.Add(help);

            Button reset = new Button();
            reset.Content = T("ResetDefaults");
            StyleSettingsButton(reset);
            reset.Click += delegate { ResetHotkeyDefaults(); };
            buttons.Children.Add(reset);

            hotkeyCaptureHint = new TextBlock();
            hotkeyCaptureHint.VerticalAlignment = VerticalAlignment.Center;
            hotkeyCaptureHint.Foreground = SettingsMutedTextBrush();
            actions.Children.Add(hotkeyCaptureHint);

            return group;
        }

        private GroupBox Section(string title)
        {
            GroupBox group = new GroupBox();
            TextBlock header = new TextBlock();
            header.Text = title;
            header.Foreground = SettingsTextBrush();
            header.FontWeight = FontWeights.SemiBold;
            header.FontSize = 13;
            group.Header = header;
            group.Margin = new Thickness(0, 0, 0, 12);
            group.Padding = new Thickness(0);
            group.Foreground = SettingsTextBrush();
            group.Background = SettingsSectionBrush();
            group.BorderBrush = SettingsBorderBrush(58);
            group.Style = GlassSectionStyle();
            return group;
        }

        private string T(string key)
        {
            return Localization.Text(model.LanguageCode, key);
        }

        private void RebuildForLanguage()
        {
            recordingHotkeyCommand = null;
            Content = BuildContent();
            WindowInteropHelper helper = new WindowInteropHelper(this);
            WindowBackdrop.ApplySettingsBackdrop(helper.Handle);
            RefreshScreens();
            RefreshFromModel();
        }

        private StackPanel SectionPanel()
        {
            StackPanel panel = new StackPanel();
            panel.Orientation = Orientation.Vertical;
            return panel;
        }

        private Slider Slider(double min, double max, double tick)
        {
            Slider slider = new Slider();
            slider.Minimum = min;
            slider.Maximum = max;
            slider.TickFrequency = tick;
            slider.IsSnapToTickEnabled = true;
            slider.VerticalAlignment = VerticalAlignment.Center;
            slider.Foreground = SettingsAccentBrush();
            slider.Style = SettingsSliderStyle();
            return slider;
        }

        private TextBlock ValueText()
        {
            TextBlock text = new TextBlock();
            text.Width = 58;
            text.TextAlignment = TextAlignment.Right;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Foreground = SettingsMutedTextBrush();
            return text;
        }

        private UIElement SliderRow(string label, Slider slider, TextBlock value)
        {
            Grid grid = RowGrid();

            TextBlock labelBlock = Label(label);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);

            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            return grid;
        }

        private UIElement ControlRow(string label, Control control)
        {
            Grid grid = RowGrid();
            grid.ColumnDefinitions.RemoveAt(2);

            TextBlock labelBlock = Label(label);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            control.MinWidth = 220;
            control.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);

            return grid;
        }

        private Grid RowGrid()
        {
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 0, 0, 10);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            return grid;
        }

        private TextBlock Label(string label)
        {
            TextBlock text = new TextBlock();
            text.Text = label;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Foreground = SettingsTextBrush();
            return text;
        }

        private UIElement EditableShortcutRow(ShortcutCommand command)
        {
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 0, 0, 5);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button hotkeyButton = new Button();
            hotkeyButton.Content = hotkeyProfile.Get(command).DisplayTextFor(model.LanguageCode);
            hotkeyButton.FontFamily = new FontFamily("Consolas");
            hotkeyButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            hotkeyButton.Tag = command;
            StyleSettingsButton(hotkeyButton);
            hotkeyButton.Click += delegate { BeginHotkeyCapture(command); };
            hotkeyButtons[command] = hotkeyButton;
            Grid.SetColumn(hotkeyButton, 0);
            grid.Children.Add(hotkeyButton);

            TextBlock actionText = new TextBlock();
            actionText.Text = Localization.CommandText(model.LanguageCode, command);
            actionText.Margin = new Thickness(12, 0, 0, 0);
            actionText.VerticalAlignment = VerticalAlignment.Center;
            actionText.Foreground = SettingsTextBrush();
            Grid.SetColumn(actionText, 1);
            grid.Children.Add(actionText);

            return grid;
        }

        private void RefreshScreens()
        {
            if (screenCombo == null)
            {
                return;
            }

            screenCombo.Items.Clear();
            screenCombo.Items.Add(new ScreenChoice(T("ScreenAutoPrimary"), ""));

            System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                string name = screens[i].DeviceName;
                if (screens[i].Primary)
                {
                    name += " (" + T("ScreenPrimarySuffix") + ")";
                }

                screenCombo.Items.Add(new ScreenChoice(name, screens[i].DeviceName));
            }
        }

        private void RefreshFromModel()
        {
            updating = true;

            SelectLanguageChoice(model.LanguageCode);
            if (scriptBox != null && scriptBox.Text != model.Script)
            {
                scriptBox.Text = model.Script;
            }
            if (speedSlider != null)
            {
                speedSlider.Value = model.SpeedPointsPerSecond;
            }
            if (speedValue != null)
            {
                speedValue.Text = ((int)model.SpeedPointsPerSecond).ToString(CultureInfo.InvariantCulture);
            }
            SelectChoice(scrollModeCombo, model.ScrollMode);
            SelectFontChoice(model.FontFamilyName);
            if (fontSizeSlider != null)
            {
                fontSizeSlider.Value = model.FontSize;
            }
            if (fontSizeValue != null)
            {
                fontSizeValue.Text = ((int)model.FontSize).ToString(CultureInfo.InvariantCulture);
            }
            if (widthSlider != null)
            {
                widthSlider.Value = model.OverlayWidth;
            }
            if (widthValue != null)
            {
                widthValue.Text = ((int)model.OverlayWidth).ToString(CultureInfo.InvariantCulture);
            }
            if (heightSlider != null)
            {
                heightSlider.Value = model.OverlayHeight;
            }
            if (heightValue != null)
            {
                heightValue.Text = ((int)model.OverlayHeight).ToString(CultureInfo.InvariantCulture);
            }
            if (showOverlayCheck != null)
            {
                showOverlayCheck.IsChecked = model.IsOverlayVisible;
            }
            if (privacyCheck != null)
            {
                privacyCheck.IsChecked = model.PrivacyModeEnabled;
            }
            SelectScreenChoice(model.SelectedScreenDeviceName);
            UpdateReadDuration();
            RefreshHotkeyButtons();

            updating = false;
        }

        private void SelectLanguageChoice(string languageCode)
        {
            if (languageCombo == null)
            {
                return;
            }

            string normalized = Localization.NormalizeLanguageCode(languageCode);
            for (int i = 0; i < languageCombo.Items.Count; i++)
            {
                Choice choice = languageCombo.Items[i] as Choice;
                if (choice != null && String.Equals(choice.Value as string, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    languageCombo.SelectedIndex = i;
                    return;
                }
            }

            if (languageCombo.Items.Count > 0)
            {
                languageCombo.SelectedIndex = 0;
            }
        }

        private void SelectScreenChoice(string deviceName)
        {
            if (screenCombo == null)
            {
                return;
            }

            for (int i = 0; i < screenCombo.Items.Count; i++)
            {
                ScreenChoice choice = screenCombo.Items[i] as ScreenChoice;
                if (choice != null && String.Equals(choice.DeviceName, deviceName ?? "", StringComparison.OrdinalIgnoreCase))
                {
                    screenCombo.SelectedIndex = i;
                    return;
                }
            }

            screenCombo.SelectedIndex = 0;
        }

        private void SelectChoice(ComboBox comboBox, object value)
        {
            if (comboBox == null)
            {
                return;
            }

            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                Choice choice = comboBox.Items[i] as Choice;
                if (choice != null && Object.Equals(choice.Value, value))
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void SelectFontChoice(string fontFamilyName)
        {
            if (fontFamilyCombo == null)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(fontFamilyName))
            {
                fontFamilyName = PrompterModel.DefaultFontFamilyName;
            }

            for (int i = 0; i < fontFamilyCombo.Items.Count; i++)
            {
                Choice choice = fontFamilyCombo.Items[i] as Choice;
                if (choice != null && String.Equals(choice.Value as string, fontFamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    fontFamilyCombo.SelectedIndex = i;
                    return;
                }
            }

            Choice custom = new Choice(fontFamilyName, fontFamilyName);
            fontFamilyCombo.Items.Add(custom);
            fontFamilyCombo.SelectedItem = custom;
        }

        private void PopulateFontFamilyCombo()
        {
            fontFamilyCombo.Items.Clear();

            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            AddFontChoice(T("FontAppleDefault"), PrompterModel.DefaultFontFamilyName, seen);
            AddFontChoice(T("FontGilroy"), "Gilroy, Segoe UI Variable Text, Segoe UI, Arial", seen);
            AddFontChoice(T("FontSegoeVariable"), "Segoe UI Variable Text, Segoe UI, Arial", seen);
            AddFontChoice(T("FontSegoe"), "Segoe UI, Arial", seen);
            AddFontChoice(T("FontArial"), "Arial", seen);

            List<string> installed = new List<string>();
            foreach (FontFamily family in Fonts.SystemFontFamilies)
            {
                string source = family.Source;
                if (!String.IsNullOrWhiteSpace(source) && !seen.ContainsKey(source))
                {
                    installed.Add(source);
                    seen[source] = true;
                }
            }

            installed.Sort(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < installed.Count; i++)
            {
                fontFamilyCombo.Items.Add(new Choice(installed[i], installed[i]));
            }
        }

        private void AddFontChoice(string label, string value, Dictionary<string, bool> seen)
        {
            fontFamilyCombo.Items.Add(new Choice(label, value));
            if (!seen.ContainsKey(value))
            {
                seen[value] = true;
            }
        }

        private void UpdateReadDuration()
        {
            if (readDurationText == null)
            {
                return;
            }

            TimeSpan duration = model.EstimatedReadDuration;
            if (duration.TotalSeconds < 60)
            {
                readDurationText.Text = "~" + Math.Round(duration.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s";
            }
            else
            {
                readDurationText.Text = "~" + ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m " + duration.Seconds.ToString("00", CultureInfo.InvariantCulture) + "s";
            }
        }

        private void ModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (updating)
            {
                return;
            }

            if (e.PropertyName == "LanguageCode")
            {
                RebuildForLanguage();
                return;
            }

            RefreshFromModel();
        }

        private void ImportScript()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = T("OpenFileFilter");
            dialog.CheckFileExists = true;

            bool? result = dialog.ShowDialog(this);
            if (result == true)
            {
                string fileName = dialog.FileName;
                model.Stop();
                IsEnabled = false;
                Cursor previousCursor = Cursor;
                Cursor = Cursors.Wait;

                Thread thread = new Thread(delegate()
                {
                    try
                    {
                        string imported = ScriptFileIO.ImportText(fileName);
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            IsEnabled = true;
                            Cursor = previousCursor;
                            model.Script = imported;
                            model.ResetScroll();
                            Activate();
                        }));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            IsEnabled = true;
                            Cursor = previousCursor;
                            Activate();
                            MessageBox.Show(this, ex.Message, T("ImportFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        }));
                    }
                });
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
        }

        private void ExportScript()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = T("SaveFileFilter");
            dialog.FileName = "script.txt";
            dialog.DefaultExt = ".txt";
            dialog.OverwritePrompt = true;

            bool? result = dialog.ShowDialog(this);
            if (result == true)
            {
                try
                {
                    ScriptFileIO.ExportText(dialog.FileName, model.Script);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, T("ExportFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void CopyScript()
        {
            if (scriptBox.SelectionLength > 0)
            {
                scriptBox.Copy();
                return;
            }

            Clipboard.SetText(model.Script ?? "");
        }

        private void PasteScript()
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }

            string text = Clipboard.GetText();
            int start = scriptBox.SelectionStart;
            int length = scriptBox.SelectionLength;
            string current = scriptBox.Text ?? "";
            scriptBox.Text = current.Remove(start, length).Insert(start, text);
            scriptBox.SelectionStart = start + text.Length;
            scriptBox.SelectionLength = 0;
            scriptBox.Focus();
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

        private void BeginHotkeyCapture(ShortcutCommand command)
        {
            recordingHotkeyCommand = command;
            RefreshHotkeyButtons();
            hotkeyCaptureHint.Text = T("HotkeyPressNew");
            Focus();
        }

        private void SettingsWindowOnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (recordingHotkeyCommand == null)
            {
                return;
            }

            e.Handled = true;
            ShortcutCommand command = recordingHotkeyCommand.Value;

            if (e.Key == Key.Escape)
            {
                recordingHotkeyCommand = null;
                hotkeyCaptureHint.Text = "";
                RefreshHotkeyButtons();
                return;
            }

            if (e.Key == Key.Back || e.Key == Key.Delete)
            {
                hotkeyProfile.Set(command, new HotkeyGesture());
                recordingHotkeyCommand = null;
                hotkeyCaptureHint.Text = T("HotkeyCleared");
                RefreshHotkeyButtons();
                NotifyHotkeysChanged();
                return;
            }

            Key inputKey = e.Key == Key.System ? e.SystemKey : e.Key;
            HotkeyGesture gesture = HotkeyGesture.FromInput(inputKey, Keyboard.Modifiers);
            if (gesture == null)
            {
                return;
            }

            if (!gesture.Control && !gesture.Alt && !gesture.Shift && !gesture.Win)
            {
                hotkeyCaptureHint.Text = T("HotkeyUseModifier");
                return;
            }

            ShortcutCommand duplicateCommand;
            if (TryFindDuplicate(command, gesture, out duplicateCommand))
            {
                hotkeyCaptureHint.Text = T("HotkeyAlreadyAssigned");
                return;
            }

            hotkeyProfile.Set(command, gesture);
            recordingHotkeyCommand = null;
            hotkeyCaptureHint.Text = T("HotkeyUpdated");
            RefreshHotkeyButtons();
            NotifyHotkeysChanged();
        }

        private bool TryFindDuplicate(ShortcutCommand command, HotkeyGesture gesture, out ShortcutCommand duplicateCommand)
        {
            foreach (ShortcutCommand candidate in hotkeyProfile.Commands)
            {
                if (candidate == command)
                {
                    continue;
                }

                HotkeyGesture existing = hotkeyProfile.Get(candidate);
                if (existing != null && !existing.IsEmpty && existing.SameAs(gesture))
                {
                    duplicateCommand = candidate;
                    return true;
                }
            }

            duplicateCommand = command;
            return false;
        }

        private void RefreshHotkeyButtons()
        {
            foreach (KeyValuePair<ShortcutCommand, Button> entry in hotkeyButtons)
            {
                if (recordingHotkeyCommand != null && recordingHotkeyCommand.Value == entry.Key)
                {
                    entry.Value.Content = T("HotkeyPressKeys");
                }
                else
                {
                    entry.Value.Content = hotkeyProfile.Get(entry.Key).DisplayTextFor(model.LanguageCode);
                }
            }
        }

        private void ShowHotkeyHelp()
        {
            MessageBox.Show(this, hotkeyProfile.HelpText(model.LanguageCode), T("HotkeyDialogTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetHotkeyDefaults()
        {
            HotkeyProfile defaults = new HotkeyProfile();
            foreach (ShortcutCommand command in defaults.Commands)
            {
                hotkeyProfile.Set(command, defaults.Get(command));
            }

            recordingHotkeyCommand = null;
            hotkeyCaptureHint.Text = T("HotkeyDefaultsRestored");
            RefreshHotkeyButtons();
            NotifyHotkeysChanged();
        }

        private void NotifyHotkeysChanged()
        {
            if (hotkeysChanged != null)
            {
                hotkeysChanged();
            }
        }

        private enum SettingsPage
        {
            General,
            Script,
            Playback,
            Appearance,
            Display,
            Privacy,
            Shortcuts
        }

        private sealed class ScreenChoice
        {
            public ScreenChoice(string label, string deviceName)
            {
                Label = label;
                DeviceName = deviceName;
            }

            public string Label { get; private set; }
            public string DeviceName { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }

        private sealed class Choice
        {
            public Choice(string label, object value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; private set; }
            public object Value { get; private set; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
