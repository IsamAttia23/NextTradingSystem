param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\bin\Debug\net10.0'),
    [switch]$WithDependencies,
    [string]$BrowsersPath
)
$ErrorActionPreference = 'Stop'
$installer = Join-Path $OutputDirectory 'playwright.ps1'
if (!(Test-Path -LiteralPath $installer)) {
    throw 'Build or publish NEXT.web first, then pass its output directory.'
}
$options = @('install', 'chromium', '--only-shell')
if ($WithDependencies) { $options += '--with-deps' }
if (!$BrowsersPath) { $BrowsersPath = Join-Path $OutputDirectory 'App_Data/playwright' }
$previousPath = $env:PLAYWRIGHT_BROWSERS_PATH
try {
    $env:PLAYWRIGHT_BROWSERS_PATH = [System.IO.Path]::GetFullPath($BrowsersPath)
    & $installer @options
    if ($LASTEXITCODE -ne 0) { throw 'Chromium installation failed.' }
} finally { $env:PLAYWRIGHT_BROWSERS_PATH = $previousPath }
