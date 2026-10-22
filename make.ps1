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
      msix        Build the MSIX, sign it with a local test cert, install and launch it
      clean       Remove bin, obj, and publish folders
      help        Show this help

.EXAMPLE
    .\make.ps1 build
    .\make.ps1 publish
    .\make.ps1 sandbox
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "publish", "sandbox", "sandbox-fd", "run", "msix", "clean", "help")]
    [string]$Command = "help"
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = "$root\TaskBard"
$csproj = "$project\TaskBard.csproj"
$solution = "$root\TaskBard.slnx"
$testProject = "$root\TaskBard.Tests"
$packageProject = "$root\TaskBard.Package"

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

function Find-PackagingMSBuild {
    # The .wapproj needs Visual Studio's MSBuild with the MSIX packaging tools
    # (DesktopBridge targets); the dotnet CLI can't build it.
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { throw "Visual Studio not found (no vswhere.exe)" }
    $installs = & $vswhere -all -prerelease -sort -property installationPath
    foreach ($vs in $installs) {
        $msbuild = "$vs\MSBuild\Current\Bin\amd64\MSBuild.exe"
        if ((Test-Path "$vs\MSBuild\Microsoft\DesktopBridge\Microsoft.DesktopBridge.targets") -and (Test-Path $msbuild)) {
            return $msbuild
        }
    }
    throw "No Visual Studio install has the MSIX packaging tools (DesktopBridge targets)"
}

function Find-SignTool {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
    if (-not $signtool) { throw "signtool.exe not found (install the Windows SDK)" }
    return $signtool.FullName
}

# Returns a code-signing cert whose subject matches the manifest Publisher,
# creating a self-signed one in CurrentUser\My on first use. Local testing only:
# Store submissions are signed by Partner Center.
function Get-TestSigningCert {
    param([string]$Publisher)

    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Publisher -and $_.NotAfter -gt (Get-Date).AddDays(1) -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($cert) {
        Write-Host "  Using test cert $($cert.Thumbprint) (expires $($cert.NotAfter.ToString('yyyy-MM-dd')))" -ForegroundColor Green
        return $cert
    }

    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher `
        -KeyUsage DigitalSignature -FriendlyName "TaskBard local test signing" `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(1) `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    Write-Host "  Created test cert $($cert.Thumbprint) for $Publisher" -ForegroundColor Green
    return $cert
}

# Windows only installs sideloaded MSIX signed by a cert in LocalMachine\TrustedPeople.
# Importing there needs admin, so this prompts UAC once per new cert.
function Confirm-CertTrusted {
    param($Cert, [string]$OutDir)

    if (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $Cert.Thumbprint) {
        Write-Host "  Test cert already trusted" -ForegroundColor Green
        return
    }

    $cer = "$OutDir\TaskBard-test.cer"
    Export-Certificate -Cert $Cert -FilePath $cer | Out-Null
    Write-Host "  Trusting test cert in LocalMachine\TrustedPeople (approve the UAC prompt)..." -ForegroundColor Yellow
    $import = "Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null"
    $shell = (Get-Process -Id $PID).Path
    $p = Start-Process $shell -Verb RunAs -Wait -PassThru -ArgumentList "-NoProfile", "-Command", $import
    if ($p.ExitCode -ne 0 -or -not (Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq $Cert.Thumbprint)) {
        throw "Could not trust the test cert (UAC declined?)"
    }
    Write-Host "  OK: test cert trusted" -ForegroundColor Green
}

function Invoke-Msix {
    Write-Host "=== MSIX (build, test-sign, install) ===" -ForegroundColor Cyan

    [xml]$manifest = Get-Content "$packageProject\Package.appxmanifest"
    $identity = $manifest.Package.Identity
    $appId = $manifest.Package.Applications.Application.Id
    $outDir = "$root\publish\msix"
    Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory $outDir -Force | Out-Null

    Write-Host "`n--- Build package ---" -ForegroundColor Yellow
    $msbuild = Find-PackagingMSBuild
    Write-Host "  MSBuild: $msbuild"
    & $msbuild "$packageProject\TaskBard.Package.wapproj" -restore -nologo -v:minimal `
        -p:Configuration=Release -p:Platform=x64 `
        -p:AppxPackageSigningEnabled=false -p:UapAppxPackageBuildMode=SideloadOnly `
        -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Never "-p:AppxPackageDir=$outDir\\"
    if ($LASTEXITCODE -ne 0) { throw "MSIX build failed" }
    $msix = Get-ChildItem $outDir -Recurse -Filter *.msix | Select-Object -First 1
    if (-not $msix) { throw "No .msix produced under $outDir" }

    Write-Host "`n--- Sign ---" -ForegroundColor Yellow
    $cert = Get-TestSigningCert -Publisher $identity.Publisher
    & (Find-SignTool) sign /fd SHA256 /sha1 $cert.Thumbprint /s My $msix.FullName
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
    Confirm-CertTrusted -Cert $cert -OutDir $outDir

    Write-Host "`n--- Install ---" -ForegroundColor Yellow
    # A running copy (portable or packaged) holds the single-instance mutex and
    # would make the new one exit silently.
    Get-Process TaskBard -ErrorAction SilentlyContinue | Stop-Process -Force
    # Same-version reinstalls are rejected, so remove first. This also resets the
    # package's startup-task state and package-local data.
    Get-AppxPackage -Name $identity.Name | Remove-AppxPackage
    Add-AppxPackage -Path $msix.FullName
    $pkg = Get-AppxPackage -Name $identity.Name
    if (-not $pkg) { throw "Package not installed" }
    Write-Host "  OK: $($pkg.PackageFullName)" -ForegroundColor Green

    Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!$appId"
    Write-Host "`n=== MSIX installed and launched. Check the system tray. ===" -ForegroundColor Green
    Write-Host "  Uninstall: Get-AppxPackage $($identity.Name) | Remove-AppxPackage"
}

function Invoke-Clean {
    Write-Host "=== Clean ===" -ForegroundColor Cyan
    $dirs = @("$project\bin", "$project\obj", "$testProject\bin", "$testProject\obj",
        "$packageProject\bin", "$packageProject\obj", "$packageProject\BundleArtifacts", "$root\publish")
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
    msix        Build the MSIX, sign it with a local test cert, install and launch it
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
        "msix"       { Invoke-Msix }
        "clean"      { Invoke-Clean }
        "help"       { Show-Help }
    }
} catch {
    Write-Host "`nFAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
