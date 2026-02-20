#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Globalization;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    #region Enums

    public enum WatermarkTfStyle
    {
        TradingView,
        MetaTrader
    }

    public enum WatermarkRowContent
    {
        Ticker,
        Timeframe,
        Exchange,
        Countdown,
        CustomText,
        Empty
    }

    public enum WatermarkVerticalPosition
    {
        Top,
        Middle,
        Bottom
    }

    public enum WatermarkHorizontalPosition
    {
        Left,
        Center,
        Right
    }

    public enum WatermarkTextAlign
    {
        Left,
        Center,
        Right
    }

    #endregion

    public class TopRightWatermark : Indicator
    {
        #region Private Variables

        private SharpDX.Direct2D1.Brush textBrushDx;
        private TextFormat textFormat1;
        private TextFormat textFormat2;
        private TextFormat textFormat3;
        private TextFormat textFormat4;
        private bool needsRecreateResources = true;

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Displays symbol, timeframe, and countdown watermark on chart";
                Name = "TopRightWatermark";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = false;
                ScaleJustification = ScaleJustification.Right;

                // Style parameters
                FontSize = 25;
                TextColor = Brushes.Gray;
                TextOpacity = 30;
                VPosition = WatermarkVerticalPosition.Top;
                HPosition = WatermarkHorizontalPosition.Right;
                TxtAlignment = WatermarkTextAlign.Center;
                TimeframeStyle = WatermarkTfStyle.TradingView;
                Margin = 10;

                // Row content
                Row1 = WatermarkRowContent.Ticker;
                Row1FontSize = 0;
                Row2 = WatermarkRowContent.Timeframe;
                Row2FontSize = 0;
                Row3 = WatermarkRowContent.Countdown;
                Row3FontSize = 15;
                Row4 = WatermarkRowContent.Empty;
                Row4FontSize = 0;
                CustomTextValue = "custom text";

                // Typography
                IsBold = false;
                IsItalic = false;
                FontFamily = "Arial";
            }
            else if (State == State.Configure)
            {
            }
            else if (State == State.Terminated)
            {
                DisposeResources();
            }
        }

        private void DisposeResources()
        {
            if (textBrushDx != null)
            {
                textBrushDx.Dispose();
                textBrushDx = null;
            }
            if (textFormat1 != null)
            {
                textFormat1.Dispose();
                textFormat1 = null;
            }
            if (textFormat2 != null)
            {
                textFormat2.Dispose();
                textFormat2 = null;
            }
            if (textFormat3 != null)
            {
                textFormat3.Dispose();
                textFormat3 = null;
            }
            if (textFormat4 != null)
            {
                textFormat4.Dispose();
                textFormat4 = null;
            }
        }

        protected override void OnBarUpdate()
        {
            // Force chart to refresh so countdown updates in real-time
            if (IsFirstTickOfBar || Calculate == Calculate.OnEachTick)
                ForceRefresh();
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (chartControl == null || ChartPanel == null)
                return;

            SharpDX.Direct2D1.RenderTarget renderTarget = RenderTarget;
            if (renderTarget == null)
                return;

            // Create/recreate resources if needed
            if (needsRecreateResources || textBrushDx == null)
            {
                CreateResources(renderTarget);
                needsRecreateResources = false;
            }

            // Get chart panel bounds
            float panelWidth = (float)ChartPanel.W;
            float panelHeight = (float)ChartPanel.H;

            // Get text for each row
            string row1Text = GetRowText(Row1);
            string row2Text = GetRowText(Row2);
            string row3Text = GetRowText(Row3);
            string row4Text = GetRowText(Row4);

            // Measure text sizes
            Size2F size1 = MeasureText(row1Text, textFormat1, renderTarget);
            Size2F size2 = MeasureText(row2Text, textFormat2, renderTarget);
            Size2F size3 = MeasureText(row3Text, textFormat3, renderTarget);
            Size2F size4 = MeasureText(row4Text, textFormat4, renderTarget);

            // Calculate total block size
            float maxWidth = Math.Max(Math.Max(size1.Width, size2.Width), Math.Max(size3.Width, size4.Width));
            float totalHeight = size1.Height + size2.Height + size3.Height + size4.Height;

            // Calculate position based on alignment settings
            float blockX = CalculateHorizontalPosition(panelWidth, maxWidth);
            float blockY = CalculateVerticalPosition(panelHeight, totalHeight);

            float currentY = blockY;

            // Draw each row
            if (!string.IsNullOrEmpty(row1Text))
            {
                float x1 = CalculateTextX(blockX, maxWidth, size1.Width);
                DrawText(renderTarget, row1Text, textFormat1, x1, currentY);
                currentY += size1.Height;
            }

            if (!string.IsNullOrEmpty(row2Text))
            {
                float x2 = CalculateTextX(blockX, maxWidth, size2.Width);
                DrawText(renderTarget, row2Text, textFormat2, x2, currentY);
                currentY += size2.Height;
            }

            if (!string.IsNullOrEmpty(row3Text))
            {
                float x3 = CalculateTextX(blockX, maxWidth, size3.Width);
                DrawText(renderTarget, row3Text, textFormat3, x3, currentY);
                currentY += size3.Height;
            }

            if (!string.IsNullOrEmpty(row4Text))
            {
                float x4 = CalculateTextX(blockX, maxWidth, size4.Width);
                DrawText(renderTarget, row4Text, textFormat4, x4, currentY);
            }
        }

        private void CreateResources(SharpDX.Direct2D1.RenderTarget renderTarget)
        {
            DisposeResources();

            // Create brush with opacity
            System.Windows.Media.Color mediaColor = ((SolidColorBrush)TextColor).Color;
            byte alpha = (byte)(255 * TextOpacity / 100.0);
            SharpDX.Color dxColor = new SharpDX.Color(mediaColor.R, mediaColor.G, mediaColor.B, alpha);
            textBrushDx = new SharpDX.Direct2D1.SolidColorBrush(renderTarget, dxColor);

            // Create font styles
            SharpDX.DirectWrite.FontWeight fontWeight = IsBold ? SharpDX.DirectWrite.FontWeight.Bold : SharpDX.DirectWrite.FontWeight.Normal;
            SharpDX.DirectWrite.FontStyle fontStyle = IsItalic ? SharpDX.DirectWrite.FontStyle.Italic : SharpDX.DirectWrite.FontStyle.Normal;

            using (var factory = new SharpDX.DirectWrite.Factory())
            {
                int size1 = Row1FontSize > 0 ? Row1FontSize : FontSize;
                int size2 = Row2FontSize > 0 ? Row2FontSize : FontSize;
                int size3 = Row3FontSize > 0 ? Row3FontSize : FontSize;
                int size4 = Row4FontSize > 0 ? Row4FontSize : FontSize;

                textFormat1 = new TextFormat(factory, FontFamily, fontWeight, fontStyle, size1);
                textFormat2 = new TextFormat(factory, FontFamily, fontWeight, fontStyle, size2);
                textFormat3 = new TextFormat(factory, FontFamily, fontWeight, fontStyle, size3);
                textFormat4 = new TextFormat(factory, FontFamily, fontWeight, fontStyle, size4);
            }
        }

        private Size2F MeasureText(string text, TextFormat format, SharpDX.Direct2D1.RenderTarget renderTarget)
        {
            if (string.IsNullOrEmpty(text))
                return new Size2F(0, 0);

            using (var factory = new SharpDX.DirectWrite.Factory())
            using (var layout = new TextLayout(factory, text, format, float.MaxValue, float.MaxValue))
            {
                return new Size2F(layout.Metrics.Width, layout.Metrics.Height);
            }
        }

        private void DrawText(SharpDX.Direct2D1.RenderTarget renderTarget, string text, TextFormat format, float x, float y)
        {
            if (string.IsNullOrEmpty(text) || textBrushDx == null)
                return;

            using (var factory = new SharpDX.DirectWrite.Factory())
            using (var layout = new TextLayout(factory, text, format, float.MaxValue, float.MaxValue))
            {
                renderTarget.DrawTextLayout(new Vector2(x, y), layout, textBrushDx);
            }
        }

        private float CalculateHorizontalPosition(float panelWidth, float blockWidth)
        {
            switch (HPosition)
            {
                case WatermarkHorizontalPosition.Left:
                    return Margin;
                case WatermarkHorizontalPosition.Center:
                    return (panelWidth - blockWidth) / 2;
                case WatermarkHorizontalPosition.Right:
                default:
                    return panelWidth - blockWidth - Margin;
            }
        }

        private float CalculateVerticalPosition(float panelHeight, float blockHeight)
        {
            switch (VPosition)
            {
                case WatermarkVerticalPosition.Top:
                default:
                    return Margin;
                case WatermarkVerticalPosition.Middle:
                    return (panelHeight - blockHeight) / 2;
                case WatermarkVerticalPosition.Bottom:
                    return panelHeight - blockHeight - Margin;
            }
        }

        private float CalculateTextX(float blockX, float blockWidth, float textWidth)
        {
            switch (TxtAlignment)
            {
                case WatermarkTextAlign.Left:
                    return blockX;
                case WatermarkTextAlign.Center:
                default:
                    return blockX + (blockWidth - textWidth) / 2;
                case WatermarkTextAlign.Right:
                    return blockX + blockWidth - textWidth;
            }
        }

        private string GetRowText(WatermarkRowContent content)
        {
            switch (content)
            {
                case WatermarkRowContent.Ticker:
                    return Instrument != null ? Instrument.MasterInstrument.Name : "Symbol";
                case WatermarkRowContent.Timeframe:
                    return FormatTimeframe();
                case WatermarkRowContent.Exchange:
                    return GetExchangeName();
                case WatermarkRowContent.Countdown:
                    return GetCountdownText();
                case WatermarkRowContent.CustomText:
                    return CustomTextValue ?? string.Empty;
                case WatermarkRowContent.Empty:
                default:
                    return string.Empty;
            }
        }

        private string GetExchangeName()
        {
            try
            {
                if (Instrument != null)
                {
                    string exchange = Instrument.Exchange.ToString();
                    if (!string.IsNullOrEmpty(exchange))
                        return exchange;
                }
                return "Exchange";
            }
            catch
            {
                return "Exchange";
            }
        }

        private string FormatTimeframe()
        {
            if (BarsPeriod == null)
                return "TF";

            int value = BarsPeriod.Value;
            BarsPeriodType periodType = BarsPeriod.BarsPeriodType;

            if (TimeframeStyle == WatermarkTfStyle.TradingView)
            {
                switch (periodType)
                {
                    case BarsPeriodType.Second:
                        return string.Format("{0}s", value);
                    case BarsPeriodType.Minute:
                        return string.Format("{0}m", value);
                    case BarsPeriodType.Day:
                        return string.Format("{0}D", value);
                    case BarsPeriodType.Week:
                        return string.Format("{0}W", value);
                    case BarsPeriodType.Month:
                        return string.Format("{0}M", value);
                    case BarsPeriodType.Year:
                        return string.Format("{0}Y", value);
                    case BarsPeriodType.Tick:
                        return string.Format("{0}T", value);
                    case BarsPeriodType.Range:
                        return string.Format("{0}R", value);
                    case BarsPeriodType.Renko:
                        return string.Format("{0}Renko", value);
                    default:
                        return BarsPeriod.ToString();
                }
            }
            else // MetaTrader style
            {
                switch (periodType)
                {
                    case BarsPeriodType.Second:
                        return string.Format("S{0}", value);
                    case BarsPeriodType.Minute:
                        return string.Format("M{0}", value);
                    case BarsPeriodType.Day:
                        return string.Format("D{0}", value);
                    case BarsPeriodType.Week:
                        return string.Format("W{0}", value);
                    case BarsPeriodType.Month:
                        return value == 1 ? "MN" : string.Format("MN{0}", value);
                    case BarsPeriodType.Year:
                        return string.Format("Y{0}", value);
                    case BarsPeriodType.Tick:
                        return string.Format("T{0}", value);
                    case BarsPeriodType.Range:
                        return string.Format("R{0}", value);
                    case BarsPeriodType.Renko:
                        return string.Format("Renko{0}", value);
                    default:
                        return BarsPeriod.ToString();
                }
            }
        }

        private string GetCountdownText()
        {
            try
            {
                if (BarsPeriod == null)
                    return "--:--";

                int barPeriodValue = BarsPeriod.Value;
                BarsPeriodType periodType = BarsPeriod.BarsPeriodType;

                // Only support time-based periods
                if (periodType != BarsPeriodType.Second &&
                    periodType != BarsPeriodType.Minute &&
                    periodType != BarsPeriodType.Day &&
                    periodType != BarsPeriodType.Week)
                    return "--:--";

                DateTime now = DateTime.Now;
                int remainingSeconds = 0;

                if (periodType == BarsPeriodType.Second)
                {
                    int totalSeconds = now.Hour * 3600 + now.Minute * 60 + now.Second;
                    int secondsIntoBar = totalSeconds % barPeriodValue;
                    remainingSeconds = barPeriodValue - secondsIntoBar;
                }
                else if (periodType == BarsPeriodType.Minute)
                {
                    int totalSeconds = now.Hour * 3600 + now.Minute * 60 + now.Second;
                    int barSeconds = barPeriodValue * 60;
                    int secondsIntoBar = totalSeconds % barSeconds;
                    remainingSeconds = barSeconds - secondsIntoBar;
                }
                else if (periodType == BarsPeriodType.Day)
                {
                    // Time until midnight
                    DateTime midnight = now.Date.AddDays(1);
                    remainingSeconds = (int)(midnight - now).TotalSeconds;
                }
                else if (periodType == BarsPeriodType.Week)
                {
                    // Time until end of week (Sunday midnight)
                    int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
                    if (daysUntilSunday == 0) daysUntilSunday = 7;
                    DateTime endOfWeek = now.Date.AddDays(daysUntilSunday);
                    remainingSeconds = (int)(endOfWeek - now).TotalSeconds;
                }

                if (remainingSeconds <= 0)
                    remainingSeconds = 0;

                return FormatTimeSpan(TimeSpan.FromSeconds(remainingSeconds));
            }
            catch
            {
                return "--:--";
            }
        }

        private TimeSpan? GetBarDuration()
        {
            if (BarsPeriod == null)
                return null;

            int value = BarsPeriod.Value;
            BarsPeriodType periodType = BarsPeriod.BarsPeriodType;

            switch (periodType)
            {
                case BarsPeriodType.Second:
                    return TimeSpan.FromSeconds(value);
                case BarsPeriodType.Minute:
                    return TimeSpan.FromMinutes(value);
                case BarsPeriodType.Day:
                    return TimeSpan.FromDays(value);
                case BarsPeriodType.Week:
                    return TimeSpan.FromDays(value * 7);
                default:
                    return null; // Tick, Range, Renko, etc. - skip countdown
            }
        }

        private string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalDays >= 1)
            {
                return string.Format("{0}d {1:D2}:{2:D2}:{3:D2}",
                    (int)span.TotalDays,
                    span.Hours,
                    span.Minutes,
                    span.Seconds);
            }
            else if (span.TotalHours >= 1)
            {
                return string.Format("{0}:{1:D2}:{2:D2}",
                    (int)span.TotalHours,
                    span.Minutes,
                    span.Seconds);
            }
            else
            {
                return string.Format("{0:D2}:{1:D2}",
                    (int)span.TotalMinutes,
                    span.Seconds);
            }
        }

        public override void OnRenderTargetChanged()
        {
            needsRecreateResources = true;
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(8, 200)]
        [Display(Name = "Font Size", Order = 10, GroupName = "Watermark Style")]
        public int FontSize { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Text Color", Order = 20, GroupName = "Watermark Style")]
        public Brush TextColor { get; set; }

        [Browsable(false)]
        public string TextColorSerializable
        {
            get { return Serialize.BrushToString(TextColor); }
            set { TextColor = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Text Opacity %", Order = 25, GroupName = "Watermark Style")]
        public int TextOpacity { get; set; }

        [Display(Name = "Vertical Position", Order = 30, GroupName = "Watermark Style")]
        public WatermarkVerticalPosition VPosition { get; set; }

        [Display(Name = "Horizontal Position", Order = 40, GroupName = "Watermark Style")]
        public WatermarkHorizontalPosition HPosition { get; set; }

        [Display(Name = "Text Alignment", Order = 50, GroupName = "Watermark Style")]
        public WatermarkTextAlign TxtAlignment { get; set; }

        [Display(Name = "Timeframe Style", Order = 60, GroupName = "Watermark Style")]
        public WatermarkTfStyle TimeframeStyle { get; set; }

        [NinjaScriptProperty]
        [Range(0, 500)]
        [Display(Name = "Margin (px)", Order = 70, GroupName = "Watermark Style")]
        public int Margin { get; set; }

        [Display(Name = "Row 1", Order = 100, GroupName = "Row Content")]
        public WatermarkRowContent Row1 { get; set; }

        [Range(0, 200)]
        [Display(Name = "Row 1 Font Size (0=inherit)", Order = 101, GroupName = "Row Content")]
        public int Row1FontSize { get; set; }

        [Display(Name = "Row 2", Order = 110, GroupName = "Row Content")]
        public WatermarkRowContent Row2 { get; set; }

        [Range(0, 200)]
        [Display(Name = "Row 2 Font Size (0=inherit)", Order = 111, GroupName = "Row Content")]
        public int Row2FontSize { get; set; }

        [Display(Name = "Row 3", Order = 120, GroupName = "Row Content")]
        public WatermarkRowContent Row3 { get; set; }

        [Range(0, 200)]
        [Display(Name = "Row 3 Font Size (0=inherit)", Order = 121, GroupName = "Row Content")]
        public int Row3FontSize { get; set; }

        [Display(Name = "Row 4", Order = 130, GroupName = "Row Content")]
        public WatermarkRowContent Row4 { get; set; }

        [Range(0, 200)]
        [Display(Name = "Row 4 Font Size (0=inherit)", Order = 131, GroupName = "Row Content")]
        public int Row4FontSize { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Custom Text", Order = 140, GroupName = "Row Content")]
        public string CustomTextValue { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Bold", Order = 200, GroupName = "Typography")]
        public bool IsBold { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Italic", Order = 210, GroupName = "Typography")]
        public bool IsItalic { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Font Family", Order = 220, GroupName = "Typography")]
        public string FontFamily { get; set; }

        #endregion
    }
}
