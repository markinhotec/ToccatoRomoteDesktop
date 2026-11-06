param([switch]$SkipInstaller, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'artifacts'
$portable = Join-Path $out 'portable'
New-Item -ItemType Directory -Force -Path $portable | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path $compiler)) { throw 'Instale o .NET Framework 4.8 para compilar o projeto.' }
$release = Get-ItemPropertyValue 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release
if ($release -lt 528040) { throw 'O projeto requer .NET Framework 4.8 ou superior.' }
$source = Join-Path $root 'src\Toccato.Desktop'
$exe = Join-Path $portable 'ToccatoDesktopManager.exe'
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Runtime.Serialization.dll', 'Microsoft.CSharp.dll')
$arguments = @('/nologo', '/optimize+', '/target:winexe', '/platform:anycpu', '/langversion:5', '/codepage:65001', "/out:$exe", "/win32manifest:$source\app.manifest", "/win32icon:$root\assets\tdm.ico")
foreach ($reference in $references) { $arguments += "/reference:$(Join-Path $framework $reference)" }
$arguments += @(Get-ChildItem $source -Filter '*.cs' | ForEach-Object FullName)
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do aplicativo.' }
Copy-Item (Join-Path $source 'App.config') "$exe.config" -Force
Copy-Item (Join-Path $root 'assets\tdm.ico') $portable -Force
Copy-Item (Join-Path $root 'docs\Guia_TDM_1_0.html') $portable -Force
Copy-Item (Join-Path $root 'docs\LEIA-ME.txt') $portable -Force

if (-not $SkipTests) {
    $testExe = Join-Path $out 'SecurityTests.exe'
    $testArgs = @('/nologo', '/optimize+', '/target:exe', '/langversion:5', '/codepage:65001', "/out:$testExe", "/reference:$exe", "/reference:$framework\System.Windows.Forms.dll", "/reference:$framework\System.Runtime.Serialization.dll", (Join-Path $root 'tests\SecurityTests.cs'))
    & $compiler $testArgs
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação dos testes.' }
    Copy-Item $exe (Join-Path $out 'ToccatoDesktopManager.exe') -Force
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Os testes de segurança falharam.' }
    $smokeExe = Join-Path $out 'UiSmokeTests.exe'
    & $compiler '/nologo' '/target:exe' '/langversion:5' '/codepage:65001' "/out:$smokeExe" "/reference:$exe" "/reference:$framework\System.Windows.Forms.dll" "/reference:$framework\System.Drawing.dll" (Join-Path $root 'tests\UiSmokeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do teste da interface.' }
    & $smokeExe
    if ($LASTEXITCODE -ne 0) { throw 'O teste da interface falhou.' }
}

$zip = Join-Path $out 'ToccatoDesktopManager-1.0.0-Portable.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$portable\*" -DestinationPath $zip
if (-not $SkipInstaller) {
    $nsis = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'
    if (-not (Test-Path $nsis)) { $command = Get-Command makensis.exe -ErrorAction SilentlyContinue; if ($command) { $nsis = $command.Source } }
    if (-not (Test-Path $nsis)) { throw 'Instale NSIS 3 para gerar o instalador, ou use -SkipInstaller para gerar somente o portátil.' }
    Push-Location (Join-Path $root 'installer')
    try { & $nsis "/DSOURCE=$portable" "/DOUTPUT=$out\ToccatoDesktopManager-1.0.0-Setup.exe" 'ToccatoDesktopManager.nsi'; if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' } }
    finally { Pop-Location }
}
$packages = @(Get-ChildItem $out -File | Where-Object { $_.Name -match '^ToccatoDesktopManager-1\.0\.0-(Setup\.exe|Portable\.zip)$' })
$hashes = $packages | ForEach-Object { "$( (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)" }
$hashes | Set-Content (Join-Path $out 'SHA256SUMS_1_0_0.txt') -Encoding ASCII
Write-Host "Pacotes criados em $out"
