$ErrorActionPreference = 'Stop'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework compiler not found. Build on Windows with .NET Framework installed.'
}
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $output -Force | Out-Null

$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs'
$icon = Join-Path $PSScriptRoot 'src\AlienTranslate.ico'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/out:$output\AlienTranslate.exe" `
    "/win32icon:$icon" `
    "/reference:$wpf\UIAutomationClient.dll" `
    "/reference:$wpf\UIAutomationTypes.dll" `
    "/reference:$wpf\WindowsBase.dll" `
    $sources.FullName
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (csc exit code $LASTEXITCODE)."
}
Write-Output (Join-Path $output 'AlienTranslate.exe')
