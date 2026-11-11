$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$output = Join-Path $root 'dist\tests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$testExecutable = Join-Path $output 'ConversionTests.exe'
& $compiler /nologo /target:exe /codepage:65001 "/out:$testExecutable" `
    (Join-Path $root 'src\Kedmanee.cs') (Join-Path $PSScriptRoot 'ConversionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Conversion tests failed.' }
