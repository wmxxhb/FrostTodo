param([switch]$Package)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$runtime=Join-Path $projectRoot 'electron\runtime'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework=Split-Path $compiler
$arguments=@('/nologo','/target:exe','/main:FrostTodo.ElectronBridge','/define:GPT_STYLE;CHROMIUM_CORE','/platform:x64','/optimize+','/codepage:65001','/utf8output',('/out:'+(Join-Path $runtime 'FrostTodo.Core.exe')))
$arguments+='/resource:'+(Join-Path $projectRoot 'Assets\FrostTodo.ico')+',FrostTodo.AppIcon'
$arguments+='/resource:'+(Join-Path $projectRoot 'Assets\GptStyles.xaml')+',FrostTodo.GptStyles'
$arguments+='/resource:'+(Join-Path $projectRoot 'Assets\GptIcons.json')+',FrostTodo.GptIcons'
foreach($reference in @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')){$arguments+='/reference:'+(Join-Path $framework $reference)}
$sources=Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object {$_.FullName}
& $compiler @arguments @sources
if($LASTEXITCODE -ne 0){throw 'Task engine build failed.'}
Copy-Item -LiteralPath (Join-Path $projectRoot 'FrostTodo.exe.config') -Destination (Join-Path $runtime 'FrostTodo.Core.exe.config') -Force
if($Package){Push-Location (Join-Path $projectRoot 'electron');try{node package.cjs;if($LASTEXITCODE -ne 0){throw 'Packaging failed.'}}finally{Pop-Location}}
Write-Output $runtime
