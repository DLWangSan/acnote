param(
    [string]$Version = '0.1.13',
    [string]$DotNet = 'dotnet',
    [string]$Iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    [string]$RuntimeInstaller = '',
    [string]$RuntimeInstallerX86 = '',
    [string]$UpdateSource = '',
    [string]$DownloadBase = 'https://github.com/DLWangSan/acnote/releases/download'
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have the form 0.1.0' }
$releaseRoot = Split-Path $PSScriptRoot
$output = Join-Path $releaseRoot "artifacts\$Version"
$publish = Join-Path $output 'publish'
$cache = Join-Path $releaseRoot '.tmp\packaging'
New-Item -ItemType Directory -Force $output,$cache | Out-Null
if (-not (Test-Path -LiteralPath $Iscc)) { throw 'Install Inno Setup 6 or pass -Iscc with the compiler path.' }
if (Test-Path -LiteralPath $publish) {
    $resolvedReleaseRoot = [System.IO.Path]::GetFullPath($releaseRoot).TrimEnd('\') + '\'
    $resolvedPublish = [System.IO.Path]::GetFullPath($publish)
    if (-not $resolvedPublish.StartsWith($resolvedReleaseRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $resolvedPublish.StartsWith([System.IO.Path]::GetFullPath((Join-Path $releaseRoot 'artifacts')).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clean a publish directory outside generated release artifacts.'
    }
    Remove-Item -LiteralPath $resolvedPublish -Recurse -Force
}
& $DotNet publish (Join-Path $releaseRoot 'src\AcNote.App\AcNote.App.csproj') -c Release -r win-x64 --self-contained false -m:1 -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
# The COM add-in is framework dependent and its build output must accompany the app.
Copy-Item (Join-Path $releaseRoot 'src\AcNote.WordAddin\bin\Release\net10.0-windows\win-x64') (Join-Path $publish 'addin') -Recurse
& $DotNet publish (Join-Path $releaseRoot 'src\AcNote.WordAddin\AcNote.WordAddin.csproj') -c Release -r win-x86 --self-contained false -m:1 -p:Version=$Version -o (Join-Path $publish 'addin-x86')
if ($LASTEXITCODE -ne 0) { throw '32-bit Word/WPS add-in publish failed' }
Get-ChildItem -Path $publish -Filter '*.pdb' -Recurse -File | Remove-Item -Force
foreach ($required in @('AcNote.App.exe','addin\AcNote.WordAddin.comhost.dll','addin\AcNote.WordAddin.runtimeconfig.json','addin-x86\AcNote.WordAddin.comhost.dll','addin-x86\AcNote.WordAddin.runtimeconfig.json','styles\gb-t-7714-2015-numeric.style')) {
    if (-not (Test-Path (Join-Path $publish $required))) { throw "Missing package file: $required" }
}
if ($UpdateSource) {
    if (-not $UpdateSource.StartsWith('https://')) { throw 'UpdateSource must use HTTPS' }
    @{ source=$UpdateSource } | ConvertTo-Json | Set-Content (Join-Path $publish 'updates.json') -Encoding utf8
}
if ($RuntimeInstaller) {
    $runtimePath = (Resolve-Path -LiteralPath $RuntimeInstaller).Path
} else {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $runtime = $metadata.releases[0].windowsdesktop.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' } | Select-Object -First 1
    $runtimePath = Join-Path $cache 'windowsdesktop-runtime-win-x64.exe'
    if (-not (Test-Path $runtimePath) -or (Get-FileHash $runtimePath -Algorithm SHA512).Hash -ne $runtime.hash) {
        Invoke-WebRequest $runtime.url -OutFile $runtimePath
    }
    if ((Get-FileHash $runtimePath -Algorithm SHA512).Hash -ne $runtime.hash) { throw 'Runtime checksum mismatch' }
}
if ((Get-AuthenticodeSignature $runtimePath).Status -ne 'Valid') { throw 'Invalid runtime signature' }
if ((Get-Item $runtimePath).VersionInfo.ProductVersion -notlike '10.0.*') { throw 'The supplied installer is not .NET 10 Desktop Runtime' }
Copy-Item -LiteralPath $runtimePath -Destination (Join-Path $output 'windowsdesktop-runtime-win-x64.exe') -Force
$runtimePathX86 = $RuntimeInstallerX86
if ($runtimePathX86) {
    $runtimePathX86 = (Resolve-Path -LiteralPath $runtimePathX86).Path
} else {
    $metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
    $runtimeX86 = $metadata.releases[0].windowsdesktop.files | Where-Object { $_.rid -eq 'win-x86' -and $_.name -like '*.exe' } | Select-Object -First 1
    $runtimePathX86 = Join-Path $cache 'windowsdesktop-runtime-win-x86.exe'
    if (-not (Test-Path $runtimePathX86) -or (Get-FileHash $runtimePathX86 -Algorithm SHA512).Hash -ne $runtimeX86.hash) {
        Invoke-WebRequest $runtimeX86.url -OutFile $runtimePathX86
    }
    if ((Get-FileHash $runtimePathX86 -Algorithm SHA512).Hash -ne $runtimeX86.hash) { throw 'x86 runtime checksum mismatch' }
}
if ((Get-AuthenticodeSignature $runtimePathX86).Status -ne 'Valid') { throw 'Invalid x86 runtime signature' }
if ((Get-Item $runtimePathX86).VersionInfo.ProductVersion -notlike '10.0.*') { throw 'The supplied x86 installer is not .NET 10 Desktop Runtime' }
Copy-Item -LiteralPath $runtimePathX86 -Destination (Join-Path $output 'windowsdesktop-runtime-win-x86.exe') -Force
& $Iscc "/DAppVersion=$Version" "/DPublishDir=$publish" "/DRuntimeInstaller=$runtimePath" "/DRuntimeInstallerX86=$runtimePathX86" "/DOutputPath=$output" (Join-Path $releaseRoot 'installer\AcNote.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$name = "AcNote-Setup-$Version-win-x64.exe"
$hash = (Get-FileHash (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeHash = (Get-FileHash (Join-Path $output 'windowsdesktop-runtime-win-x64.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
$runtimeHashX86 = (Get-FileHash (Join-Path $output 'windowsdesktop-runtime-win-x86.exe') -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name", "$runtimeHash  windowsdesktop-runtime-win-x64.exe", "$runtimeHashX86  windowsdesktop-runtime-win-x86.exe" | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ascii
@{ version=$Version; downloadUrl="$($DownloadBase.TrimEnd('/'))/v$Version/$name"; sha256=$hash; notes="AcNote $Version" } |
    ConvertTo-Json | Set-Content (Join-Path $output 'latest.json') -Encoding utf8
Write-Output "Release files: $output"
