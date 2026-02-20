; === NinjaTrader 8 Chart Enhancements (AutoHotkey v1.1) ===
; === GOAL 1: Scroll Wheel to Zoom (INVERTED: Ctrl+Arrow)
; === GOAL 2: "Smarter-Drag"
; === GOAL 3: Double-Click to Auto-Scale (115ms)
; === STUCK-KEY BUG FIXED ===
; === PURE v1.LAST_SCRIPT_MODIFIED SYNTAX ===

#SingleInstance force
#Persistent
#MaxHotkeysPerInterval 200 ; Fix for fast-scrolling warning

; --- SCRIPT-WIDE SPEED SETTINGS ---
Process, Priority,, High
SetBatchLines, -1
SendMode Input

; --- GLOBAL VARIABLES ---
isCtrlSent := false
isLButtonDown := false
StartX := 0
StartY := 0

; --- SUBROUTINES (Defined Globally) ---

CheckForDrag:
    if (!isLButtonDown) ; Failsafe
    {
        SetTimer, CheckForDrag, Off
        return
    }
    
    if (isCtrlSent) ; We already sent Ctrl, do nothing
        return
        
    ; Check if mouse has moved
    MouseGetPos, CurrentX, CurrentY
    DistanceX := Abs(CurrentX - StartX)
    DistanceY := Abs(CurrentY - StartY)
    
    if (DistanceX > 3 or DistanceY > 3)
    {
        ; --- THIS IS A DRAG ---
        WinGetActiveTitle, WinTitle
        if InStr(WinTitle, "Chart")
        {
            SendInput {Control Down}
            isCtrlSent := true
            SetTimer, CheckForDrag, Off ; Stop checking
        }
    }
return

; --- GLOBAL HOTKEY (Fixes Stuck Key) ---
; This hotkey *must* be global to catch the mouse release
; even if the cursor is outside the NinjaTrader window.
~LButton Up::
    isLButtonDown := false
    SetTimer, CheckForDrag, Off
    
    if (isCtrlSent)
    {
        SendInput {Control Up}
        isCtrlSent := false
    }
return

; --- CONTEXT-SENSITIVE HOTKEYS ---
; These hotkeys will ONLY run when NinjaTrader is active.
#IfWinActive ahk_exe NinjaTrader.exe

; --- GOAL 1: Scroll Wheel to Zoom (INVERTED) ---
WheelUp::
    SendInput, ^{Down} ; Send Ctrl+Down (Zoom Out)
return

WheelDown::
    SendInput, ^{Up} ; Send Ctrl+Up (Zoom In)
return

; --- GOAL 2 & 3: "Smarter-Drag" & Double-Click ---
~LButton::
    ; --- START: NEW DOUBLE-CLICK LOGIC ---
    ; Check if this is a double-click (115ms)
    if (A_TimeSincePriorHotkey < 115)
    {
        ; This is a double-click. Check if it's on a chart.
        WinGetActiveTitle, WinTitle
        if InStr(WinTitle, "Chart")
        {
            ; Send the new, safer hotkey: Ctrl+Alt+S
            ; Make sure you set this hotkey in NinjaTrader first!
            SendInput, ^!s
        }
        return
    }
    ; --- END: NEW DOUBLE-CLICK LOGIC ---

    ; --- START: ORIGINAL DRAG LOGIC (This will run on single-clicks) ---
    if (GetKeyState("Ctrl", "P") or GetKeyState("Shift", "P") or GetKeyState("Alt", "P"))
        return

    MouseGetPos, StartX, StartY
    isLButtonDown := true
    isCtrlSent := false
    SetTimer, CheckForDrag, 20 ; Check every 20ms
return

#IfWinActive ; Reset context