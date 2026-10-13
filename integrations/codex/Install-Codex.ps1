param([string]$Codex = '')

$ErrorActionPreference = 'Stop'
$integrationRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appRoot = Split-Path -Parent $integrationRoot
$server = Join-Path $appRoot 'AcNote.Mcp.exe'
$sourceSkill = Join-Path $integrationRoot 'skills\acnote'
$codexRoot = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
$skillRoot = Join-Path $codexRoot 'skills'
$targetSkill = Join-Path $skillRoot 'acnote'

if (-not $Codex) {
    if ($env:CODEX_CLI_PATH -and (Test-Path -LiteralPath $env:CODEX_CLI_PATH)) {
        $Codex = $env:CODEX_CLI_PATH
    } else {
        $desktopBin = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
        $Codex = Get-ChildItem -LiteralPath $desktopBin -Filter codex.exe -Recurse -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $Codex -or -not (Test-Path -LiteralPath $Codex)) {
    $command = Get-Command codex -ErrorAction SilentlyContinue
    if ($command) { $Codex = $command.Source }
}
if (-not $Codex) { throw 'Codex CLI was not found. Install Codex Desktop or pass -Codex with the full codex.exe path.' }

if (-not (Test-Path -LiteralPath $server)) { throw "AcNote MCP server not found: $server" }
if (-not (Test-Path -LiteralPath (Join-Path $sourceSkill 'SKILL.md'))) { throw "AcNote skill not found: $sourceSkill" }

New-Item -ItemType Directory -Force -Path $skillRoot | Out-Null
if (Test-Path -LiteralPath $targetSkill) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($targetSkill)
    $resolvedSkillRoot = [System.IO.Path]::GetFullPath($skillRoot).TrimEnd('\') + '\'
    if (-not $resolvedTarget.StartsWith($resolvedSkillRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to replace a skill outside the Codex skills directory.'
    }
    Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
}
Copy-Item -LiteralPath $sourceSkill -Destination $targetSkill -Recurse

& $Codex mcp remove acnote 2>$null
& $Codex mcp add acnote -- $server
if ($LASTEXITCODE -ne 0) { throw 'Codex MCP registration failed.' }

Write-Host 'AcNote Skill and MCP server are installed for Codex.'
Write-Host 'Restart Codex, start AcNote, open a library, and open the target Word document.'
