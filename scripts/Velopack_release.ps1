param(
    [ValidateSet("stable", "prerelease")]
    [string]$Channel = "stable",
    [string]$Version = "",
    [string]$PackId = "TataruHelper",
    [string]$MainExe = "TataruHelper.exe",
    [string]$ProjectPath = "",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    [string]$RepoUrl = "https://github.com/progneo/TataruHelper",
    [string]$RepoToken = "",
    [bool]$IncludeMsiDeploymentTool = $true,
    [switch]$SkipPublish,
    [switch]$SkipDownloadLatest,
    [switch]$InstallerOnly
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyInfoPath,
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    if (Test-Path $AssemblyInfoPath) {
        $content = Get-Content -Path $AssemblyInfoPath -Raw
        $regex = [regex]'Assembly(File)?Version\("(?<version>\d+\.\d+\.\d+(?:\.\d+)?)"\)'
        $match = $regex.Match($content)
        if ($match.Success) {
            $parts = $match.Groups["version"].Value.Split(".")
            return "$($parts[0]).$($parts[1]).$($parts[2])"
        }
    }

    if (-not (Test-Path $ProjectPath)) {
        throw "Cannot resolve project version: project file '$ProjectPath' was not found."
    }

    [xml]$projectXml = Get-Content -Path $ProjectPath -Raw
    $versionNode = $projectXml.SelectSingleNode("//Project/PropertyGroup/Version")
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        $versionNode = $projectXml.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion")
    }
    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        $versionNode = $projectXml.SelectSingleNode("//Project/PropertyGroup/FileVersion")
    }

    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw "Cannot resolve project version from '$AssemblyInfoPath' or '$ProjectPath'. Specify explicit -Version (e.g. -Version 0.10.3) for this run."
    }

    $projectVersion = $versionNode.InnerText.Trim()
    if ($projectVersion -notmatch '^(?<a>\d+)\.(?<b>\d+)\.(?<c>\d+)(?:\.\d+)?(?:[-+].*)?$') {
        throw "Resolved version '$projectVersion' has unsupported format."
    }

    return "$($matches['a']).$($matches['b']).$($matches['c'])"
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

function Invoke-VpkCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Prefix,
        [Parameter(Mandatory = $true)]
        [string[]]$Args
    )

    if ($Prefix.Count -eq 1) {
        & $Prefix[0] @Args
    }
    else {
        & $Prefix[0] $Prefix[1] $Prefix[2] $Prefix[3] @Args
    }
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
    $Version = Resolve-ProjectVersion -AssemblyInfoPath $assemblyInfoPath -ProjectPath $ProjectPath
}
Write-Host "[Velopack] Resolved version: $Version"

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

if ($InstallerOnly) {
    Write-Warning "-InstallerOnly switch is currently compatibility-only and does not change packaging outputs."
}

$fullCountBeforePack = @(Get-ChildItem -Path $OutputDir -Filter "*-full.nupkg" -File -ErrorAction SilentlyContinue).Count
$deltaCountBeforePack = @(Get-ChildItem -Path $OutputDir -Filter "*-delta.nupkg" -File -ErrorAction SilentlyContinue).Count

[string[]]$vpkPrefix = Resolve-VpkCommand
Write-Host ("[Velopack] vpk invocation prefix: " + ($vpkPrefix -join " "))
try {
    $vpkVersionArgs = @("--version")
    Write-Host ("[Velopack] Command: " + ((@($vpkPrefix) + $vpkVersionArgs) -join " "))
    Invoke-VpkCommand -Prefix $vpkPrefix -Args $vpkVersionArgs
}
catch {
    Write-Warning "Unable to print vpk version."
}

if (-not $SkipDownloadLatest) {
    $downloadHelpOutput = if ($vpkPrefix.Count -eq 1) {
        & $vpkPrefix[0] "download" "-h" 2>&1
    }
    else {
        & $vpkPrefix[0] $vpkPrefix[1] $vpkPrefix[2] $vpkPrefix[3] "download" "-h" 2>&1
    }

    $downloadHelpText = ($downloadHelpOutput | Out-String)
    Write-Host "[Velopack] download -h output:"
    Write-Host $downloadHelpText
    if ($downloadHelpText -notmatch "(?m)\bgithub\b") {
        throw "Current vpk build does not expose 'download github' command. Verify CLI version compatibility."
    }

    $downloadArgs = @(
        "download",
        "github",
        "--repoUrl", $RepoUrl,
        "--channel", $vpkChannel,
        "--outputDir", $OutputDir
    )

    if (-not [string]::IsNullOrWhiteSpace($RepoToken)) {
        $downloadArgs += @("--token", $RepoToken)
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $downloadArgs += @("--token", $env:GITHUB_TOKEN)
    }

    Write-Host "[Velopack] Downloading latest published release for delta base..."
    Write-Host ("[Velopack] Command: " + ((@($vpkPrefix) + $downloadArgs) -join " "))

    try {
        Invoke-VpkCommand -Prefix $vpkPrefix -Args $downloadArgs
    }
    catch {
        Write-Warning "Unable to download latest published release. Delta package may not be generated."
        Write-Warning $_
    }
}

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
    Invoke-VpkCommand -Prefix $vpkPrefix -Args $vpkArgs

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

$fullCountAfterPack = @(Get-ChildItem -Path $OutputDir -Filter "*-full.nupkg" -File -ErrorAction SilentlyContinue).Count
$deltaCountAfterPack = @(Get-ChildItem -Path $OutputDir -Filter "*-delta.nupkg" -File -ErrorAction SilentlyContinue).Count

if ($fullCountBeforePack -gt 0) {
    if ($deltaCountAfterPack -le $deltaCountBeforePack) {
        throw "Delta package was expected (previous full package exists in outputDir), but no new *-delta.nupkg was produced."
    }

    Write-Host "[Velopack] Delta package generated successfully."
}
else {
    Write-Host "[Velopack] No previous full package in outputDir before pack; delta package is not expected for first release in this environment."
}
