NT8 Auto Reconnect Script - Instructions
=========================================

INSTALLATION
------------

1. Install AutoHotkey v2:
   - Go to: https://www.autohotkey.com/
   - Click "Download" and choose "AutoHotkey v2.0"
   - Run the installer and follow the prompts

2. Run the Script:
   - Double-click "NT8AutoReconnect.ahk"
   - A green tray icon will appear in your system tray
   - The script is now monitoring NinjaTrader 8


HOTKEYS
-------

Ctrl+Alt+P  - Pause/Resume monitoring
Ctrl+Alt+X  - Exit the script


TRAY MENU
---------

Right-click the tray icon to access:
- Status information
- Pause/Resume
- Check Now (manual connection check)
- Open Log File
- Exit


HOW IT WORKS
------------

1. The script monitors the NT8 log file at:
   Documents\NinjaTrader 8\log\

2. It checks every 5 seconds for disconnection messages

3. When disconnection is detected:
   - Waits 3 seconds (to avoid false positives)
   - Shows a notification
   - Opens Control Center
   - Attempts to reconnect via the Connections menu

4. All reconnection attempts are logged to:
   NT8Reconnect_Log.txt (same folder as the script)


CONFIGURATION
-------------

To change settings, edit these values at the top of the script:

  CheckInterval := 5000      ; Check every 5 seconds (in milliseconds)
  ReconnectDelay := 3000     ; Wait 3 seconds before reconnecting
  ConnectionName := "NinjaTrader Continuum"  ; Your connection name


AUTO-START AT WINDOWS STARTUP
-----------------------------

Method 1 - Startup Folder (Recommended):
1. Press Win+R, type: shell:startup
2. Press Enter
3. Create a shortcut to NT8AutoReconnect.ahk in this folder

Method 2 - Task Scheduler:
1. Open Task Scheduler (search in Start menu)
2. Click "Create Basic Task"
3. Name: "NT8 Auto Reconnect"
4. Trigger: "When I log on"
5. Action: "Start a program"
6. Browse to: NT8AutoReconnect.ahk
7. Finish


TROUBLESHOOTING
---------------

Script not detecting disconnections:
- Make sure NT8 is running and creating log files
- Check that the log path is correct in the script
- Try the "Check Now" option from the tray menu

Reconnection not working:
- Make sure Control Center window is open (not just charts)
- The window should be visible (can be behind other windows)
- Try running the script as Administrator

False positives:
- Increase ReconnectDelay value (e.g., 5000 for 5 seconds)
- Increase CheckInterval value

To see what's happening:
- Right-click tray icon > Open Log File
- Check the timestamps and messages


NOTES
-----

- Keep the Control Center window open (can be in background)
- Don't minimize Control Center to system tray
- The script works best when NT8 UI is accessible
- Log file is created in the same folder as the script
