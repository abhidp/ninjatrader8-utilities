; === NinjaTrader 8 Chart Enhancements (AutoHotkey v2.0) ===
; Makes NinjaTrader charts feel like TradingView
;
; Features:
;   1. Scroll wheel to zoom in/out
;   2. Left-drag to pan (auto Ctrl key)
;   3. Double-click to auto-scale/reset
;   4. Shift+scroll to pan horizontally through time
;   5. Scroll on price scale (right edge) to zoom vertically only
;   6. Middle-click to activate Ruler/measurement tool
;
; Converted from v1.1 to v2.0 and enhanced
; =====================================================

#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent

; --- Script Performance Settings ---
ProcessSetPriority "High"
A_MaxHotkeysPerInterval := 200

; --- Global Variables ---
global isCtrlSent := false
global isLButtonDown := false
global StartX := 0
global StartY := 0

; --- Vertical Zoom State ---
global isVerticalDragging := false
global verticalDragStartX := 0
global verticalDragStartY := 0

; --- Configuration ---
global PRICE_SCALE_WIDTH := 80   ; Width in pixels of the price scale area on the right
global VERTICAL_ZOOM_PIXELS := 50 ; How many pixels to drag per scroll tick for vertical zoom
global SCROLL_RELEASE_DELAY := 300 ; Milliseconds to wait before releasing drag

; --- Helper Function: Check if on a Chart window ---
IsChartWindow() {
    try {
        title := WinGetTitle("A")
        return InStr(title, "Chart")
    } catch {
        return false
    }
}

; --- Helper Function: Check if mouse is over the price scale (right edge) ---
IsOverPriceScale() {
    try {
        ; Get mouse position relative to the active window
        MouseGetPos(&mouseX, &mouseY, &winId)

        ; Get the window's client area dimensions
        WinGetPos(&winX, &winY, &winWidth, &winHeight, winId)

        ; Check if mouse is in the rightmost PRICE_SCALE_WIDTH pixels
        return (mouseX > (winWidth - PRICE_SCALE_WIDTH))
    } catch {
        return false
    }
}

; --- Drag Detection Function ---
CheckForDrag() {
    global isLButtonDown, isCtrlSent, StartX, StartY

    if (!isLButtonDown) {
        SetTimer(CheckForDrag, 0)
        return
    }

    if (isCtrlSent)
        return

    ; Check if mouse has moved enough to be a drag
    MouseGetPos(&CurrentX, &CurrentY)
    DistanceX := Abs(CurrentX - StartX)
    DistanceY := Abs(CurrentY - StartY)

    if (DistanceX > 3 || DistanceY > 3) {
        if (IsChartWindow()) {
            Send "{Control Down}"
            isCtrlSent := true
            SetTimer(CheckForDrag, 0)
        }
    }
}

; === GLOBAL HOTKEY: Mouse Button Release ===
; Must be global to catch release even outside NinjaTrader window
; This prevents the "stuck Ctrl key" bug
~LButton Up:: {
    global isLButtonDown, isCtrlSent

    isLButtonDown := false
    SetTimer(CheckForDrag, 0)

    if (isCtrlSent) {
        Send "{Control Up}"
        isCtrlSent := false
    }
}

; === CONTEXT-SENSITIVE HOTKEYS ===
; These only activate when NinjaTrader is the active window
#HotIf WinActive("ahk_exe NinjaTrader.exe")

; --- Scroll Wheel to Zoom (Inverted to match TradingView feel) ---
WheelUp:: {
    if (IsChartWindow() && IsOverPriceScale()) {
        ; On price scale: simulate drag to zoom vertically
        SimulateVerticalDrag(-VERTICAL_ZOOM_PIXELS)  ; Drag up = zoom out vertically
    } else {
        Send "^{Down}"   ; Ctrl+Down = Regular Zoom Out
    }
}

WheelDown:: {
    if (IsChartWindow() && IsOverPriceScale()) {
        ; On price scale: simulate drag to zoom vertically
        SimulateVerticalDrag(VERTICAL_ZOOM_PIXELS)   ; Drag down = zoom in vertically
    } else {
        Send "^{Up}"     ; Ctrl+Up = Regular Zoom In
    }
}

; --- Helper Function: Simulate a vertical drag for price scale zoom ---
; Uses a "hold and drag" approach to avoid double-click issues
SimulateVerticalDrag(pixels) {
    global isVerticalDragging, verticalDragStartX, verticalDragStartY

    if (!isVerticalDragging) {
        ; First scroll tick - start the drag
        MouseGetPos(&verticalDragStartX, &verticalDragStartY)
        Click "Down"
        isVerticalDragging := true
    }

    ; Move mouse for zoom
    MouseMove(0, pixels, , "R")

    ; Reset the release timer (will release after scrolling stops)
    SetTimer(ReleaseVerticalDrag, -SCROLL_RELEASE_DELAY)
}

; --- Release the vertical drag after scrolling stops ---
ReleaseVerticalDrag() {
    global isVerticalDragging, verticalDragStartX, verticalDragStartY

    if (isVerticalDragging) {
        Click "Up"
        ; Return mouse to original position
        MouseMove(verticalDragStartX, verticalDragStartY)
        isVerticalDragging := false
    }
}

; --- Shift+Scroll for Horizontal Pan (NEW FEATURE) ---
+WheelUp:: {
    if (IsChartWindow()) {
        Send "{Left}"   ; Pan backward in time
    }
}

+WheelDown:: {
    if (IsChartWindow()) {
        Send "{Right}"  ; Pan forward in time
    }
}

; --- Middle-Click to Activate Ruler/Measurement Tool ---
MButton:: {
    if (IsChartWindow()) {
        Send "^{F3}"  ; Ctrl+F3 = Ruler tool
    }
}

; --- Left Click: Smart Drag & Double-Click Detection ---
~LButton:: {
    global isLButtonDown, isCtrlSent, StartX, StartY

    ; Double-click detection (within 150ms)
    if (A_TimeSincePriorHotkey < 150 && A_PriorHotkey = "~LButton") {
        if (IsChartWindow()) {
            ; Send Ctrl+Alt+S for auto-scale
            ; NOTE: You must configure this hotkey in NinjaTrader:
            ; Tools > Hot Keys > "Auto scale and return" > Set to Ctrl+Alt+S
            Send "^!s"
        }
        return
    }

    ; Skip if modifier key is already held
    if (GetKeyState("Ctrl", "P") || GetKeyState("Shift", "P") || GetKeyState("Alt", "P"))
        return

    ; Start drag detection
    MouseGetPos(&StartX, &StartY)
    isLButtonDown := true
    isCtrlSent := false
    SetTimer(CheckForDrag, 20)
}

#HotIf  ; Reset context
