param(
    [ValidateSet("stable", "prerelease")]
    [string]$Channel = "stable",
    [string]$Version = "",
    [string]$PackId = "TataruHelper",
    [string]$MainExe = "TataruHelper.exe",
    [string]$ProjectPath = "",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    [bool]$IncludeMsiDeploymentTool = $true,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyInfoPath
    )

    $content = Get-Content -Path $AssemblyInfoPath -Raw
    $regex = [regex]'Assembly(File)?Version\("(?<version>\d+\.\d+\.\d+\.\d+)"\)'
    $match = $regex.Match($content)
    if (-not $match.Success) {
        throw "Cannot resolve assembly version from '$AssemblyInfoPath'."
    }

    $parts = $match.Groups["version"].Value.Split(".")
    return "$($parts[0]).$($parts[1]).$($parts[2])"
}

function Resolve-VpkCommand {
    if (Get-Command vpk -ErrorAction SilentlyContinue) {
        return [string[]]@("vpk")
    }

    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        return [string[]]@("dotnet", "tool", "run", "vpk", "--")
    }

    throw "Velopack CLI is not available. Install `vpk` (`dotnet tool install -g vpk --version 0.0.1298`) and retry."
}

$scriptRoot = Split-Path -Path $PSCommandPath -Parent
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot "TataruHelper/TataruHelper.csproj"
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "artifacts/publish/$PackId/win-x64"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts/velopack/$Channel"
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $assemblyInfoPath = Join-Path $repoRoot "TataruHelper/Properties/AssemblyInfo.cs"
    $Version = Resolve-ProjectVersion -AssemblyInfoPath $assemblyInfoPath
}

$vpkChannel = if ($Channel -eq "prerelease") { "prerelease" } else { "stable" }
$iconPath = Join-Path $repoRoot "TataruHelper/app_icon2.ico"

if (-not $SkipPublish) {
    Write-Host "[Velopack] Publishing app binaries..."
    dotnet publish $ProjectPath `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=false `
        /p:PublishReadyToRun=true `
        -o $PublishDir
}

if (-not (Test-Path (Join-Path $PublishDir $MainExe))) {
    throw "Main executable '$MainExe' was not found in '$PublishDir'."
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

[string[]]$vpkPrefix = Resolve-VpkCommand

$msiFlag = $null
if ($IncludeMsiDeploymentTool) {
    $previousRollForwardForHelp = $env:DOTNET_ROLL_FORWARD
    $env:DOTNET_ROLL_FORWARD = "LatestMajor"
    try {
        $packHelpOutput = if ($vpkPrefix.Count -eq 1) {
            & $vpkPrefix[0] "pack" "-h" 2>&1
        }
        else {
            & $vpkPrefix[0] $vpkPrefix[1] $vpkPrefix[2] $vpkPrefix[3] "pack" "-h" 2>&1
        }
    }
    finally {
        if ($null -eq $previousRollForwardForHelp) {
            Remove-Item Env:DOTNET_ROLL_FORWARD -ErrorAction SilentlyContinue
        }
        else {
            $env:DOTNET_ROLL_FORWARD = $previousRollForwardForHelp
        }
    }

    $packHelpText = ($packHelpOutput | Out-String)
    if ($packHelpText -match "(?m)^\s*--msi\b") {
        $msiFlag = "--msi"
    }
    elseif ($packHelpText -match "(?m)^\s*--msiDeploymentTool\b") {
        $msiFlag = "--msiDeploymentTool"
    }
    else {
        Write-Warning "MSI packaging flag was not found in current vpk build; Setup.exe only will be generated."
    }
}

$vpkArgs = @(
    "pack",
    "--packId", $PackId,
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--mainExe", $MainExe,
    "--channel", $vpkChannel,
    "--outputDir", $OutputDir,
    "--icon", $iconPath
)

if ($msiFlag) {
    $vpkArgs += $msiFlag
}

Write-Host "[Velopack] Packaging release..."
Write-Host ("[Velopack] Command: " + ((@($vpkPrefix) + $vpkArgs) -join " "))

$previousRollForward = $env:DOTNET_ROLL_FORWARD
$env:DOTNET_ROLL_FORWARD = "LatestMajor"
try {
    if ($vpkPrefix.Count -eq 1) {
        & $vpkPrefix[0] @vpkArgs
    }
    else {
        & $vpkPrefix[0] $vpkPrefix[1] $vpkPrefix[2] $vpkPrefix[3] @vpkArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Velopack CLI failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($null -eq $previousRollForward) {
        Remove-Item Env:DOTNET_ROLL_FORWARD -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_ROLL_FORWARD = $previousRollForward
    }
}

Write-Host "[Velopack] Done."
Write-Host "[Velopack] Channel: $vpkChannel"
Write-Host "[Velopack] Version: $Version"
Write-Host "[Velopack] PublishDir: $PublishDir"
Write-Host "[Velopack] OutputDir: $OutputDir"
