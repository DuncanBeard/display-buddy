# setup-self-contained.ps1
# Copies the test config and launches the self-contained build in Windows Sandbox.

# Wait briefly for mapped folders to become available
Start-Sleep -Seconds 2

# Copy default config to the app's expected location
$configDir = "$env:LOCALAPPDATA\TaskbarAlignmentTool"
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
try {
    Copy-Item "C:\Users\Public\Sandbox\config.json" "$configDir\config.json" -Force
} catch {
    Write-Host "Warning: Could not copy config.json - app will generate defaults" -ForegroundColor Yellow
}

# Open config in Notepad for easy editing during testing
Start-Process notepad.exe "$configDir\config.json"

# Launch the app
Start-Process "C:\Users\Public\TaskbarAlignmentTool\win-x64-self-contained\TaskbarAlignmentTool.exe"

# Open some windows to populate the taskbar
Start-Process explorer.exe "C:\"
Start-Process explorer.exe "$env:USERPROFILE\Documents"
Start-Process notepad.exe
Start-Process msedge.exe "https://www.bing.com" -ErrorAction SilentlyContinue
Start-Process msedge.exe "https://learn.microsoft.com" -ErrorAction SilentlyContinue

# Keep the PowerShell window open so you can see any errors
Write-Host "`nTaskbar Alignment Tool launched. Check the system tray." -ForegroundColor Green
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
