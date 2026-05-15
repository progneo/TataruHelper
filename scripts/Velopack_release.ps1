param(
    [ValidateSet("stable", "prerelease")]
    [string]$Channel = "stable",
    [string]$Version = "",
    [string]$PackId = "TataruHelper",
    [string]$MainExe = "TataruHelper.exe",
    [string]$ProjectPath = "",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
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
        return @("vpk")
    }

    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        return @("dotnet", "tool", "run", "vpk", "--")
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

$vpkPrefix = Resolve-VpkCommand
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

Write-Host "[Velopack] Packaging release..."
Write-Host ("[Velopack] Command: " + (($vpkPrefix + $vpkArgs) -join " "))

if ($vpkPrefix.Count -eq 1) {
    & $vpkPrefix[0] @vpkArgs
}
else {
    & $vpkPrefix[0] $vpkPrefix[1] $vpkPrefix[2] $vpkPrefix[3] @vpkArgs
}

Write-Host "[Velopack] Done."
Write-Host "[Velopack] Channel: $vpkChannel"
Write-Host "[Velopack] Version: $Version"
Write-Host "[Velopack] PublishDir: $PublishDir"
Write-Host "[Velopack] OutputDir: $OutputDir"
