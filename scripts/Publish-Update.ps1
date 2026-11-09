# Run with PowerShell 7. The publishing credential is never included in client packages.
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$Version,
    [string]$Notes = '',
    [int]$AppId = 2
)
$ErrorActionPreference = 'Stop'
if (-not $env:update_api_key) { throw 'Set update_api_key in the environment before publishing.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a stable version such as 0.1.19.' }
$file = Get-Item -LiteralPath $Installer
$hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$headers = @{ Authorization = 'Bearer ' + $env:update_api_key }
$uploaded = Invoke-RestMethod 'https://update.ygs.plus/api/admin/upload' -Method Post -Headers $headers -Form @{file=$file}
if ($uploaded.ok -eq $false) { throw 'Installer upload was rejected.' }
$asset = $uploaded
if ($uploaded.file) { $asset = $uploaded.file }
elseif ($uploaded.upload) { $asset = $uploaded.upload }
if (-not $asset.download_url -or $asset.sha256 -ne $hash -or [long]$asset.file_size -ne $file.Length) {
    throw 'Uploaded installer metadata does not match the local file; no release was published.'
}
$body = @{
    app_id=$AppId; platform='windows'; version=$Version; mandatory=$false; notes=$Notes
    download_url=$asset.download_url; file_name=$asset.file_name; file_size=$asset.file_size; sha256=$hash
} | ConvertTo-Json
$published = Invoke-RestMethod 'https://update.ygs.plus/api/admin/releases' -Method Post -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
if ($published.ok -eq $false) { throw 'Release publication was rejected.' }
$latest = Invoke-RestMethod 'https://update.ygs.plus/api/v1/apps/acnote/latest?platform=windows'
if ($latest.release.version -ne $Version -or $latest.release.sha256 -ne $hash) { throw 'Published release verification failed.' }
Write-Output "Published AcNote $Version to update.ygs.plus (SHA256 $hash)"
