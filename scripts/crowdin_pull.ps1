# Pulls finished translations from Crowdin and compiles them for the app.
#
# Only the languages the application actually offers are pulled. A bare
# `crowdin download` would also overwrite Locale/en-US - English is a target
# language on the project as well as its source, and the file there is the
# English text the app displays, not a translation - and would add folders for
# 18 languages the Languages enum has no entry for. See crowdin.yml.
#
#   scripts/crowdin_pull.ps1
#   scripts/crowdin_pull.ps1 -Language ru
param(
    [string[]]$Language
)

$ErrorActionPreference = "Stop"

# The Languages enum in Utils/LanguageWrapper.cs, as Crowdin language ids.
$shipped = @("ru", "es-ES", "pl", "ko", "pt-BR", "ca", "it", "uk", "zh-CN", "zh-TW", "ja")

$wanted = if ($Language) { $Language } else { $shipped }
foreach ($lang in $wanted) {
    if ($shipped -notcontains $lang) {
        throw "'$lang' is not one of the languages the application offers: $($shipped -join ', ')"
    }
}

$repositoryRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repositoryRoot
try {
    $arguments = @("download")
    foreach ($lang in $wanted) { $arguments += @("-l", $lang) }

    & crowdin @arguments
    if ($LASTEXITCODE -ne 0) { throw "crowdin download failed with $LASTEXITCODE" }

    # Crowdin round-trips .po; NGettext loads the binary .mo, so a pull does
    # nothing on its own.
    python scripts/compile_locales.py
    if ($LASTEXITCODE -ne 0) { throw "compile_locales.py failed with $LASTEXITCODE" }
}
finally {
    Pop-Location
}
