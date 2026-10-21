<#
.SYNOPSIS
    Task runner for TaskBard.

.DESCRIPTION
    Usage: .\make.ps1 <command>

    Commands:
      build       Build the solution (Debug)
      test        Build, run MSTest, publish, and run artifact checks
      publish     Build Release packages (framework-dependent + self-contained zips)
      sandbox     Publish then launch the self-contained Windows Sandbox
      sandbox-fd  Publish then launch the framework-dependent Windows Sandbox
      run         Build and run the app locally
      clean       Remove bin, obj, and publish folders
      help        Show this help

.EXAMPLE
    .\make.ps1 build
    .\make.ps1 publish
    .\make.ps1 sandbox
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "publish", "sandbox", "sandbox-fd", "run", "clean", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = "$root\TaskBard"
$csproj = "$project\TaskBard.csproj"
$solution = "$root\TaskBard.slnx"
$testProject = "$root\TaskBard.Tests"

# Release archives and the folders they are built from. Deliberately restated
# here rather than shared with Invoke-Publish, so the artifact checks verify
# what publish produced instead of echoing its own variables back.
$publishVariants = [ordered]@{
    "TaskBard-win-x64.zip"               = "TaskBard-win-x64-framework-dependent"
    "TaskBard-win-x64-self-contained.zip" = "TaskBard-win-x64-self-contained"
}

# File names that would mean test tooling leaked into a release artifact.
$testAssemblyPattern = '^(MSTest|Microsoft\.VisualStudio\.TestPlatform|Microsoft\.TestPlatform|Microsoft\.Testing\.|testhost|TaskBard\.Tests)'

function Invoke-Build {
    Write-Host "=== Build (Debug) ===" -ForegroundColor Cyan
    dotnet build $solution
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}

function Invoke-Test {
    Invoke-Build

    Write-Host "`n=== MSTest ===" -ForegroundColor Cyan
    dotnet test $solution --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

    Write-Host "`n--- Smoke test: verify exe exists ---" -ForegroundColor Yellow
    $exe = Get-ChildItem "$project\bin\Debug\*\TaskBard.exe" -Recurse | Select-Object -First 1
    if (-not $exe) { throw "EXE not found after build" }
    Write-Host "  OK: $($exe.FullName) ($( '{0:N1} MB' -f ($exe.Length / 1MB) ))" -ForegroundColor Green

    Write-Host "`n--- Smoke test: verify assembly exists ---" -ForegroundColor Yellow
    $dll = Get-ChildItem "$project\bin\Debug\*\TaskBard.dll" -Recurse | Select-Object -First 1
    if (-not $dll) { throw "DLL not found" }
    Write-Host "  OK: Assembly present" -ForegroundColor Green

    # Publish asserts the release artifacts before returning.
    Write-Host ""
    Invoke-Publish

    Write-Host "`n=== All tests and artifact checks passed ===" -ForegroundColor Green
}

function Assert-PublishArtifacts {
    Write-Host "`n--- Artifact checks ---" -ForegroundColor Yellow
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $publish = "$root\publish"

    $zips = @(Get-ChildItem "$publish\*.zip" | ForEach-Object Name | Sort-Object)
    $expectedZips = @($publishVariants.Keys | Sort-Object)
    if (($zips -join '|') -ne ($expectedZips -join '|')) {
        throw "Expected archives [$($expectedZips -join ', ')] but found [$($zips -join ', ')]"
    }

    foreach ($zipName in $publishVariants.Keys) {
        $folder = "$publish\$($publishVariants[$zipName])"

        if (-not (Test-Path "$folder\TaskBard.exe")) { throw "TaskBard.exe missing from $folder" }
        $leaked = @(Get-ChildItem $folder -Recurse -File | Where-Object Name -Match $testAssemblyPattern)
        if ($leaked) { throw "Test assemblies in ${folder}: $($leaked.Name -join ', ')" }

        $zip = [System.IO.Compression.ZipFile]::OpenRead("$publish\$zipName")
        try {
            $entries = @($zip.Entries | ForEach-Object FullName)
        } finally {
            $zip.Dispose()
        }
        if ($entries -notcontains "TaskBard.exe") { throw "TaskBard.exe missing from $zipName" }
        $leaked = @($entries | Where-Object { (Split-Path $_ -Leaf) -match $testAssemblyPattern })
        if ($leaked) { throw "Test assemblies in ${zipName}: $($leaked -join ', ')" }

        Write-Host "  OK: $zipName <- $($publishVariants[$zipName])\TaskBard.exe, no test assemblies" -ForegroundColor Green
    }
}

function Invoke-Publish {
    Write-Host "=== Publish (Release) ===" -ForegroundColor Cyan

    Remove-Item "$root\publish" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "`n--- Framework-dependent ---" -ForegroundColor Yellow
    dotnet publish $project -c Release -r win-x64 --no-self-contained `
        -p:PublishSingleFile=true -o "$root\publish\TaskBard-win-x64-framework-dependent"
    if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed" }

    Write-Host "`n--- Self-contained ---" -ForegroundColor Yellow
    dotnet publish $project -c Release -r win-x64 --self-contained `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "$root\publish\TaskBard-win-x64-self-contained"
    if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed" }

    Write-Host "`n--- Creating zip archives ---" -ForegroundColor Yellow
    Compress-Archive -Path "$root\publish\TaskBard-win-x64-framework-dependent\*" `
        -DestinationPath "$root\publish\TaskBard-win-x64.zip" -Force
    Compress-Archive -Path "$root\publish\TaskBard-win-x64-self-contained\*" `
        -DestinationPath "$root\publish\TaskBard-win-x64-self-contained.zip" -Force

    Assert-PublishArtifacts

    Write-Host "`n=== Publish complete ===" -ForegroundColor Green
    Get-ChildItem "$root\publish\*.zip" | ForEach-Object {
        Write-Host "  $($_.Name) ($('{0:N1} MB' -f ($_.Length / 1MB)))"
    }
}

function Invoke-Sandbox {
    param([string]$WsbFile)

    # Always rebuild before launching sandbox
    Invoke-Publish

    $wsb = "$root\sandbox\$WsbFile"
    if (-not (Test-Path $wsb)) { throw "Sandbox config not found: $wsb" }

    Write-Host "=== Launching Windows Sandbox: $WsbFile ===" -ForegroundColor Cyan
    Start-Process $wsb
}

function Invoke-Run {
    Write-Host "=== Build + Run ===" -ForegroundColor Cyan
    dotnet build $csproj
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Start-Process dotnet -ArgumentList "run", "--project", $project, "--no-build"
    Write-Host "App launched. Check the system tray." -ForegroundColor Green
}

function Invoke-Clean {
    Write-Host "=== Clean ===" -ForegroundColor Cyan
    $dirs = @("$project\bin", "$project\obj", "$testProject\bin", "$testProject\obj", "$root\publish")
    foreach ($d in $dirs) {
        if (Test-Path $d) {
            Remove-Item $d -Recurse -Force
            Write-Host "  Removed $d"
        }
    }
    Write-Host "=== Clean complete ===" -ForegroundColor Green
}

function Show-Help {
    Write-Host @"

  TaskBard Task Runner
  ====================

  Usage: .\make.ps1 <command>

  Commands:
    build       Build the solution (Debug)
    test        Build, run MSTest, publish, and run artifact checks
    publish     Build Release packages (framework-dependent + self-contained zips)
    sandbox     Publish then launch self-contained Windows Sandbox
    sandbox-fd  Publish then launch framework-dependent Windows Sandbox
    run         Build and run the app locally
    clean       Remove bin, obj, and publish folders
    help        Show this help

"@ -ForegroundColor White
}

# Dispatch. Any failure exits non-zero so callers (and CI) see it.
try {
    switch ($Command) {
        "build"      { Invoke-Build }
        "test"       { Invoke-Test }
        "publish"    { Invoke-Publish }
        "sandbox"    { Invoke-Sandbox "TestSelfContained.wsb" }
        "sandbox-fd" { Invoke-Sandbox "TestFrameworkDependent.wsb" }
        "run"        { Invoke-Run }
        "clean"      { Invoke-Clean }
        "help"       { Show-Help }
    }
} catch {
    Write-Host "`nFAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
