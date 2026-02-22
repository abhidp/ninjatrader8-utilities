# RiskRewardTool for NinjaTrader 8

A visual risk/reward planning tool for NinjaTrader 8, inspired by TradingView's risk/reward drawing tool. Plan trades visually with draggable entry, stop-loss, and take-profit levels, automatic position sizing, and one-click order execution with OCO brackets.

## Features

- **Visual Trade Planning** - Draggable Entry, Stop Loss, and Take Profit lines rendered directly on the chart
- **TradingView-Style Box** - Bounded risk/reward zones with color-coded shading (red for risk, green for reward)
- **Automatic Position Sizing** - Calculates contract size based on your chosen risk mode
- **Smart Order Types** - Automatically selects Limit, Stop, or Market orders based on entry price vs current market
- **OCO Bracket Orders** - Stop loss and take profit are placed as an OCO pair that auto-cancels the other on fill
- **Chart-Anchored** - The RR box scrolls with the chart, just like native drawing tools
- **Info Panel** - Live dashboard showing direction, risk, reward, R:R ratio, contracts, and risk mode
- **Works on Any Instrument** - Futures, forex, stocks, crypto - auto-detects tick size and point value

## Installation

1. Copy `RiskRewardTool.cs` into your NinjaTrader custom strategies folder:
   ```
   Documents\NinjaTrader 8\bin\Custom\Strategies\
   ```
2. In NinjaTrader, open the **NinjaScript Editor** (New > NinjaScript Editor)
3. Press **F5** or click **Compile** to compile all scripts
4. The strategy should compile without errors

## Getting Started

1. Open a chart for any instrument
2. Right-click the chart > **Strategies** > **RiskRewardTool** > **Configure**
3. Set your risk parameters (see [Configuration](#configuration) below)
4. Check **Enabled** and click **OK**
5. The RR tool will appear on the chart with Entry (blue), Stop Loss (red), and Take Profit (green) lines

## How to Use

### Dragging Lines

| Action | How |
|--------|-----|
| Move Entry line | Left-click and drag the **blue** line or its handle |
| Move Stop Loss | Left-click and drag the **red** line or its handle |
| Move Take Profit | Left-click and drag the **green** line or its handle |
| Move entire box | Left-click and drag inside the shaded zone area |
| Resize box width | Left-click and drag the left or right vertical edge of the box |

- **Entry line** moves independently - dragging it does not move SL/TP
- **Box move** shifts all three levels together, preserving the shape
- All price levels snap to valid tick increments

### Placing Orders

**Right-click** on the RR box (the shaded zone area) or the info panel (top-left corner) to open the order menu.

The menu shows:

- **Pending order** (Limit or Stop) at the entry price level - only shown when entry differs from market price
  - **Buy Limit** - when entry is below current market (long setup)
  - **Buy Stop** - when entry is above current market (long setup)
  - **Sell Limit** - when entry is above current market (short setup)
  - **Sell Stop** - when entry is below current market (short setup)
- **Market order** - always available, executes immediately at market price
- **Reset to Current Price** - resets entry/SL/TP to defaults around the current price
- **Flip Direction** - swaps SL and TP to switch between long and short setups
- **Hide/Show Tool** - toggles the visual overlay

Right-clicking **anywhere else** on the chart passes through to NinjaTrader's native context menu as normal.

### Direction Detection

The tool automatically detects long vs short based on your TP placement:
- **TP above entry** = Long setup (green arrow in info panel)
- **TP below entry** = Short setup (red arrow in info panel)

Use **Flip Direction** from the right-click menu to quickly switch.

### Reading the Info Panel

The info panel in the top-left corner shows:

| Field | Description |
|-------|-------------|
| Direction | Long or Short, with arrow indicator |
| Entry | The planned entry price |
| Risk | Dollar risk amount + points distance to SL |
| Reward | Dollar reward amount + points distance to TP |
| R:R Ratio | Risk-to-reward ratio (e.g., 1:2.5) |
| Contracts | Auto-calculated position size |
| Mode | Current risk mode and value |

### Reading the Price Labels

On the right side of the chart, each line has a colored label showing:
- **Entry label** - Price + contract count
- **SL label** - Price + points distance + dollar risk
- **TP label** - Price + points distance + dollar reward

## Configuration

### Risk Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| Risk Mode | Percent of Account | How position size is calculated |
| Risk Value | 1.0 | The risk amount (meaning depends on mode) |
| Default SL Ticks | 50 | Initial stop loss distance from entry |
| Default TP Ticks | 100 | Initial take profit distance from entry |

### Risk Modes

| Mode | Risk Value Meaning | Example |
|------|--------------------|---------|
| **Percent of Account** | % of account balance to risk per trade | 1.0 = risk 1% of account |
| **Fixed Cash** | Dollar amount to risk per trade | 500 = risk $500 max |
| **Fixed Contracts** | Always use this many contracts | 2 = always trade 2 contracts |

### Order Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| Show Confirmation | True | Show a confirmation dialog before executing orders |

### Visual Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| Entry Line Color | DodgerBlue | Color of the entry line and label |
| Stop Loss Color | Crimson | Color of the SL line, zone, and label |
| Take Profit Color | LimeGreen | Color of the TP line, zone, and label |
| Line Width | 2 | Thickness of the horizontal lines |
| Zone Opacity % | 20 | Transparency of the risk/reward shaded zones (5-80) |
| Text Color | White | Color of text in labels and info panel |

## Order Execution Details

- Uses **Unmanaged order approach** for full control over OCO brackets
- Entry orders are submitted first; SL/TP bracket is placed **on fill** (not before)
- SL and TP are linked as an **OCO pair** - when one fills, the other is automatically cancelled
- For pending orders (Limit/Stop), the SL/TP levels are **snapshotted** at submission time, so moving the lines after placing an order does not affect the pending bracket
- If **Show Confirmation** is enabled, a dialog shows all order details before execution

## Tips

- Start with the **Sim101** account to practice before using real money
- The position size updates in real-time as you drag the SL line closer or further
- Use wider SL for volatile instruments (smaller position) and tighter SL for calmer ones
- The R:R ratio updates live - aim for at least 1:2 for most setups
- You can resize the box width by dragging its vertical edges to make it wider or narrower
- The box scrolls with the chart so it stays anchored to the price bars where you placed it

## Technical Details

- **Platform**: NinjaTrader 8.1.x (64-bit)
- **Language**: C# / NinjaScript
- **Type**: Strategy (renders on chart AND can execute orders)
- **Rendering**: SharpDX (DirectX) for high-performance chart overlay
- **Order Management**: Unmanaged approach with OCO bracket support
- **Calculation**: OnEachTick for real-time price updates

## License

This project is for personal trading use.
