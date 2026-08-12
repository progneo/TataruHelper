param(
    [ValidateSet("stable", "prerelease")]
    [string]$Channel = "stable",
    [string]$Version = "",
    [string]$PackId = "TataruHelper",
    [string]$MainExe = "TataruHelper.exe",
    [string]$ProjectPath = "",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    [string]$RepoUrl = "https://github.com/NightlyRevenger/TataruHelper",
    [string]$RepoToken = "",
    [switch]$SkipPublish,
    [switch]$SkipDownloadLatest,
    [switch]$InstallerOnly,

    # Ship whatever index is already in the publish folder instead of building
    # one. Building fetches the translation project in full: a few hundred
    # megabytes and some minutes.
    [switch]$SkipReferenceIndex,

    # A folder holding an unpacked xivrus export, when there is one at hand and
    # the download is not wanted. An index built that way carries no revision,
    # so the first update after install will fetch a fresh one.
    [string]$ReferenceIndexSource = ""
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
$iconPath = Join-Path $repoRoot "TataruHelper/Resources/app_icon2.ico"

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

# The index of hand-made translations is not in the repository - hundreds of
# megabytes that the translation project adds to weekly - so a release builds
# its own. Shipped rather than left to the button so that a fresh install
# translates from the first line, works without a network, and keeps a copy of
# the translations should the project ever go away. The application reads
# whichever is newer, this one or one the user has fetched since.
#
# Built by the published exe rather than by a script of its own: two copies of
# the parsing rules had already drifted apart once. Started rather than called,
# because the application is a windowed program and PowerShell will not wait
# for it. It also asks for administrator rights, so run the release from an
# elevated console - an unelevated one hands the work to a process whose output
# and exit code it never sees.
if (-not $SkipReferenceIndex) {
    Write-Host "[Velopack] Building the reference translation index..."

    $indexPath = Join-Path $PublishDir "Resources/ReferenceTranslations.db"
    $indexArgs = @("--build-reference-index", "--output", $indexPath)
    if ($ReferenceIndexSource) {
        $indexArgs += @("--source", $ReferenceIndexSource)
    }

    $indexLog = Join-Path $OutputDir "reference-index.log"
    $indexErrorLog = Join-Path $OutputDir "reference-index.err.log"

    $indexProcess = Start-Process -FilePath (Join-Path $PublishDir $MainExe) `
        -ArgumentList $indexArgs -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $indexLog -RedirectStandardError $indexErrorLog

    Get-Content $indexLog -ErrorAction SilentlyContinue | Write-Host

    if ($indexProcess.ExitCode -ne 0) {
        Get-Content $indexErrorLog -ErrorAction SilentlyContinue | Write-Warning
        throw "Building the reference index failed with exit code $($indexProcess.ExitCode). See '$indexLog'."
    }

    if (-not (Test-Path $indexPath)) {
        throw "The reference index was reported as built but '$indexPath' is not there."
    }
}

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

    # The token is masked out of the echo. It was printed in full, which put a
    # live credential into the release log of every build - the one file anybody
    # would attach when asking why a release failed.
    $echoedArgs = @()
    for ($i = 0; $i -lt $downloadArgs.Count; $i++) {
        $echoedArgs += if ($i -gt 0 -and $downloadArgs[$i - 1] -eq "--token") { "***" } else { $downloadArgs[$i] }
    }

    Write-Host ("[Velopack] Command: " + ((@($vpkPrefix) + $echoedArgs) -join " "))

    try {
        Invoke-VpkCommand -Prefix $vpkPrefix -Args $downloadArgs
    }
    catch {
        Write-Warning "Unable to download latest published release. Delta package may not be generated."
        Write-Warning $_
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

# The installer under the name it had for years.
#
# https://github.com/NightlyRevenger/TataruHelper/releases/latest/download/Setup.exe
# is the link in the guides, in Discord, and in whatever people bookmarked -
# 76,493 downloads on v0.9.108 alone - and it broke when packaging started
# naming the file after the pack id. Both names are shipped rather than one
# renamed: the current name is what the README links and what the downloads
# badge counts, and moving it would trade one broken link for another.
$setupPath = Join-Path $OutputDir "$PackId-$vpkChannel-Setup.exe"
if (Test-Path $setupPath) {
    Copy-Item $setupPath (Join-Path $OutputDir "Setup.exe") -Force
    Write-Host "[Velopack] Installer also published as Setup.exe for the long-standing link."
}
else {
    Write-Warning "Installer '$setupPath' was not found; Setup.exe was not written."
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
