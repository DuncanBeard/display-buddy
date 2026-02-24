# setup-framework-dependent.ps1
# Installs .NET 8 Desktop Runtime, copies test config, and launches the app in Windows Sandbox.

# Wait briefly for mapped folders to become available
Start-Sleep -Seconds 2

$installerPath = "$env:TEMP\dotnet-desktop-runtime.exe"
$appPath = "C:\Users\Public\TaskbarAlignmentTool\win-x64-framework-dependent\TaskbarAlignmentTool.exe"

Write-Host "Downloading .NET 8 Desktop Runtime..." -ForegroundColor Cyan
try {
    Invoke-WebRequest -Uri "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0/latest/windowsdesktop-runtime-win-x64.exe" `
        -OutFile $installerPath -UseBasicParsing
} catch {
    Write-Host "Direct download failed, trying alternate URL..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" `
        -OutFile $installerPath -UseBasicParsing
}

Write-Host "Installing .NET 8 Desktop Runtime..." -ForegroundColor Cyan
Start-Process -FilePath $installerPath -ArgumentList "/install", "/quiet", "/norestart" -Wait

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

Write-Host "Launching TaskbarAlignmentTool..." -ForegroundColor Green
Start-Process -FilePath $appPath

# Open some windows to populate the taskbar
Start-Process explorer.exe "C:\"
Start-Process explorer.exe "$env:USERPROFILE\Documents"
Start-Process notepad.exe
Start-Process msedge.exe "https://www.bing.com" -ErrorAction SilentlyContinue
Start-Process msedge.exe "https://learn.microsoft.com" -ErrorAction SilentlyContinue

Write-Host "`nTaskbar Alignment Tool launched. Check the system tray." -ForegroundColor Green
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
