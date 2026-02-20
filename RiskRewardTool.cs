#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	#region Enums

	public enum RiskMode
	{
		FixedCash,
		PercentOfAccount,
		FixedContracts
	}

	#endregion

	public class RiskRewardTool : Strategy
	{
		#region Variables

		private enum DragTarget { None, Entry, StopLoss, TakeProfit, BoxMove, BoxResizeLeft, BoxResizeRight }
		private const float EdgeHitZone = 8; // pixels from edge to trigger resize

		// Box horizontal position (left edge X in pixels, draggable)
		private float boxLeftX = -1;
		private float boxWidth = 200;
		private float dragStartMouseX;
		private float dragStartMouseY;
		private double dragStartEntryPrice;
		private double dragStartSlPrice;
		private double dragStartTpPrice;

		// Price levels
		private double entryPrice;
		private double slPrice;
		private double tpPrice;

		// Dragging state
		private bool isDragging;
		private DragTarget currentDragTarget;

		// SharpDX resources
		private SharpDX.Direct2D1.Brush entryBrushDX;
		private SharpDX.Direct2D1.Brush slBrushDX;
		private SharpDX.Direct2D1.Brush tpBrushDX;
		private SharpDX.Direct2D1.Brush riskZoneBrushDX;
		private SharpDX.Direct2D1.Brush rewardZoneBrushDX;
		private SharpDX.Direct2D1.Brush panelBrushDX;
		private SharpDX.Direct2D1.Brush textBrushDX;
		private SharpDX.DirectWrite.TextFormat textFormat;
		private SharpDX.DirectWrite.TextFormat titleTextFormat;

		// Calculated values
		private int contractSize;
		private double riskDollars;
		private double rewardDollars;
		private double rrRatio;
		private int slTicks;
		private int tpTicks;
		private double slPoints;
		private double tpPoints;

		// Current market price (updated every tick)
		private double currentMarketPrice;

		// Order tracking
		private Order entryOrder;
		private Order stopOrder;
		private Order targetOrder;
		private string ocoId;

		// Hit test threshold in pixels
		private const int HitTestThreshold = 10;

		// Track if levels have been initialized
		private bool levelsInitialized;

		// Visibility toggle
		private bool isToolVisible = true;

		// Cached chart scale reference for mouse handlers
		private ChartScale cachedChartScale;

		#endregion

		#region Parameters

		[NinjaScriptProperty]
		[Display(Name = "Risk Mode", Description = "How to calculate position size", Order = 1, GroupName = "1. Risk Settings")]
		public RiskMode SelectedRiskMode { get; set; }

		[NinjaScriptProperty]
		[Range(0.01, double.MaxValue)]
		[Display(Name = "Risk Value", Description = "Dollar amount, percentage, or contracts", Order = 2, GroupName = "1. Risk Settings")]
		public double RiskValue { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name = "Default SL Ticks", Description = "Default stop loss distance in ticks", Order = 3, GroupName = "1. Risk Settings")]
		public int DefaultSLTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name = "Default TP Ticks", Description = "Default take profit distance in ticks", Order = 4, GroupName = "1. Risk Settings")]
		public int DefaultTPTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show Confirmation", Description = "Show confirmation dialog before executing", Order = 1, GroupName = "2. Order Settings")]
		public bool ShowConfirmation { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Entry Line Color", Order = 1, GroupName = "3. Visual Settings")]
		public System.Windows.Media.Brush EntryLineBrush { get; set; }

		[Browsable(false)]
		public string EntryLineBrushSerialize
		{
			get { return Serialize.BrushToString(EntryLineBrush); }
			set { EntryLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Stop Loss Color", Order = 2, GroupName = "3. Visual Settings")]
		public System.Windows.Media.Brush StopLossLineBrush { get; set; }

		[Browsable(false)]
		public string StopLossLineBrushSerialize
		{
			get { return Serialize.BrushToString(StopLossLineBrush); }
			set { StopLossLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Take Profit Color", Order = 3, GroupName = "3. Visual Settings")]
		public System.Windows.Media.Brush TakeProfitLineBrush { get; set; }

		[Browsable(false)]
		public string TakeProfitLineBrushSerialize
		{
			get { return Serialize.BrushToString(TakeProfitLineBrush); }
			set { TakeProfitLineBrush = Serialize.StringToBrush(value); }
		}

		[NinjaScriptProperty]
		[Range(1, 5)]
		[Display(Name = "Line Width", Order = 4, GroupName = "3. Visual Settings")]
		public int LineWidth { get; set; }

		[NinjaScriptProperty]
		[Range(5, 80)]
		[Display(Name = "Zone Opacity %", Order = 5, GroupName = "3. Visual Settings")]
		public int ZoneOpacity { get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name = "Text Color", Order = 6, GroupName = "3. Visual Settings")]
		public System.Windows.Media.Brush TextBrush { get; set; }

		[Browsable(false)]
		public string TextBrushSerialize
		{
			get { return Serialize.BrushToString(TextBrush); }
			set { TextBrush = Serialize.StringToBrush(value); }
		}

		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = "Visual Risk/Reward tool with one-click order execution";
				Name = "RiskRewardTool";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				DisplayInDataBox = false;
				DrawOnPricePanel = true;
				IsFillLimitOnTouch = false;
				ScaleJustification = ScaleJustification.Right;

				// Default parameter values
				SelectedRiskMode = RiskMode.PercentOfAccount;
				RiskValue = 1.0;
				DefaultSLTicks = 50;
				DefaultTPTicks = 100;
				ShowConfirmation = true;

				EntryLineBrush = Brushes.DodgerBlue;
				StopLossLineBrush = Brushes.Crimson;
				TakeProfitLineBrush = Brushes.LimeGreen;
				LineWidth = 2;
				ZoneOpacity = 20;
				TextBrush = Brushes.White;
			}
			else if (State == State.Configure)
			{
				IsUnmanaged = true;
			}
			else if (State == State.DataLoaded)
			{
				levelsInitialized = false;
			}
			else if (State == State.Realtime)
			{
				// Subscribe to mouse events on the UI thread
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						ChartControl.MouseDown += OnChartMouseDown;
						ChartControl.MouseMove += OnChartMouseMove;
						ChartControl.MouseUp += OnChartMouseUp;
					});
				}
			}
			else if (State == State.Terminated)
			{
				// Unsubscribe from mouse events
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						ChartControl.MouseDown -= OnChartMouseDown;
						ChartControl.MouseMove -= OnChartMouseMove;
						ChartControl.MouseUp -= OnChartMouseUp;
					});
				}

				DisposeSharpDXResources();
			}
		}

		protected override void OnBarUpdate()
		{
			// Only initialize on the very last historical bar or in realtime
			// This ensures entryPrice is near current price, not from days ago
			if (!levelsInitialized && Close[0] > 0)
			{
				bool isLastHistoricalBar = (State == State.Historical && CurrentBar == Bars.Count - 2);
				bool isRealtime = (State == State.Realtime);

				if (isLastHistoricalBar || isRealtime)
				{
					entryPrice = Close[0];
					slPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice - (DefaultSLTicks * TickSize));
					tpPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice + (DefaultTPTicks * TickSize));
					levelsInitialized = true;
					RecalculateValues();
				}
			}

			// Always keep current market price up to date
			if (Close[0] > 0)
				currentMarketPrice = Close[0];

			if (levelsInitialized)
				RecalculateValues();
		}

		#region Mouse Event Handlers

		private void OnChartMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (cachedChartScale == null) return;

			// Ctrl+Right-click for strategy context menu (normal right-click passes through to NT8)
			if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed
				&& (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
			{
				ShowContextMenu();
				e.Handled = true;
				return;
			}

			if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
			if (!isToolVisible) return;

			System.Windows.Point point = e.GetPosition(ChartControl);
			float mouseX = (float)point.X;
			float mouseY = (float)point.Y;

			float entryY = cachedChartScale.GetYByValue(entryPrice);
			float slY = cachedChartScale.GetYByValue(slPrice);
			float tpY = cachedChartScale.GetYByValue(tpPrice);

			float boxRight = boxLeftX + boxWidth;
			bool inBoxX = (mouseX >= boxLeftX && mouseX <= boxRight);

			// Check line hit-tests (within box horizontal range and near labels)
			if (Math.Abs(mouseY - entryY) <= HitTestThreshold)
			{
				isDragging = true;
				currentDragTarget = DragTarget.Entry;
				e.Handled = true;
			}
			else if (Math.Abs(mouseY - slY) <= HitTestThreshold)
			{
				isDragging = true;
				currentDragTarget = DragTarget.StopLoss;
				e.Handled = true;
			}
			else if (Math.Abs(mouseY - tpY) <= HitTestThreshold)
			{
				isDragging = true;
				currentDragTarget = DragTarget.TakeProfit;
				e.Handled = true;
			}
			// Check vertical edge resize zones first, then box move
			else
			{
				float minZoneY = Math.Min(entryY, Math.Min(slY, tpY));
				float maxZoneY = Math.Max(entryY, Math.Max(slY, tpY));
				bool inBoxY = (mouseY >= minZoneY && mouseY <= maxZoneY);

				if (inBoxY && Math.Abs(mouseX - boxLeftX) <= EdgeHitZone)
				{
					// Left edge resize
					isDragging = true;
					currentDragTarget = DragTarget.BoxResizeLeft;
					dragStartMouseX = mouseX;
					e.Handled = true;
				}
				else if (inBoxY && Math.Abs(mouseX - (boxLeftX + boxWidth)) <= EdgeHitZone)
				{
					// Right edge resize
					isDragging = true;
					currentDragTarget = DragTarget.BoxResizeRight;
					dragStartMouseX = mouseX;
					e.Handled = true;
				}
				else if (inBoxX && inBoxY)
				{
					// Box move
					isDragging = true;
					currentDragTarget = DragTarget.BoxMove;
					dragStartMouseX = mouseX;
					dragStartMouseY = mouseY;
					dragStartEntryPrice = entryPrice;
					dragStartSlPrice = slPrice;
					dragStartTpPrice = tpPrice;
					e.Handled = true;
				}
			}
		}

		private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!isDragging || cachedChartScale == null) return;

			System.Windows.Point point = e.GetPosition(ChartControl);
			float mouseX = (float)point.X;
			float mouseY = (float)point.Y;

			if (currentDragTarget == DragTarget.BoxMove)
			{
				// Horizontal movement
				float deltaX = mouseX - dragStartMouseX;
				boxLeftX += deltaX;
				dragStartMouseX = mouseX;

				// Clamp to chart bounds
				float maxX = (float)ChartControl.ActualWidth - 170 - boxWidth;
				if (boxLeftX < 0) boxLeftX = 0;
				if (boxLeftX > maxX) boxLeftX = maxX;

				// Vertical movement - shift all 3 prices by same amount (preserves box shape)
				double priceAtStart = cachedChartScale.GetValueByY(dragStartMouseY);
				double priceAtNow = cachedChartScale.GetValueByY(mouseY);
				double priceDelta = priceAtNow - priceAtStart;

				entryPrice = Instrument.MasterInstrument.RoundToTickSize(dragStartEntryPrice + priceDelta);
				slPrice = Instrument.MasterInstrument.RoundToTickSize(dragStartSlPrice + priceDelta);
				tpPrice = Instrument.MasterInstrument.RoundToTickSize(dragStartTpPrice + priceDelta);

				RecalculateValues();
			}
			else if (currentDragTarget == DragTarget.BoxResizeLeft)
			{
				float deltaX = mouseX - dragStartMouseX;
				float newLeftX = boxLeftX + deltaX;
				float newWidth = boxWidth - deltaX;

				// Enforce minimum width
				if (newWidth >= 60)
				{
					boxLeftX = newLeftX;
					boxWidth = newWidth;
					dragStartMouseX = mouseX;
				}
			}
			else if (currentDragTarget == DragTarget.BoxResizeRight)
			{
				float deltaX = mouseX - dragStartMouseX;
				float newWidth = boxWidth + deltaX;

				// Enforce minimum width and max bound
				float maxRight = (float)ChartControl.ActualWidth - 170;
				if (newWidth >= 60 && boxLeftX + newWidth <= maxRight)
				{
					boxWidth = newWidth;
					dragStartMouseX = mouseX;
				}
			}
			else
			{
				double newPrice = cachedChartScale.GetValueByY(mouseY);
				newPrice = Instrument.MasterInstrument.RoundToTickSize(newPrice);

				switch (currentDragTarget)
				{
					case DragTarget.Entry:
						entryPrice = newPrice;
						break;
					case DragTarget.StopLoss:
						slPrice = newPrice;
						break;
					case DragTarget.TakeProfit:
						tpPrice = newPrice;
						break;
				}

				RecalculateValues();
			}

			ChartControl.InvalidateVisual();
			e.Handled = true;
		}

		private void OnChartMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (isDragging)
			{
				isDragging = false;
				currentDragTarget = DragTarget.None;
				e.Handled = true;
			}
		}

		#endregion

		#region Calculation Methods

		private void RecalculateValues()
		{
			if (TickSize <= 0) return;

			slTicks = (int)Math.Round(Math.Abs(entryPrice - slPrice) / TickSize);
			tpTicks = (int)Math.Round(Math.Abs(tpPrice - entryPrice) / TickSize);

			// Points = price distance (ticks * tickSize)
			slPoints = Math.Abs(entryPrice - slPrice);
			tpPoints = Math.Abs(tpPrice - entryPrice);

			contractSize = CalculateContractSize();

			double tickValue = Instrument.MasterInstrument.PointValue * TickSize;
			riskDollars = slTicks * tickValue * contractSize;
			rewardDollars = tpTicks * tickValue * contractSize;

			rrRatio = slTicks > 0 ? (double)tpTicks / slTicks : 0;
		}

		private int CalculateContractSize()
		{
			double tickValue = Instrument.MasterInstrument.PointValue * TickSize;
			double slValue = slTicks * tickValue;

			if (slValue <= 0) return 1;

			switch (SelectedRiskMode)
			{
				case RiskMode.FixedCash:
					return Math.Max(1, (int)Math.Floor(RiskValue / slValue));

				case RiskMode.PercentOfAccount:
					double accountBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
					double accountRisk = accountBalance * (RiskValue / 100.0);
					return Math.Max(1, (int)Math.Floor(accountRisk / slValue));

				case RiskMode.FixedContracts:
					return Math.Max(1, (int)RiskValue);

				default:
					return 1;
			}
		}

		private bool IsLongSetup()
		{
			return tpPrice > entryPrice;
		}

		#endregion

		#region SharpDX Rendering

		public override void OnRenderTargetChanged()
		{
			DisposeSharpDXResources();

			if (RenderTarget == null) return;

			try
			{
				entryBrushDX = EntryLineBrush.ToDxBrush(RenderTarget);
				slBrushDX = StopLossLineBrush.ToDxBrush(RenderTarget);
				tpBrushDX = TakeProfitLineBrush.ToDxBrush(RenderTarget);

				var riskColor = ((System.Windows.Media.SolidColorBrush)StopLossLineBrush).Color;
				riskZoneBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color(riskColor.R, riskColor.G, riskColor.B, (byte)(255 * ZoneOpacity / 100)));

				var rewardColor = ((System.Windows.Media.SolidColorBrush)TakeProfitLineBrush).Color;
				rewardZoneBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color(rewardColor.R, rewardColor.G, rewardColor.B, (byte)(255 * ZoneOpacity / 100)));

				panelBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
					new SharpDX.Color(30, 30, 30, 230));

				textBrushDX = TextBrush.ToDxBrush(RenderTarget);

				textFormat = new SharpDX.DirectWrite.TextFormat(
					Core.Globals.DirectWriteFactory,
					"Consolas",
					SharpDX.DirectWrite.FontWeight.Bold,
					SharpDX.DirectWrite.FontStyle.Normal,
					12f);

				titleTextFormat = new SharpDX.DirectWrite.TextFormat(
					Core.Globals.DirectWriteFactory,
					"Consolas",
					SharpDX.DirectWrite.FontWeight.Bold,
					SharpDX.DirectWrite.FontStyle.Normal,
					14f);
			}
			catch (Exception ex)
			{
				Print("RiskRewardTool: Error creating SharpDX resources - " + ex.Message);
			}
		}

		private void DisposeSharpDXResources()
		{
			if (entryBrushDX != null) { entryBrushDX.Dispose(); entryBrushDX = null; }
			if (slBrushDX != null) { slBrushDX.Dispose(); slBrushDX = null; }
			if (tpBrushDX != null) { tpBrushDX.Dispose(); tpBrushDX = null; }
			if (riskZoneBrushDX != null) { riskZoneBrushDX.Dispose(); riskZoneBrushDX = null; }
			if (rewardZoneBrushDX != null) { rewardZoneBrushDX.Dispose(); rewardZoneBrushDX = null; }
			if (panelBrushDX != null) { panelBrushDX.Dispose(); panelBrushDX = null; }
			if (textBrushDX != null) { textBrushDX.Dispose(); textBrushDX = null; }
			if (textFormat != null) { textFormat.Dispose(); textFormat = null; }
			if (titleTextFormat != null) { titleTextFormat.Dispose(); titleTextFormat = null; }
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			base.OnRender(chartControl, chartScale);

			if (RenderTarget == null) return;

			// Cache the chart scale for mouse event handlers
			cachedChartScale = chartScale;

			// Fallback initialization - always try from last bar close
			if (!levelsInitialized)
			{
				if (Bars != null && Bars.Count > 0)
				{
					entryPrice = Bars.GetClose(Bars.Count - 1);
					slPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice - (DefaultSLTicks * TickSize));
					tpPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice + (DefaultTPTicks * TickSize));
					levelsInitialized = true;
					RecalculateValues();
				}
			}

			if (!levelsInitialized) return;
			if (entryBrushDX == null || slBrushDX == null || tpBrushDX == null) return;

			// Skip rendering if tool is hidden
			if (!isToolVisible) return;

			float chartWidth = (float)chartControl.ActualWidth;
			float labelAreaWidth = 180f;

			float entryY = chartScale.GetYByValue(entryPrice);
			float slY = chartScale.GetYByValue(slPrice);
			float tpY = chartScale.GetYByValue(tpPrice);

			// Initialize box position to right-center of chart on first render
			if (boxLeftX < 0)
				boxLeftX = chartWidth - labelAreaWidth - boxWidth - 20;

			float boxRightX = boxLeftX + boxWidth;

			// Draw bounded zone boxes (TradingView style)
			DrawZoneBox(boxLeftX, entryY, boxRightX, slY, riskZoneBrushDX, slBrushDX);
			DrawZoneBox(boxLeftX, entryY, boxRightX, tpY, rewardZoneBrushDX, tpBrushDX);

			// Draw entry line across the box width
			RenderTarget.DrawLine(new Vector2(boxLeftX, entryY), new Vector2(boxRightX, entryY), entryBrushDX, LineWidth + 1);

			// Draw drag handles on all three lines
			float handleCenterX = boxLeftX + boxWidth / 2;
			DrawDragHandle(handleCenterX, entryY, entryBrushDX);
			DrawDragHandle(handleCenterX, slY, slBrushDX);
			DrawDragHandle(handleCenterX, tpY, tpBrushDX);

			// Draw dashed extension lines from box edges to labels
			var strokeStyle = new StrokeStyle(RenderTarget.Factory, new StrokeStyleProperties
			{
				DashStyle = SharpDX.Direct2D1.DashStyle.Dash,
				DashOffset = 0
			});

			float labelEdge = chartWidth - labelAreaWidth;
			RenderTarget.DrawLine(new Vector2(boxRightX, entryY), new Vector2(labelEdge, entryY), entryBrushDX, 1, strokeStyle);
			RenderTarget.DrawLine(new Vector2(boxRightX, slY), new Vector2(labelEdge, slY), slBrushDX, 1, strokeStyle);
			RenderTarget.DrawLine(new Vector2(boxRightX, tpY), new Vector2(labelEdge, tpY), tpBrushDX, 1, strokeStyle);

			strokeStyle.Dispose();

			// Draw price labels
			DrawPriceLabels(chartWidth, labelAreaWidth, entryY, slY, tpY);

			// Draw info panel
			DrawInfoPanel();
		}

		private void DrawZoneBox(float x1, float y1, float x2, float y2, SharpDX.Direct2D1.Brush fillBrush, SharpDX.Direct2D1.Brush borderBrush)
		{
			float top = Math.Min(y1, y2);
			float height = Math.Abs(y2 - y1);
			if (height < 1) return;

			float left = Math.Min(x1, x2);
			float width = Math.Abs(x2 - x1);

			var rect = new SharpDX.RectangleF(left, top, width, height);

			// Filled zone
			RenderTarget.FillRectangle(rect, fillBrush);

			// Border outline
			RenderTarget.DrawRectangle(rect, borderBrush, 1);

			// SL/TP line at the far edge from entry
			float lineY = (y2 == top) ? top : top + height;
			RenderTarget.DrawLine(new Vector2(left, lineY), new Vector2(left + width, lineY), borderBrush, LineWidth + 1);
		}

		private void DrawDragHandle(float centerX, float centerY, SharpDX.Direct2D1.Brush fillBrush)
		{
			float handleW = 20;
			float handleH = 12;
			var handleRect = new SharpDX.RectangleF(centerX - handleW / 2, centerY - handleH / 2, handleW, handleH);
			var handleRounded = new RoundedRectangle { Rect = handleRect, RadiusX = 2, RadiusY = 2 };
			RenderTarget.FillRoundedRectangle(handleRounded, fillBrush);

			// Grip lines
			float gripX1 = centerX - 4;
			float gripX2 = centerX + 4;
			RenderTarget.DrawLine(new Vector2(gripX1, centerY - 2), new Vector2(gripX2, centerY - 2), textBrushDX, 1);
			RenderTarget.DrawLine(new Vector2(gripX1, centerY + 1), new Vector2(gripX2, centerY + 1), textBrushDX, 1);
		}

		private void DrawPriceLabels(float chartWidth, float labelAreaWidth, float entryY, float slY, float tpY)
		{
			float rightMargin = 15;
			float labelWidth = labelAreaWidth - rightMargin - 5;
			float labelX = chartWidth - labelAreaWidth + 3;
			float labelHeight = 38;

			// Entry label (with contract size)
			string entryText = string.Format("ENTRY  {0}\n{1} contracts", FormatPrice(entryPrice), contractSize);
			DrawPriceLabel(labelX, entryY - labelHeight / 2, labelWidth, labelHeight, entryText, entryBrushDX);

			// SL label (show points)
			string slText = string.Format("SL  {0}\n{1}pts  -${2}", FormatPrice(slPrice), FormatPoints(slPoints), riskDollars.ToString("F0"));
			DrawPriceLabel(labelX, slY - labelHeight / 2, labelWidth, labelHeight, slText, slBrushDX);

			// TP label (show points)
			string tpText = string.Format("TP  {0}\n{1}pts  +${2}", FormatPrice(tpPrice), FormatPoints(tpPoints), rewardDollars.ToString("F0"));
			DrawPriceLabel(labelX, tpY - labelHeight / 2, labelWidth, labelHeight, tpText, tpBrushDX);
		}

		private void DrawPriceLabel(float x, float y, float width, float height, string text, SharpDX.Direct2D1.Brush colorBrush)
		{
			var rect = new SharpDX.RectangleF(x, y, width, height);

			// Fully colored background
			RenderTarget.FillRectangle(rect, colorBrush);

			// Text (white on colored background)
			var textRect = new SharpDX.RectangleF(x + 8, y + 4, width - 12, height - 8);
			RenderTarget.DrawText(text, textFormat, textRect, textBrushDX);
		}

		private void DrawInfoPanel()
		{
			float panelWidth = 220;
			float panelHeight = 200;
			float panelX = 15;
			float panelY = 15;
			float padding = 12;
			float lineHeight = 22;

			// Panel background with rounded corners
			var panelRect = new SharpDX.RectangleF(panelX, panelY, panelWidth, panelHeight);
			var roundedRect = new RoundedRectangle
			{
				Rect = panelRect,
				RadiusX = 4,
				RadiusY = 4
			};
			RenderTarget.FillRoundedRectangle(roundedRect, panelBrushDX);
			RenderTarget.DrawRoundedRectangle(roundedRect, textBrushDX, 1);

			float textX = panelX + padding;
			float textW = panelWidth - padding * 2;
			float textY = panelY + padding;

			bool isLong = IsLongSetup();
			string direction = isLong ? "LONG" : "SHORT";
			SharpDX.Direct2D1.Brush dirBrush = isLong ? tpBrushDX : slBrushDX;

			// Title
			DrawPanelTextLine("Risk / Reward Tool", textX, textY, textW, titleTextFormat, textBrushDX);
			textY += lineHeight + 4;

			// Separator line
			RenderTarget.DrawLine(
				new Vector2(textX, textY),
				new Vector2(textX + textW, textY),
				textBrushDX, 0.5f);
			textY += 8;

			// All labels padded to 11 chars (Consolas monospace) for aligned values
			string dirSymbol = isLong ? "▲" : "▼";
			DrawPanelTextLine("Direction: " + dirSymbol + " " + direction, textX, textY, textW, textFormat, dirBrush);
			textY += lineHeight;

			DrawPanelTextLine("Entry:     " + FormatPrice(entryPrice), textX, textY, textW, textFormat, textBrushDX);
			textY += lineHeight;

			DrawPanelTextLine("Risk:      $" + riskDollars.ToString("F2") + "  " + FormatPoints(slPoints) + "pts", textX, textY, textW, textFormat, slBrushDX);
			textY += lineHeight;

			DrawPanelTextLine("Reward:    $" + rewardDollars.ToString("F2") + "  " + FormatPoints(tpPoints) + "pts", textX, textY, textW, textFormat, tpBrushDX);
			textY += lineHeight;

			DrawPanelTextLine("R:R Ratio: 1 : " + rrRatio.ToString("F1"), textX, textY, textW, textFormat, textBrushDX);
			textY += lineHeight;

			DrawPanelTextLine("Contracts: " + contractSize, textX, textY, textW, textFormat, textBrushDX);
			textY += lineHeight;

			string modeValue;
			switch (SelectedRiskMode)
			{
				case RiskMode.FixedCash:
					modeValue = "$" + RiskValue.ToString("F0") + " Fixed";
					break;
				case RiskMode.PercentOfAccount:
					modeValue = RiskValue.ToString("F1") + "% Account";
					break;
				case RiskMode.FixedContracts:
					modeValue = RiskValue.ToString("F0") + " Contracts";
					break;
				default:
					modeValue = "";
					break;
			}
			DrawPanelTextLine("Mode:      " + modeValue, textX, textY, textW, textFormat, textBrushDX);
		}

		private void DrawPanelTextLine(string text, float x, float y, float width, SharpDX.DirectWrite.TextFormat format, SharpDX.Direct2D1.Brush brush)
		{
			var rect = new SharpDX.RectangleF(x, y, width, 20);
			RenderTarget.DrawText(text, format, rect, brush);
		}

		private string FormatPrice(double price)
		{
			if (TickSize < 0.01)
				return price.ToString("F4");
			else if (TickSize < 0.1)
				return price.ToString("F2");
			else
				return price.ToString("F2");
		}

		private string FormatPoints(double points)
		{
			// Determine decimal places needed to represent one tick cleanly
			// e.g. ES tick=0.25 → 2dp, GC tick=0.10 → 1dp, SI tick=0.005 → 3dp
			if (TickSize >= 1)
				return points.ToString("F0");

			int decimals = Math.Max(1, (int)Math.Ceiling(-Math.Log10(TickSize)));
			return points.ToString("F" + decimals);
		}

		#endregion

		#region Context Menu and Order Execution

		private string GetOrderTypeLabel(bool isLong)
		{
			double currentPrice = currentMarketPrice;
			if (isLong)
			{
				if (entryPrice < currentPrice) return "LIMIT";
				if (entryPrice > currentPrice) return "STOP";
				return "MARKET";
			}
			else
			{
				if (entryPrice > currentPrice) return "LIMIT";
				if (entryPrice < currentPrice) return "STOP";
				return "MARKET";
			}
		}

		private void ShowContextMenu()
		{
			var contextMenu = new System.Windows.Controls.ContextMenu();

			bool isLong = IsLongSetup();
			string pendingOrderType = isLong ? GetOrderTypeLabel(true) : GetOrderTypeLabel(false);

			// Pending order at entry price (Limit/Stop) - only show if entry != market
			if (pendingOrderType != "MARKET")
			{
				if (isLong)
				{
					var pendingItem = new System.Windows.Controls.MenuItem
					{
						Header = string.Format("▲  BUY {0}  ({1} ct @ {2})", pendingOrderType, contractSize, FormatPrice(entryPrice))
					};
					pendingItem.Click += (s, ev) => ExecuteOrder(true);
					contextMenu.Items.Add(pendingItem);
				}
				else
				{
					var pendingItem = new System.Windows.Controls.MenuItem
					{
						Header = string.Format("▼  SELL {0}  ({1} ct @ {2})", pendingOrderType, contractSize, FormatPrice(entryPrice))
					};
					pendingItem.Click += (s, ev) => ExecuteOrder(false);
					contextMenu.Items.Add(pendingItem);
				}
			}

			// Market order - always present
			if (isLong)
			{
				var marketItem = new System.Windows.Controls.MenuItem
				{
					Header = string.Format("▲  BUY MARKET  ({0} ct)", contractSize)
				};
				marketItem.Click += (s, ev) => ExecuteMarketOrder(true);
				contextMenu.Items.Add(marketItem);
			}
			else
			{
				var marketItem = new System.Windows.Controls.MenuItem
				{
					Header = string.Format("▼  SELL MARKET  ({0} ct)", contractSize)
				};
				marketItem.Click += (s, ev) => ExecuteMarketOrder(false);
				contextMenu.Items.Add(marketItem);
			}

			contextMenu.Items.Add(new System.Windows.Controls.Separator());

			// Reset levels
			var resetItem = new System.Windows.Controls.MenuItem { Header = "Reset to Current Price" };
			resetItem.Click += (s, ev) => ResetLevels();
			contextMenu.Items.Add(resetItem);

			// Flip direction
			var flipItem = new System.Windows.Controls.MenuItem { Header = "Flip Direction (Swap SL/TP)" };
			flipItem.Click += (s, ev) => FlipDirection();
			contextMenu.Items.Add(flipItem);

			contextMenu.Items.Add(new System.Windows.Controls.Separator());

			// Hide/Show toggle
			if (isToolVisible)
			{
				var hideItem = new System.Windows.Controls.MenuItem { Header = "Hide Tool" };
				hideItem.Click += (s, ev) =>
				{
					isToolVisible = false;
					if (ChartControl != null) ChartControl.InvalidateVisual();
				};
				contextMenu.Items.Add(hideItem);
			}
			else
			{
				var showItem = new System.Windows.Controls.MenuItem { Header = "Show Tool" };
				showItem.Click += (s, ev) =>
				{
					isToolVisible = true;
					if (ChartControl != null) ChartControl.InvalidateVisual();
				};
				contextMenu.Items.Add(showItem);
			}

			contextMenu.IsOpen = true;
		}

		private void ExecuteOrder(bool isLong)
		{
			// Determine order type based on entry price vs current market price
			double currentPrice = currentMarketPrice;
			OrderType orderType;
			string orderTypeLabel;

			if (isLong)
			{
				if (entryPrice < currentPrice)
				{
					// Buy below market = Buy Limit
					orderType = OrderType.Limit;
					orderTypeLabel = "BUY LIMIT";
				}
				else if (entryPrice > currentPrice)
				{
					// Buy above market = Buy Stop Market
					orderType = OrderType.StopMarket;
					orderTypeLabel = "BUY STOP";
				}
				else
				{
					// Buy at market
					orderType = OrderType.Market;
					orderTypeLabel = "BUY MARKET";
				}
			}
			else
			{
				if (entryPrice > currentPrice)
				{
					// Sell above market = Sell Limit
					orderType = OrderType.Limit;
					orderTypeLabel = "SELL LIMIT";
				}
				else if (entryPrice < currentPrice)
				{
					// Sell below market = Sell Stop Market
					orderType = OrderType.StopMarket;
					orderTypeLabel = "SELL STOP";
				}
				else
				{
					// Sell at market
					orderType = OrderType.Market;
					orderTypeLabel = "SELL MARKET";
				}
			}

			if (ShowConfirmation)
			{
				string direction = isLong ? "LONG" : "SHORT";
				string message = string.Format(
					"Execute {0} Order?\n\n" +
					"Order Type: {1}\n" +
					"Instrument: {2}\n" +
					"Contracts: {3}\n" +
					"Entry: {4}\n" +
					"Current Price: {5}\n" +
					"Stop Loss: {6}\n" +
					"Take Profit: {7}\n\n" +
					"Risk: ${8}\n" +
					"Reward: ${9}\n" +
					"R:R: 1:{10}",
					direction,
					orderTypeLabel,
					Instrument.FullName,
					contractSize,
					FormatPrice(entryPrice),
					FormatPrice(currentPrice),
					FormatPrice(slPrice),
					FormatPrice(tpPrice),
					riskDollars.ToString("F2"),
					rewardDollars.ToString("F2"),
					rrRatio.ToString("F1"));

				var result = System.Windows.MessageBox.Show(message, "Confirm Order",
					MessageBoxButton.YesNo, MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;
			}

			ocoId = Guid.NewGuid().ToString();

			try
			{
				double limitPrice = (orderType == OrderType.Limit) ? entryPrice : 0;
				double stopPrice = (orderType == OrderType.StopMarket) ? entryPrice : 0;

				if (isLong)
				{
					entryOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, orderType,
						contractSize, limitPrice, stopPrice, ocoId, "RRT_Entry");
				}
				else
				{
					entryOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, orderType,
						contractSize, limitPrice, stopPrice, ocoId, "RRT_Entry");
				}

				Print(string.Format("RiskRewardTool: {0} order submitted - {1} contracts @ {2}",
					isLong ? "LONG" : "SHORT", contractSize, FormatPrice(entryPrice)));
			}
			catch (Exception ex)
			{
				Print("RiskRewardTool: Order error - " + ex.Message);
				Log("RiskRewardTool: Order error - " + ex.Message, LogLevel.Error);
			}
		}

		private void ExecuteMarketOrder(bool isLong)
		{
			if (ShowConfirmation)
			{
				string direction = isLong ? "LONG" : "SHORT";
				string message = string.Format(
					"Execute {0} MARKET Order?\n\n" +
					"Instrument: {1}\n" +
					"Contracts: {2}\n" +
					"Stop Loss: {3}\n" +
					"Take Profit: {4}\n\n" +
					"Risk: ${5}\n" +
					"Reward: ${6}\n" +
					"R:R: 1:{7}",
					direction,
					Instrument.FullName,
					contractSize,
					FormatPrice(slPrice),
					FormatPrice(tpPrice),
					riskDollars.ToString("F2"),
					rewardDollars.ToString("F2"),
					rrRatio.ToString("F1"));

				var result = System.Windows.MessageBox.Show(message, "Confirm Market Order",
					MessageBoxButton.YesNo, MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return;
			}

			ocoId = Guid.NewGuid().ToString();

			try
			{
				if (isLong)
				{
					entryOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market,
						contractSize, 0, 0, ocoId, "RRT_Entry");
				}
				else
				{
					entryOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market,
						contractSize, 0, 0, ocoId, "RRT_Entry");
				}

				Print(string.Format("RiskRewardTool: {0} MARKET order submitted - {1} contracts",
					isLong ? "LONG" : "SHORT", contractSize));
			}
			catch (Exception ex)
			{
				Print("RiskRewardTool: Market order error - " + ex.Message);
				Log("RiskRewardTool: Market order error - " + ex.Message, LogLevel.Error);
			}
		}

		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
			int quantity, int filled, double averageFillPrice, OrderState orderState,
			DateTime time, ErrorCode error, string comment)
		{
			if (order.Name == "RRT_Entry" && orderState == OrderState.Filled)
			{
				entryOrder = order;
				PlaceOCOBracket(order.IsLong);
			}

			if (error != ErrorCode.NoError)
			{
				Print(string.Format("RiskRewardTool: Order error on {0} - {1}: {2}",
					order.Name, error, comment));
			}
		}

		private void PlaceOCOBracket(bool isLong)
		{
			string bracketOcoId = "RRT_OCO_" + Guid.NewGuid().ToString();

			try
			{
				if (isLong)
				{
					stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket,
						contractSize, 0, slPrice, bracketOcoId, "RRT_StopLoss");

					targetOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit,
						contractSize, tpPrice, 0, bracketOcoId, "RRT_TakeProfit");
				}
				else
				{
					stopOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket,
						contractSize, 0, slPrice, bracketOcoId, "RRT_StopLoss");

					targetOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit,
						contractSize, tpPrice, 0, bracketOcoId, "RRT_TakeProfit");
				}

				Print(string.Format("RiskRewardTool: OCO Bracket placed - SL: {0}, TP: {1}",
					FormatPrice(slPrice), FormatPrice(tpPrice)));
			}
			catch (Exception ex)
			{
				Print("RiskRewardTool: Bracket error - " + ex.Message);
				Log("RiskRewardTool: Bracket error - " + ex.Message, LogLevel.Error);
			}
		}

		#endregion

		#region Utility Methods

		private void ResetLevels()
		{
			if (currentMarketPrice <= 0) return;

			entryPrice = currentMarketPrice;
			slPrice = entryPrice - (DefaultSLTicks * TickSize);
			tpPrice = entryPrice + (DefaultTPTicks * TickSize);

			RecalculateValues();

			if (ChartControl != null)
				ChartControl.InvalidateVisual();
		}

		private void FlipDirection()
		{
			double slDistance = Math.Abs(entryPrice - slPrice);
			double tpDistance = Math.Abs(tpPrice - entryPrice);

			if (IsLongSetup())
			{
				// Flip to short: SL above, TP below
				slPrice = entryPrice + slDistance;
				tpPrice = entryPrice - tpDistance;
			}
			else
			{
				// Flip to long: SL below, TP above
				slPrice = entryPrice - slDistance;
				tpPrice = entryPrice + tpDistance;
			}

			slPrice = Instrument.MasterInstrument.RoundToTickSize(slPrice);
			tpPrice = Instrument.MasterInstrument.RoundToTickSize(tpPrice);

			RecalculateValues();

			if (ChartControl != null)
				ChartControl.InvalidateVisual();
		}

		#endregion
	}
}
