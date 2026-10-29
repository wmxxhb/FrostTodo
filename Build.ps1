param([string]$OutputName = 'FrostTodo.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Split-Path $compiler
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001','/utf8output',('/out:' + (Join-Path $projectRoot $OutputName)),('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')))
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
& $compiler @arguments @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($OutputName -ne 'FrostTodo.exe') { Copy-Item -LiteralPath (Join-Path $projectRoot 'FrostTodo.exe.config') -Destination (Join-Path $projectRoot ($OutputName + '.config')) -Force }
Write-Output (Join-Path $projectRoot $OutputName)
