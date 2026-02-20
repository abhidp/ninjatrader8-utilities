# NinjaTrader 8 Risk/Reward Trading Tool - Strategy Development Prompt

## Project Overview

Build a professional **NinjaTrader 8 Strategy** called "RiskRewardTool" that replicates TradingView's risk/reward drawing tool functionality. This strategy allows traders to visually plan trades with draggable entry, stop-loss, and take-profit levels while automatically calculating position sizing based on risk parameters. **Right-click to execute orders** with automatic OCO bracket creation.

## IMPORTANT: Technical Foundation

- **Platform:** NinjaTrader 8.1.x (64-bit)
- **Language:** C#
- **Type:** Strategy (NOT Indicator or Drawing Tool — strategies can render AND trade)
- **Rendering:** SharpDX (DirectX wrapper for high-performance chart rendering)
- **Order Management:** Unmanaged approach for full OCO control
- **Namespace:** NinjaTrader.NinjaScript.Strategies

## Core Requirements

### 1. Visual Components

Render three draggable horizontal lines using SharpDX:
- **Entry Line** (Blue, DodgerBlue) - The planned entry price
- **Stop Loss Line** (Red, Crimson) - Below entry for longs, above for shorts
- **Take Profit Line** (Green, LimeGreen) - Above entry for longs, below for shorts

Render two semi-transparent rectangle zones:
- **Risk Zone** (Red, 20% opacity) - Between Entry and Stop Loss
- **Reward Zone** (Green, 20% opacity) - Between Entry and Take Profit

Render price labels on the right side of each line:
- Price value
- Distance in ticks from entry
- Dollar risk/reward value

### 2. Interaction Model

```
PLACEMENT PHASE:
1. Strategy loads → Entry line appears at current price
2. SL and TP lines appear at default distances (based on ATR or fixed ticks)
3. User can immediately drag any line

DRAG INTERACTION:
- Click near a line (within hit-test threshold ~10 pixels)
- Drag to new price level
- Release to set
- All calculations update in real-time during drag

EXECUTION:
- Right-click on chart → Custom context menu appears
- Select "Execute Long" or "Execute Short"
- Order placed with automatic OCO bracket
```

### 3. Risk Calculation Modes

```csharp
public enum RiskMode
{
    FixedCash,          // Fixed dollar amount
    PercentOfAccount,   // % of account balance
    FixedContracts      // Fixed number of contracts
}
```

### 4. Input Parameters

```csharp
#region Parameters

[NinjaScriptProperty]
[Display(Name = "Risk Mode", Description = "How to calculate position size", Order = 1, GroupName = "Risk Settings")]
public RiskMode SelectedRiskMode { get; set; }

[NinjaScriptProperty]
[Range(1, double.MaxValue)]
[Display(Name = "Risk Value", Description = "Dollar amount, percentage, or contracts", Order = 2, GroupName = "Risk Settings")]
public double RiskValue { get; set; }

[NinjaScriptProperty]
[Range(1, 1000)]
[Display(Name = "Default SL Ticks", Description = "Default stop loss distance in ticks", Order = 3, GroupName = "Risk Settings")]
public int DefaultSLTicks { get; set; }

[NinjaScriptProperty]
[Range(1, 1000)]
[Display(Name = "Default TP Ticks", Description = "Default take profit distance in ticks", Order = 4, GroupName = "Risk Settings")]
public int DefaultTPTicks { get; set; }

[NinjaScriptProperty]
[Display(Name = "Show Confirmation", Description = "Show confirmation dialog before executing", Order = 5, GroupName = "Order Settings")]
public bool ShowConfirmation { get; set; }

[NinjaScriptProperty]
[Display(Name = "Entry Line Color", Order = 1, GroupName = "Visual Settings")]
public Brush EntryLineBrush { get; set; }

[NinjaScriptProperty]
[Display(Name = "Stop Loss Color", Order = 2, GroupName = "Visual Settings")]
public Brush StopLossLineBrush { get; set; }

[NinjaScriptProperty]
[Display(Name = "Take Profit Color", Order = 3, GroupName = "Visual Settings")]
public Brush TakeProfitLineBrush { get; set; }

[NinjaScriptProperty]
[Range(1, 5)]
[Display(Name = "Line Width", Order = 4, GroupName = "Visual Settings")]
public int LineWidth { get; set; }

[NinjaScriptProperty]
[Range(10, 100)]
[Display(Name = "Zone Opacity %", Order = 5, GroupName = "Visual Settings")]
public int ZoneOpacity { get; set; }

[NinjaScriptProperty]
[Display(Name = "Panel Background", Order = 6, GroupName = "Visual Settings")]
public Brush PanelBackgroundBrush { get; set; }

[NinjaScriptProperty]
[Display(Name = "Text Color", Order = 7, GroupName = "Visual Settings")]
public Brush TextBrush { get; set; }

#endregion
```

### 5. State Management

```csharp
#region Variables

// Price levels
private double entryPrice;
private double slPrice;
private double tpPrice;

// Dragging state
private bool isDragging;
private DragTarget currentDragTarget;
private enum DragTarget { None, Entry, StopLoss, TakeProfit }

// SharpDX resources (must be created/disposed properly)
private SharpDX.Direct2D1.Brush entryBrushDX;
private SharpDX.Direct2D1.Brush slBrushDX;
private SharpDX.Direct2D1.Brush tpBrushDX;
private SharpDX.Direct2D1.Brush riskZoneBrushDX;
private SharpDX.Direct2D1.Brush rewardZoneBrushDX;
private SharpDX.Direct2D1.Brush panelBrushDX;
private SharpDX.Direct2D1.Brush textBrushDX;
private SharpDX.DirectWrite.TextFormat textFormat;

// Calculated values (updated on every price change)
private int contractSize;
private double riskDollars;
private double rewardDollars;
private double rrRatio;
private int slTicks;
private int tpTicks;

// Order tracking
private Order entryOrder;
private Order stopOrder;
private Order targetOrder;
private string ocoId;

#endregion
```

### 6. Initialization

```csharp
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
        IsChartOnly = true;
        
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
        PanelBackgroundBrush = Brushes.Black;
        TextBrush = Brushes.White;
    }
    else if (State == State.Configure)
    {
        // Use unmanaged order handling for OCO support
        IsUnmanaged = true;
    }
    else if (State == State.DataLoaded)
    {
        // Initialize price levels
        entryPrice = Close[0];
        slPrice = entryPrice - (DefaultSLTicks * TickSize);
        tpPrice = entryPrice + (DefaultTPTicks * TickSize);
        
        // Subscribe to mouse events
        if (ChartControl != null)
        {
            ChartControl.MouseDown += OnMouseDown;
            ChartControl.MouseMove += OnMouseMove;
            ChartControl.MouseUp += OnMouseUp;
        }
    }
    else if (State == State.Historical)
    {
        // Skip historical processing
    }
    else if (State == State.Terminated)
    {
        // Cleanup mouse event subscriptions
        if (ChartControl != null)
        {
            ChartControl.MouseDown -= OnMouseDown;
            ChartControl.MouseMove -= OnMouseMove;
            ChartControl.MouseUp -= OnMouseUp;
        }
        
        // Dispose SharpDX resources
        DisposeSharpDXResources();
    }
}
```

### 7. Mouse Event Handling

```csharp
private const int HitTestThreshold = 10; // pixels

private void OnMouseDown(object sender, MouseEventArgs e)
{
    if (e.Button != MouseButtons.Left) return;
    
    // Convert mouse Y to price
    double mousePrice = ChartControl.Instrument.MasterInstrument.RoundToTickSize(
        ChartControl.PanelIndex == 0 
            ? ChartingExtensions.ConvertToVerticalPixels(e.Y, ChartControl.PanelHeight, ChartControl.MinValue, ChartControl.MaxValue)
            : 0
    );
    
    // Alternative: Use ChartScale for conversion
    int mouseY = e.Y;
    
    // Check hit test for each line
    int entryY = ChartScale.GetYByValue(entryPrice);
    int slY = ChartScale.GetYByValue(slPrice);
    int tpY = ChartScale.GetYByValue(tpPrice);
    
    if (Math.Abs(mouseY - entryY) <= HitTestThreshold)
    {
        isDragging = true;
        currentDragTarget = DragTarget.Entry;
    }
    else if (Math.Abs(mouseY - slY) <= HitTestThreshold)
    {
        isDragging = true;
        currentDragTarget = DragTarget.StopLoss;
    }
    else if (Math.Abs(mouseY - tpY) <= HitTestThreshold)
    {
        isDragging = true;
        currentDragTarget = DragTarget.TakeProfit;
    }
}

private void OnMouseMove(object sender, MouseEventArgs e)
{
    if (!isDragging) return;
    
    // Convert Y pixel to price
    double newPrice = ChartScale.GetValueByY(e.Y);
    newPrice = Instrument.MasterInstrument.RoundToTickSize(newPrice);
    
    // Update the appropriate price level
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
    
    // Recalculate everything
    RecalculateValues();
    
    // Force redraw
    ChartControl.InvalidateVisual();
}

private void OnMouseUp(object sender, MouseEventArgs e)
{
    isDragging = false;
    currentDragTarget = DragTarget.None;
}
```

### 8. Calculation Functions

```csharp
private void RecalculateValues()
{
    // Calculate tick distances
    slTicks = (int)Math.Round(Math.Abs(entryPrice - slPrice) / TickSize);
    tpTicks = (int)Math.Round(Math.Abs(tpPrice - entryPrice) / TickSize);
    
    // Determine if long or short
    bool isLong = tpPrice > entryPrice;
    
    // Calculate position size based on risk mode
    contractSize = CalculateContractSize();
    
    // Calculate dollar values
    double tickValue = Instrument.MasterInstrument.PointValue * TickSize;
    riskDollars = slTicks * tickValue * contractSize;
    rewardDollars = tpTicks * tickValue * contractSize;
    
    // Calculate R:R ratio
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
            double accountRisk = Account.Get(AccountItem.CashValue, Currency.UsDollar) * (RiskValue / 100.0);
            return Math.Max(1, (int)Math.Floor(accountRisk / slValue));
            
        case RiskMode.FixedContracts:
            return (int)RiskValue;
            
        default:
            return 1;
    }
}

private bool IsLongSetup()
{
    return tpPrice > entryPrice;
}
```

### 9. SharpDX Rendering

```csharp
public override void OnRenderTargetChanged()
{
    // Dispose old resources
    DisposeSharpDXResources();
    
    // Create new SharpDX resources for the new render target
    if (RenderTarget != null)
    {
        entryBrushDX = EntryLineBrush.ToDxBrush(RenderTarget);
        slBrushDX = StopLossLineBrush.ToDxBrush(RenderTarget);
        tpBrushDX = TakeProfitLineBrush.ToDxBrush(RenderTarget);
        
        // Create semi-transparent zone brushes
        var riskColor = ((SolidColorBrush)StopLossLineBrush).Color;
        riskZoneBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, 
            new SharpDX.Color(riskColor.R, riskColor.G, riskColor.B, (byte)(255 * ZoneOpacity / 100)));
        
        var rewardColor = ((SolidColorBrush)TakeProfitLineBrush).Color;
        rewardZoneBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
            new SharpDX.Color(rewardColor.R, rewardColor.G, rewardColor.B, (byte)(255 * ZoneOpacity / 100)));
        
        panelBrushDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
            new SharpDX.Color(40, 40, 40, 230));
        
        textBrushDX = TextBrush.ToDxBrush(RenderTarget);
        
        // Create text format
        textFormat = new SharpDX.DirectWrite.TextFormat(
            Core.Globals.DirectWriteFactory,
            "Arial",
            SharpDX.DirectWrite.FontWeight.Normal,
            SharpDX.DirectWrite.FontStyle.Normal,
            12);
    }
}

private void DisposeSharpDXResources()
{
    entryBrushDX?.Dispose();
    slBrushDX?.Dispose();
    tpBrushDX?.Dispose();
    riskZoneBrushDX?.Dispose();
    rewardZoneBrushDX?.Dispose();
    panelBrushDX?.Dispose();
    textBrushDX?.Dispose();
    textFormat?.Dispose();
}

protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
{
    base.OnRender(chartControl, chartScale);
    
    if (RenderTarget == null) return;
    
    // Get chart dimensions
    float chartWidth = (float)chartControl.ActualWidth;
    float chartHeight = (float)chartControl.ActualHeight;
    
    // Convert prices to Y coordinates
    float entryY = chartScale.GetYByValue(entryPrice);
    float slY = chartScale.GetYByValue(slPrice);
    float tpY = chartScale.GetYByValue(tpPrice);
    
    // Draw zones (rectangles)
    DrawZones(chartWidth, entryY, slY, tpY);
    
    // Draw lines
    DrawLine(0, entryY, chartWidth - 150, entryY, entryBrushDX, LineWidth);
    DrawLine(0, slY, chartWidth - 150, slY, slBrushDX, LineWidth);
    DrawLine(0, tpY, chartWidth - 150, tpY, tpBrushDX, LineWidth);
    
    // Draw price labels on the right
    DrawPriceLabels(chartWidth, entryY, slY, tpY);
    
    // Draw info panel
    DrawInfoPanel(chartControl);
}

private void DrawZones(float chartWidth, float entryY, float slY, float tpY)
{
    // Risk zone (entry to SL)
    var riskRect = new SharpDX.RectangleF(0, Math.Min(entryY, slY), chartWidth - 150, Math.Abs(slY - entryY));
    RenderTarget.FillRectangle(riskRect, riskZoneBrushDX);
    
    // Reward zone (entry to TP)
    var rewardRect = new SharpDX.RectangleF(0, Math.Min(entryY, tpY), chartWidth - 150, Math.Abs(tpY - entryY));
    RenderTarget.FillRectangle(rewardRect, rewardZoneBrushDX);
}

private void DrawLine(float x1, float y1, float x2, float y2, SharpDX.Direct2D1.Brush brush, float strokeWidth)
{
    RenderTarget.DrawLine(
        new SharpDX.Vector2(x1, y1),
        new SharpDX.Vector2(x2, y2),
        brush,
        strokeWidth);
}

private void DrawPriceLabels(float chartWidth, float entryY, float slY, float tpY)
{
    float labelX = chartWidth - 145;
    float labelWidth = 140;
    float labelHeight = 20;
    
    // Entry label
    string entryText = $"Entry: {entryPrice:F2}";
    DrawLabel(labelX, entryY - labelHeight/2, labelWidth, labelHeight, entryText, entryBrushDX);
    
    // SL label
    string slText = $"SL: {slPrice:F2} ({slTicks}t) -${riskDollars:F0}";
    DrawLabel(labelX, slY - labelHeight/2, labelWidth, labelHeight, slText, slBrushDX);
    
    // TP label
    string tpText = $"TP: {tpPrice:F2} ({tpTicks}t) +${rewardDollars:F0}";
    DrawLabel(labelX, tpY - labelHeight/2, labelWidth, labelHeight, tpText, tpBrushDX);
}

private void DrawLabel(float x, float y, float width, float height, string text, SharpDX.Direct2D1.Brush brush)
{
    var rect = new SharpDX.RectangleF(x, y, width, height);
    
    // Background
    RenderTarget.FillRectangle(rect, panelBrushDX);
    RenderTarget.DrawRectangle(rect, brush, 1);
    
    // Text
    var textRect = new SharpDX.RectangleF(x + 5, y + 2, width - 10, height - 4);
    RenderTarget.DrawText(text, textFormat, textRect, textBrushDX);
}

private void DrawInfoPanel(ChartControl chartControl)
{
    float panelWidth = 200;
    float panelHeight = 180;
    float panelX = 20;
    float panelY = 50;
    
    // Panel background
    var panelRect = new SharpDX.RectangleF(panelX, panelY, panelWidth, panelHeight);
    RenderTarget.FillRectangle(panelRect, panelBrushDX);
    RenderTarget.DrawRectangle(panelRect, textBrushDX, 1);
    
    // Title
    float textY = panelY + 10;
    float lineHeight = 22;
    
    bool isLong = IsLongSetup();
    string direction = isLong ? "▲ LONG" : "▼ SHORT";
    
    DrawPanelText($"Risk/Reward Tool", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"Direction: {direction}", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"Entry: {entryPrice:F2}", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"Risk: ${riskDollars:F2} ({slTicks} ticks)", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"Reward: ${rewardDollars:F2} ({tpTicks} ticks)", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"R:R Ratio: 1 : {rrRatio:F1}", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    DrawPanelText($"Contracts: {contractSize}", panelX + 10, textY, panelWidth - 20);
    textY += lineHeight;
    
    // Mode info
    string modeText = SelectedRiskMode switch
    {
        RiskMode.FixedCash => $"Mode: ${RiskValue:F0} Fixed",
        RiskMode.PercentOfAccount => $"Mode: {RiskValue:F1}% Account",
        RiskMode.FixedContracts => $"Mode: {RiskValue:F0} Contracts",
        _ => ""
    };
    DrawPanelText(modeText, panelX + 10, textY, panelWidth - 20);
}

private void DrawPanelText(string text, float x, float y, float width)
{
    var textRect = new SharpDX.RectangleF(x, y, width, 20);
    RenderTarget.DrawText(text, textFormat, textRect, textBrushDX);
}
```

### 10. Context Menu for Order Execution

```csharp
protected override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
{
    // Handle right-click for context menu
    if (System.Windows.Forms.Control.MouseButtons == MouseButtons.Right)
    {
        ShowContextMenu();
    }
}

private void ShowContextMenu()
{
    // Create context menu
    var contextMenu = new System.Windows.Controls.ContextMenu();
    
    // Execute Long option
    var executeLong = new System.Windows.Controls.MenuItem { Header = "Execute LONG Order" };
    executeLong.Click += (s, e) => ExecuteOrder(true);
    contextMenu.Items.Add(executeLong);
    
    // Execute Short option
    var executeShort = new System.Windows.Controls.MenuItem { Header = "Execute SHORT Order" };
    executeShort.Click += (s, e) => ExecuteOrder(false);
    contextMenu.Items.Add(executeShort);
    
    // Separator
    contextMenu.Items.Add(new System.Windows.Controls.Separator());
    
    // Reset levels
    var resetLevels = new System.Windows.Controls.MenuItem { Header = "Reset to Current Price" };
    resetLevels.Click += (s, e) => ResetLevels();
    contextMenu.Items.Add(resetLevels);
    
    // Show menu
    contextMenu.IsOpen = true;
}
```

### 11. Order Execution with OCO

```csharp
private void ExecuteOrder(bool isLong)
{
    // Confirmation dialog
    if (ShowConfirmation)
    {
        string direction = isLong ? "LONG" : "SHORT";
        string message = $"Execute {direction} Order?\n\n" +
                        $"Instrument: {Instrument.FullName}\n" +
                        $"Contracts: {contractSize}\n" +
                        $"Entry: {entryPrice:F2}\n" +
                        $"Stop Loss: {slPrice:F2}\n" +
                        $"Take Profit: {tpPrice:F2}\n\n" +
                        $"Risk: ${riskDollars:F2}\n" +
                        $"Reward: ${rewardDollars:F2}\n" +
                        $"R:R: 1:{rrRatio:F1}";
        
        var result = System.Windows.MessageBox.Show(message, "Confirm Order", 
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        
        if (result != System.Windows.MessageBoxResult.Yes)
            return;
    }
    
    // Generate unique OCO ID
    ocoId = Guid.NewGuid().ToString();
    
    try
    {
        if (isLong)
        {
            // Enter long at market
            entryOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contractSize, 0, 0, ocoId, "Entry");
        }
        else
        {
            // Enter short at market
            entryOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, contractSize, 0, 0, ocoId, "Entry");
        }
    }
    catch (Exception ex)
    {
        Print($"Order error: {ex.Message}");
        Log($"Order error: {ex.Message}", LogLevel.Error);
    }
}

protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, 
    double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
{
    // When entry order fills, place OCO bracket
    if (order == entryOrder && orderState == OrderState.Filled)
    {
        PlaceOCOBracket(order.IsLong);
    }
}

private void PlaceOCOBracket(bool isLong)
{
    string bracketOcoId = "OCO_" + Guid.NewGuid().ToString();
    
    if (isLong)
    {
        // Long position: Sell Stop for SL, Sell Limit for TP
        stopOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, contractSize, 
            0, slPrice, bracketOcoId, "StopLoss");
        
        targetOrder = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, contractSize, 
            tpPrice, 0, bracketOcoId, "TakeProfit");
    }
    else
    {
        // Short position: Buy Stop for SL, Buy Limit for TP
        stopOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contractSize, 
            0, slPrice, bracketOcoId, "StopLoss");
        
        targetOrder = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contractSize, 
            tpPrice, 0, bracketOcoId, "TakeProfit");
    }
    
    Print($"OCO Bracket placed - SL: {slPrice:F2}, TP: {tpPrice:F2}");
}
```

### 12. Helper Functions

```csharp
private void ResetLevels()
{
    entryPrice = Close[0];
    slPrice = entryPrice - (DefaultSLTicks * TickSize);
    tpPrice = entryPrice + (DefaultTPTicks * TickSize);
    
    RecalculateValues();
    ChartControl.InvalidateVisual();
}

protected override void OnBarUpdate()
{
    // Only process real-time data
    if (State != State.Realtime) return;
    
    // Recalculate on each tick to keep values current
    RecalculateValues();
}
```

### 13. File Structure

Create a single file: `RiskRewardTool.cs`

Location: `Documents\NinjaTrader 8\bin\Custom\Strategies\RiskRewardTool.cs`

```
RiskRewardTool.cs
├── using statements
├── namespace NinjaTrader.NinjaScript.Strategies
├── public class RiskRewardTool : Strategy
│   ├── #region Variables
│   ├── #region Parameters
│   ├── OnStateChange()
│   ├── OnBarUpdate()
│   ├── Mouse Event Handlers
│   │   ├── OnMouseDown()
│   │   ├── OnMouseMove()
│   │   └── OnMouseUp()
│   ├── Calculation Methods
│   │   ├── RecalculateValues()
│   │   ├── CalculateContractSize()
│   │   └── IsLongSetup()
│   ├── Rendering Methods
│   │   ├── OnRenderTargetChanged()
│   │   ├── OnRender()
│   │   ├── DrawZones()
│   │   ├── DrawLine()
│   │   ├── DrawPriceLabels()
│   │   ├── DrawLabel()
│   │   ├── DrawInfoPanel()
│   │   └── DrawPanelText()
│   ├── Order Execution
│   │   ├── ShowContextMenu()
│   │   ├── ExecuteOrder()
│   │   ├── OnOrderUpdate()
│   │   └── PlaceOCOBracket()
│   ├── Utility Methods
│   │   ├── ResetLevels()
│   │   └── DisposeSharpDXResources()
│   └── #region Properties (auto-generated by NT)
```

### 14. Required Using Statements

```csharp
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
#endregion
```

### 15. Important Implementation Notes

1. **Unmanaged Orders Required:**
   - Set `IsUnmanaged = true` in `State.Configure`
   - Use `SubmitOrderUnmanaged()` for OCO support
   - Track orders manually via `OnOrderUpdate()`

2. **SharpDX Resource Management:**
   - Create resources in `OnRenderTargetChanged()`
   - Dispose in `OnStateChange()` when `State == State.Terminated`
   - Never create SharpDX resources in `OnRender()` (memory leak)

3. **Mouse Event Threading:**
   - Chart events run on UI thread
   - Order submission should be safe but consider `TriggerCustomEvent()` for complex logic

4. **Price Rounding:**
   - Always use `Instrument.MasterInstrument.RoundToTickSize(price)`
   - Prevents invalid price errors

5. **Account Access:**
   - Use `Account.Get(AccountItem.CashValue, Currency.UsDollar)` for balance
   - Works for both sim and live accounts

6. **Context Menu:**
   - WPF ContextMenu works in NT8
   - Can alternatively use Windows Forms ContextMenuStrip

### 16. Edge Cases to Handle

1. **No position on chart** — Only show visual tool, no execution available
2. **Market closed** — Warn user, disable market orders
3. **Insufficient margin** — Check before execution
4. **Invalid tick size** — Validate SL/TP distances
5. **Contract size of 0** — Default to 1, show warning
6. **SL beyond TP** — Auto-detect direction, swap if needed
7. **Strategy disabled** — Clean up pending orders

### 17. Testing Checklist

1. [ ] Lines render correctly on chart
2. [ ] Dragging lines updates prices in real-time
3. [ ] Risk/reward calculations are accurate
4. [ ] Panel displays correct information
5. [ ] Right-click menu appears
6. [ ] Long order executes with correct SL/TP
7. [ ] Short order executes with correct SL/TP
8. [ ] OCO cancels other order when one fills
9. [ ] Works on different instruments (ES, NQ, CL, etc.)
10. [ ] Works in simulation account
11. [ ] Works in replay mode (for testing)

## Quality Requirements

1. **Compiles without errors or warnings** in NinjaTrader
2. **Clean, readable C# code** following .NET conventions
3. **Proper disposal** of all SharpDX resources
4. **Thread-safe** operations where needed
5. **Works on any futures instrument**
6. **Works in simulation and live accounts**

## Deliverable

Generate the complete, compilable `RiskRewardTool.cs` file that can be placed in the NinjaTrader Strategies folder. The strategy should be fully functional with:
- Visual risk/reward overlay
- Draggable price lines
- Real-time calculations
- Right-click order execution
- Automatic OCO bracket creation

After the initial version, I will compile in NinjaTrader, test on a simulation account, and provide feedback for iterations.
