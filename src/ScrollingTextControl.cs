using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PromptBar
{
    public sealed class ScrollingTextControl : FrameworkElement
    {
        private const double LoopGap = 24;
        private readonly PrompterModel model;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private double phase;
        private double contentHeight = 1;
        private bool hasMeasuredContentHeight;
        private bool hasReachedEndInStopMode;
        private FormattedText cachedFormattedText;
        private string cachedText;
        private double cachedFontSize;
        private string cachedFontFamilyName;
        private double cachedTextWidth;
        private bool layoutDirty = true;

        public ScrollingTextControl(PrompterModel model)
        {
            this.model = model;
            Focusable = false;
            ClipToBounds = true;
            SnapsToDevicePixels = false;
            UseLayoutRounding = false;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(16);
            timer.Tick += TimerOnTick;
            timer.Start();
            stopwatch.Start();

            model.PropertyChanged += ModelOnPropertyChanged;
            model.ResetRequested += ModelOnResetRequested;
            model.JumpBackRequested += ModelOnJumpBackRequested;
            model.ManualScrollRequested += ModelOnManualScrollRequested;

            ResetPhase();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            Rect bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 1 || bounds.Height <= 1)
            {
                return;
            }

            drawingContext.PushClip(new RectangleGeometry(bounds));

            string text = model.Script ?? "";
            bool hasContent = text.Trim().Length > 0;
            if (!hasContent)
            {
                DrawCenteredMessage(drawingContext, Localization.Text(model.LanguageCode, "NoScriptMessage"));
                drawingContext.Pop();
                return;
            }

            if (!model.HasStartedSession)
            {
                DrawCenteredMessage(drawingContext, Localization.Text(model.LanguageCode, "ReadyMessage"));
                drawingContext.Pop();
                return;
            }

            FormattedText formatted = GetFormattedScriptText(text, Math.Max(1, bounds.Width));

            if (model.ScrollMode == ScrollMode.StopAtEnd)
            {
                drawingContext.DrawText(formatted, new Point(0, -phase));
            }
            else
            {
                double cycle = CycleLength;
                double baseY = -Remainder(phase, cycle);
                int copies = Math.Max(3, (int)Math.Ceiling(bounds.Height / cycle) + 3);
                for (int i = -1; i < copies; i++)
                {
                    drawingContext.DrawText(formatted, new Point(0, baseY + (i * cycle)));
                }
            }

            drawingContext.Pop();
        }

        protected override void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            model.HandleManualScroll(-e.Delta / 4.0);
            e.Handled = true;
        }

        private void TimerOnTick(object sender, EventArgs e)
        {
            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            stopwatch.Restart();
            elapsedSeconds = Math.Max(0, Math.Min(elapsedSeconds, 0.25));

            bool shouldInvalidate = false;

            if (model.IsRunning && HasRenderableScript())
            {
                phase += model.SpeedPointsPerSecond * elapsedSeconds;
                shouldInvalidate = true;

                if (model.ScrollMode == ScrollMode.StopAtEnd && hasMeasuredContentHeight)
                {
                    double target = EndPhase;
                    if (phase >= target)
                    {
                        phase = target;
                        if (!hasReachedEndInStopMode)
                        {
                            hasReachedEndInStopMode = true;
                            model.MarkReachedEndInStopMode();
                        }
                    }
                }

                if (model.ScrollMode == ScrollMode.Infinite)
                {
                    double cycle = CycleLength;
                    if (phase >= cycle * 8)
                    {
                        phase = Remainder(phase, cycle);
                    }
                }
            }

            if (shouldInvalidate)
            {
                InvalidateVisual();
            }
        }

        private void ModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Script")
            {
                hasMeasuredContentHeight = false;
                InvalidateTextLayout();
                ResetPhase();
            }
            else if (e.PropertyName == "FontSize")
            {
                hasMeasuredContentHeight = false;
                InvalidateTextLayout();
                if (phase <= Math.Max(12, model.FontSize * 1.6))
                {
                    ResetPhase();
                }
            }
            else if (e.PropertyName == "FontFamilyName")
            {
                hasMeasuredContentHeight = false;
                InvalidateTextLayout();
            }
            else if (e.PropertyName == "ScrollMode")
            {
                hasReachedEndInStopMode = false;
            }
            else if (e.PropertyName == "LanguageCode")
            {
                InvalidateTextLayout();
            }

            InvalidateVisual();
        }

        private void ModelOnResetRequested(object sender, EventArgs e)
        {
            ResetPhase();
            InvalidateVisual();
        }

        private void ModelOnJumpBackRequested(object sender, JumpBackEventArgs e)
        {
            hasReachedEndInStopMode = false;
            phase = Math.Max(phase - Math.Max(0, e.DistancePoints), TopOfScriptPhaseFloor);
            InvalidateVisual();
        }

        private void ModelOnManualScrollRequested(object sender, ManualScrollEventArgs e)
        {
            hasReachedEndInStopMode = false;
            phase += e.DeltaPoints;

            if (model.ScrollMode == ScrollMode.StopAtEnd && hasMeasuredContentHeight)
            {
                phase = Math.Min(Math.Max(phase, TopOfScriptPhaseFloor), EndPhase);
            }
            else
            {
                double cycle = CycleLength;
                if (Math.Abs(phase) >= cycle * 8)
                {
                    phase = Remainder(phase, cycle);
                }

                phase = Math.Max(phase, TopOfScriptPhaseFloor);
            }

            InvalidateVisual();
        }

        private void ResetPhase()
        {
            phase = TopOfScriptPhaseFloor;
            hasReachedEndInStopMode = false;
            stopwatch.Restart();
        }

        private bool HasRenderableScript()
        {
            return (model.Script ?? "").Trim().Length > 0 && model.HasStartedSession;
        }

        private double TopOfScriptPhaseFloor
        {
            get { return -StartAnchorOffset; }
        }

        private double StartAnchorOffset
        {
            get
            {
                double fallback = Math.Max(8, Math.Min(model.FontSize * 0.45, 22));
                if (ActualHeight <= 1)
                {
                    return fallback;
                }

                double raw = TopFadeClearInset + Math.Max(2, model.FontSize * 0.12);
                double capped = Math.Min(raw, Math.Max(18, ActualHeight * 0.38));
                return Math.Max(capped, fallback);
            }
        }

        private double TopFadeClearInset
        {
            get { return Math.Max(0, ActualHeight * 0.20); }
        }

        private double CycleLength
        {
            get { return Math.Max(contentHeight + LoopGap, 1); }
        }

        private double EndPhase
        {
            get
            {
                double bottomInset = TopFadeClearInset + Math.Max(2, model.FontSize * 0.12);
                double lastLinePhase = contentHeight - Math.Max(0, ActualHeight - bottomInset);
                return Math.Max(TopOfScriptPhaseFloor, lastLinePhase);
            }
        }

        private static double Remainder(double value, double divisor)
        {
            if (Math.Abs(divisor) < 0.001)
            {
                return 0;
            }

            return value % divisor;
        }

        private FormattedText MakeFormattedText(string text, double fontSize, Brush brush)
        {
            Typeface typeface = new Typeface(
                new FontFamily(model.FontFamilyName),
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal);

            #pragma warning disable 0618
            FormattedText formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush);
            #pragma warning restore 0618

            return formatted;
        }

        private FormattedText GetFormattedScriptText(string text, double maxTextWidth)
        {
            if (layoutDirty ||
                cachedFormattedText == null ||
                cachedText != text ||
                Math.Abs(cachedFontSize - model.FontSize) > 0.001 ||
                cachedFontFamilyName != model.FontFamilyName ||
                Math.Abs(cachedTextWidth - maxTextWidth) > 0.5)
            {
                cachedFormattedText = MakeFormattedText(text, model.FontSize, Brushes.White);
                cachedFormattedText.MaxTextWidth = maxTextWidth;
                cachedFormattedText.TextAlignment = TextAlignment.Left;
                cachedText = text;
                cachedFontSize = model.FontSize;
                cachedFontFamilyName = model.FontFamilyName;
                cachedTextWidth = maxTextWidth;
                contentHeight = Math.Max(1, cachedFormattedText.Height);
                hasMeasuredContentHeight = true;
                layoutDirty = false;
            }

            return cachedFormattedText;
        }

        private void InvalidateTextLayout()
        {
            cachedFormattedText = null;
            cachedText = null;
            cachedFontFamilyName = null;
            layoutDirty = true;
            hasMeasuredContentHeight = false;
        }

        private void DrawCenteredMessage(DrawingContext drawingContext, string message)
        {
            Typeface typeface = new Typeface(
                new FontFamily(model.FontFamilyName),
                FontStyles.Normal,
                FontWeights.SemiBold,
                FontStretches.Normal);

            #pragma warning disable 0618
            FormattedText shadow = new FormattedText(
                message,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Max(model.FontSize * 0.72, 13),
                new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)));

            FormattedText formatted = new FormattedText(
                message,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                Math.Max(model.FontSize * 0.72, 13),
                new SolidColorBrush(Color.FromArgb(205, 235, 238, 246)));
            #pragma warning restore 0618

            formatted.MaxTextWidth = Math.Max(1, ActualWidth - 24);
            shadow.MaxTextWidth = formatted.MaxTextWidth;
            formatted.TextAlignment = TextAlignment.Center;
            shadow.TextAlignment = TextAlignment.Center;
            double textWidth = formatted.MaxTextWidth;
            double x = (ActualWidth - textWidth) / 2;
            double y = (ActualHeight - formatted.Height) / 2;
            drawingContext.DrawText(shadow, new Point(Math.Max(0, x), Math.Max(0, y + 1)));
            drawingContext.DrawText(formatted, new Point(Math.Max(0, x), Math.Max(0, y)));
        }

    }
}
