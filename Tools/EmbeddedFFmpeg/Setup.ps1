param([string]$SdkDirectory)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$work = Join-Path $repo 'Temp/EmbeddedFFmpeg'
$output = Join-Path $repo 'Assets/Plugins/x86_64/EmbeddedFFmpeg'
New-Item -ItemType Directory -Force $work,$output | Out-Null
if (!$SdkDirectory) {
    $archive = Join-Path $work 'ffmpeg-pinned.zip'
    $url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-11-01-12-49/ffmpeg-n8.1.3-29-gad3b795b72-win64-lgpl-shared-8.1.zip'
    $expected = '786b43f77ea260af71fbb1bcdaabd43df83492122fb5575027f659d490d30b37'
    if (!(Test-Path -LiteralPath $archive)) { Invoke-WebRequest $url -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        throw 'FFmpeg SDK checksum mismatch. Remove the archive and retry.'
    }
    $unpacked = Join-Path $work 'pinned-sdk'
    Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
    $SdkDirectory = (Get-ChildItem -LiteralPath $unpacked -Directory | Select-Object -First 1).FullName
}
$SdkDirectory = (Resolve-Path -LiteralPath $SdkDirectory).Path
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'Install Visual Studio C++ desktop build tools first.' }
$devcmd = Join-Path $vs 'Common7/Tools/VsDevCmd.bat'
$source = Join-Path $PSScriptRoot 'opensh2_bink.c'
$nativeOutput = Join-Path $output 'OpenSH2.Bink.dll'
$object = Join-Path $work 'opensh2_bink.obj'
$build = Join-Path $work 'build.cmd'
@"
@echo off
call "$devcmd" -arch=x64 -host_arch=x64
if errorlevel 1 exit /b 1
cl /nologo /O2 /MD /LD /std:c11 /W4 /I"$SdkDirectory/include" "$source" /Fo"$object" /link /OUT:"$nativeOutput" /IMPLIB:"$work/OpenSH2.Bink.lib" /LIBPATH:"$SdkDirectory/lib" avformat.lib avcodec.lib avutil.lib swscale.lib swresample.lib
"@ | Set-Content -LiteralPath $build -Encoding ascii
& $env:ComSpec /c $build
if ($LASTEXITCODE -ne 0) { throw 'Native Bink bridge build failed.' }
foreach ($name in @('avcodec-62.dll','avformat-62.dll','avutil-60.dll','swscale-9.dll','swresample-6.dll')) {
    Copy-Item -LiteralPath (Join-Path $SdkDirectory "bin/$name") -Destination $output -Force
}
Copy-Item -LiteralPath (Join-Path $SdkDirectory 'LICENSE.txt') -Destination (Join-Path $output 'FFmpeg-LICENSE.txt') -Force
# Do not load these native DLLs on unsupported editor/player architectures.
foreach ($dll in Get-ChildItem -LiteralPath $output -Filter '*.dll') {
    $meta = $dll.FullName + '.meta'
    $guid = [guid]::NewGuid().ToString('N')
    if (Test-Path -LiteralPath $meta) {
        $line = Get-Content -LiteralPath $meta | Where-Object { $_ -match '^guid: ' } | Select-Object -First 1
        if ($line) { $guid = $line.Substring(6) }
    }
@"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Editor:
      enabled: 1
      settings:
        CPU: x86_64
        DefaultValueInitialized: true
        OS: Windows
    Win64:
      enabled: 1
      settings:
        CPU: x86_64
    Win:
      enabled: 0
      settings:
        CPU: None
    Linux64:
      enabled: 0
      settings:
        CPU: None
    OSXUniversal:
      enabled: 0
      settings:
        CPU: None
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@ | Set-Content -LiteralPath $meta -Encoding ascii
}
Write-Output "Embedded FFmpeg runtime ready: $output"
