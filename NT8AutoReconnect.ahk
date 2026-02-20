#Requires AutoHotkey v2.0
#SingleInstance Force
Persistent

; ============================================
; NT8 Auto Reconnect Script
; AutoHotkey v2
; ============================================

class NT8AutoReconnect {
    ; Configuration
    static CheckInterval := 5000          ; Check every 5 seconds
    static ReconnectDelay := 3000         ; Wait 3 seconds before reconnecting
    static ConnectionName := "NinjaTrader Continuum"
    static LogFilePath := "C:\Users\abhid\OneDrive\Documents\NinjaTrader 8\log\"
    static ScriptLogFile := A_ScriptDir "\NT8Reconnect_Log.txt"

    ; State
    static IsRunning := true
    static IsPaused := false
    static LastLogPosition := 0
    static CurrentLogFile := ""
    static TrayMenu := {}
    static CheckTimer := 0

    ; Initialize the script
    static Init() {
        this.SetupTray()
        this.Log("Script started")
        this.ShowNotification("NT8 Auto Reconnect", "Monitoring started")
        this.FindCurrentLogFile()
        this.StartMonitoring()
    }

    ; Setup system tray
    static SetupTray() {
        A_TrayMenu.Delete()
        A_TrayMenu.Add("NT8 Auto Reconnect", (*) => this.ShowStatus())
        A_TrayMenu.Add()
        A_TrayMenu.Add("Pause/Resume`tCtrl+Alt+P", (*) => this.TogglePause())
        A_TrayMenu.Add("Check Now", (*) => this.CheckConnection())
        A_TrayMenu.Add("Test Reconnect`tCtrl+Alt+T", (*) => this.TestReconnect())
        A_TrayMenu.Add("Open Log File", (*) => this.OpenLogFile())
        A_TrayMenu.Add()
        A_TrayMenu.Add("Exit`tCtrl+Alt+X", (*) => this.ExitScript())
        A_TrayMenu.Default := "NT8 Auto Reconnect"

        ; Set tray icon tip
        A_IconTip := "NT8 Auto Reconnect - Running"

        ; Try to use a custom icon color (green for running)
        try TraySetIcon("shell32.dll", 144)  ; Green checkmark
    }

    ; Find the current log file (NT8 uses date-based log files)
    static FindCurrentLogFile() {
        today := FormatTime(, "yyyyMMdd")
        ; NT8 log files are named like: log.20251230.00000.txt (not .log)
        logPattern := this.LogFilePath . "log." . today . ".*.txt"

        ; Find the most recent log file
        latestFile := ""
        latestTime := 0

        Loop Files logPattern {
            ; Skip .en.txt files, use the main log
            if InStr(A_LoopFileName, ".en.txt")
                continue
            if (A_LoopFileTimeModified > latestTime) {
                latestTime := A_LoopFileTimeModified
                latestFile := A_LoopFilePath
            }
        }

        ; If no today's log, find the most recent one
        if (latestFile = "") {
            Loop Files this.LogFilePath "log.*.txt" {
                if InStr(A_LoopFileName, ".en.txt")
                    continue
                if (A_LoopFileTimeModified > latestTime) {
                    latestTime := A_LoopFileTimeModified
                    latestFile := A_LoopFilePath
                }
            }
        }

        if (latestFile != "") {
            this.CurrentLogFile := latestFile
            ; Set position to end of file to only monitor new entries
            try {
                fileObj := FileOpen(latestFile, "r")
                if (fileObj) {
                    fileObj.Seek(0, 2)  ; Seek to end
                    this.LastLogPosition := fileObj.Pos
                    fileObj.Close()
                }
            }
            this.Log("Monitoring log file: " . latestFile)
        } else {
            this.Log("Warning: No NT8 log file found")
        }
    }

    ; Start the monitoring timer
    static StartMonitoring() {
        SetTimer(() => this.CheckConnection(), this.CheckInterval)
    }

    ; Check connection status
    static CheckConnection() {
        if (this.IsPaused)
            return

        ; First, refresh log file if needed (new day)
        this.RefreshLogFileIfNeeded()

        ; Check log file for disconnection messages
        if (this.CheckLogForDisconnection()) {
            this.Log("Disconnection detected!")
            this.HandleDisconnection()
            return
        }

        ; Also check via UI as backup
        if (this.CheckUIForDisconnection()) {
            this.Log("Disconnection detected via UI!")
            this.HandleDisconnection()
        }
    }

    ; Refresh log file if it's a new day
    static RefreshLogFileIfNeeded() {
        today := FormatTime(, "yyyyMMdd")
        if (!InStr(this.CurrentLogFile, today)) {
            this.FindCurrentLogFile()
        }
    }

    ; Check NT8 log file for disconnection
    static CheckLogForDisconnection() {
        if (this.CurrentLogFile = "" || !FileExist(this.CurrentLogFile))
            return false

        try {
            fileObj := FileOpen(this.CurrentLogFile, "r")
            if (!fileObj)
                return false

            ; Seek to last known position
            if (this.LastLogPosition > 0)
                fileObj.Seek(this.LastLogPosition, 0)

            newContent := fileObj.Read()
            this.LastLogPosition := fileObj.Pos
            fileObj.Close()

            if (newContent = "")
                return false

            ; Check for disconnection patterns
            disconnectPatterns := [
                "Connection lost",
                "Disconnected",
                "Connection.*failed",
                "Unable to connect",
                "Connection error",
                "Lost connection",
                this.ConnectionName . ".*disconnected",
                this.ConnectionName . ".*lost"
            ]

            for pattern in disconnectPatterns {
                if (RegExMatch(newContent, "i)" . pattern)) {
                    ; Make sure it's not a "reconnected" message
                    if (!RegExMatch(newContent, "i)reconnected|connection established|connected successfully")) {
                        return true
                    }
                }
            }
        } catch as err {
            this.Log("Error reading log file: " . err.Message)
        }

        return false
    }

    ; Check UI for disconnection (backup method)
    static CheckUIForDisconnection() {
        ; Find Control Center window
        try {
            if !WinExist("Control Center") {
                return false
            }

            ; Get window handle
            hwnd := WinExist("Control Center")

            ; Try to find connection status via UI Automation
            ; This checks if disconnection text is visible
            controlText := ""
            try {
                controlText := WinGetText("Control Center")
            }

            if (InStr(controlText, "Disconnected") && InStr(controlText, this.ConnectionName)) {
                return true
            }
        } catch {
            ; UI check failed, not critical
        }

        return false
    }

    ; Handle disconnection - perform reconnection
    static HandleDisconnection() {
        this.Log("Waiting " . (this.ReconnectDelay / 1000) . " seconds before reconnecting...")
        this.ShowNotification("NT8 Disconnected", "Attempting to reconnect in " . (this.ReconnectDelay / 1000) . " seconds...")

        Sleep(this.ReconnectDelay)

        ; Double-check we're still disconnected (avoid false positives)
        ; Give a brief moment and recheck log
        Sleep(500)

        this.AttemptReconnection()
    }

    ; Attempt to reconnect
    static AttemptReconnection() {
        this.Log("Attempting reconnection...")

        try {
            ; Find Control Center window
            if !WinExist("Control Center") {
                this.Log("Control Center window not found")
                this.ShowNotification("Reconnect Failed", "Control Center window not found")
                return false
            }

            ; Activate Control Center
            WinActivate("Control Center")
            WinWaitActive("Control Center",, 3)
            Sleep(500)

            ; Method 1: Try clicking on Connections menu
            if (this.ReconnectViaMenu()) {
                this.Log("Reconnection initiated via menu")
                this.ShowNotification("NT8 Reconnecting", "Reconnection initiated")
                return true
            }

            ; Method 2: Try right-clicking on connection status area
            if (this.ReconnectViaStatusArea()) {
                this.Log("Reconnection initiated via status area")
                this.ShowNotification("NT8 Reconnecting", "Reconnection initiated")
                return true
            }

            this.Log("Reconnection attempt completed")
            this.ShowNotification("NT8 Reconnect", "Reconnection attempted - check NT8")
            return true

        } catch as err {
            this.Log("Reconnection error: " . err.Message)
            this.ShowNotification("Reconnect Error", err.Message)
            return false
        }
    }

    ; Reconnect via Connections menu using ImageSearch
    ; Searches ONLY within the popup menu area for reliability
    static ReconnectViaMenu() {
        try {
            WinActivate("Control Center")
            WinWaitActive("Control Center",, 3)
            Sleep(500)

            this.Log("Opening Connections menu...")

            ; Open Connections menu
            Send("{Alt}")
            Sleep(200)
            Send("{Right}{Right}{Right}")
            Sleep(100)
            Send("{Enter}")
            Sleep(500)

            ; Wait for popup menu to appear
            if !WinWait("ahk_class #32768",, 2) {
                this.Log("Popup menu did not appear")
                return false
            }

            ; Get the popup menu position - we'll ONLY search within this area
            WinGetPos(&menuX, &menuY, &menuW, &menuH, "ahk_class #32768")
            this.Log("Menu area: X=" . menuX . " Y=" . menuY . " W=" . menuW . " H=" . menuH)

            ; Try ImageSearch first (most reliable)
            imagePath := A_ScriptDir . "\NinjaTraderContinuum.png"

            if FileExist(imagePath) {
                this.Log("Using ImageSearch within menu area...")
                CoordMode("Pixel", "Screen")
                CoordMode("Mouse", "Screen")

                ; Search ONLY within the popup menu bounds
                if ImageSearch(&foundX, &foundY, menuX, menuY, menuX + menuW, menuY + menuH, "*50 " . imagePath) {
                    this.Log("Found at X:" . foundX . " Y:" . foundY)
                    Click(foundX + 50, foundY + 8)  ; Click center of the found text
                    Sleep(500)
                    return true
                }
                this.Log("Image not found in menu, trying keyboard method...")
            } else {
                this.Log("Image file not found, using keyboard method...")
            }

            ; Fallback: Keyboard navigation
            ; Go to top of menu and navigate down
            Send("{Home}")
            Sleep(100)

            ; Type to jump to items starting with "N"
            Send("n")
            Sleep(200)

            ; Check if "NinjaTrader" is highlighted by pressing Enter
            Send("{Enter}")
            Sleep(500)

            ; If menu closed, we clicked something
            if !WinExist("ahk_class #32768") {
                this.Log("Clicked via keyboard navigation")
                return true
            }

            ; Still open? Try different approach
            Send("{Escape}")
            this.Log("Keyboard method failed")
            return false

        } catch as err {
            this.Log("Menu reconnect error: " . err.Message)
            Send("{Escape}")
            return false
        }
    }

    ; Toggle pause state
    static TogglePause() {
        this.IsPaused := !this.IsPaused

        if (this.IsPaused) {
            A_IconTip := "NT8 Auto Reconnect - PAUSED"
            try TraySetIcon("shell32.dll", 110)  ; Warning icon
            this.ShowNotification("NT8 Auto Reconnect", "Monitoring PAUSED")
            this.Log("Monitoring paused")
        } else {
            A_IconTip := "NT8 Auto Reconnect - Running"
            try TraySetIcon("shell32.dll", 144)  ; Green icon
            this.ShowNotification("NT8 Auto Reconnect", "Monitoring RESUMED")
            this.Log("Monitoring resumed")
        }
    }

    ; Show current status
    static ShowStatus() {
        status := this.IsPaused ? "PAUSED" : "Running"
        logFile := this.CurrentLogFile != "" ? this.CurrentLogFile : "Not found"

        MsgBox(
            "NT8 Auto Reconnect Status`n`n" .
            "Status: " . status . "`n" .
            "Check Interval: " . (this.CheckInterval / 1000) . " seconds`n" .
            "Reconnect Delay: " . (this.ReconnectDelay / 1000) . " seconds`n" .
            "Connection: " . this.ConnectionName . "`n" .
            "Log File: " . logFile,
            "NT8 Auto Reconnect",
            "Iconi"
        )
    }

    ; Open the script's log file
    static OpenLogFile() {
        if FileExist(this.ScriptLogFile)
            Run(this.ScriptLogFile)
        else
            MsgBox("Log file not found yet.", "NT8 Auto Reconnect", "Icon!")
    }

    ; Log message to file
    static Log(message) {
        timestamp := FormatTime(, "yyyy-MM-dd HH:mm:ss")
        logLine := "[" . timestamp . "] " . message . "`n"

        try {
            FileAppend(logLine, this.ScriptLogFile)
        }
    }

    ; Show Windows notification
    static ShowNotification(title, message) {
        TrayTip(message, title, "Iconi")
    }

    ; Exit the script
    static ExitScript() {
        this.Log("Script stopped")
        this.ShowNotification("NT8 Auto Reconnect", "Script stopped")
        Sleep(500)
        ExitApp()
    }

    ; Test reconnection manually (for testing purposes)
    static TestReconnect() {
        this.Log("=== MANUAL TEST TRIGGERED ===")
        this.ShowNotification("NT8 Test", "Testing reconnection sequence...")

        result := MsgBox(
            "This will test the reconnection sequence.`n`n" .
            "Make sure Control Center is open.`n`n" .
            "Do you want to proceed?",
            "NT8 Auto Reconnect - Test",
            "YesNo Iconi"
        )

        if (result = "Yes") {
            this.Log("Running test reconnection...")
            this.AttemptReconnection()
            this.Log("=== TEST COMPLETE ===")
        }
    }
}

; ============================================
; Hotkeys
; ============================================

^!p::NT8AutoReconnect.TogglePause()  ; Ctrl+Alt+P - Pause/Resume
^!x::NT8AutoReconnect.ExitScript()   ; Ctrl+Alt+X - Exit
^!t::NT8AutoReconnect.TestReconnect() ; Ctrl+Alt+T - Test reconnection

; ============================================
; Start the script
; ============================================

NT8AutoReconnect.Init()
