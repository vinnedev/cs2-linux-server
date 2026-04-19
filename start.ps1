$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServerDir = Join-Path $RootDir "server"
$ComponentsDir = Join-Path $RootDir "components\csgo"
$CsgoDir = Join-Path $ServerDir "game\csgo"
$Cs2Exe = Join-Path $ServerDir "game\bin\win64\cs2.exe"

function Import-DotEnv {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    Write-Host "Carregando variaveis de $Path" -ForegroundColor Cyan

    foreach ($line in Get-Content $Path) {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#")) {
            continue
        }

        $parts = $trimmed -split "=", 2
        if ($parts.Count -ne 2) {
            continue
        }

        $name = $parts[0].Trim()
        $value = $parts[1].Trim()

        if (
            ($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))
        ) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

function Resolve-CS2Root {
    param(
        [string]$CandidatePath
    )

    if (-not $CandidatePath) {
        return $null
    }

    $expandedPath = [Environment]::ExpandEnvironmentVariables($CandidatePath.Trim())
    if (-not (Test-Path $expandedPath)) {
        return $null
    }

    $fullPath = (Get-Item $expandedPath).FullName
    $parentPath = Split-Path -Parent $fullPath
    $grandParentPath = if ($parentPath) { Split-Path -Parent $parentPath } else { $null }
    $rootCandidates = @($fullPath, $parentPath, $grandParentPath) | Where-Object { $_ } | Select-Object -Unique

    foreach ($rootCandidate in $rootCandidates) {
        $gameInfoPath = Join-Path $rootCandidate "game\csgo\gameinfo.gi"
        $cs2Path = Join-Path $rootCandidate "game\bin\win64\cs2.exe"

        if ((Test-Path $gameInfoPath) -and (Test-Path $cs2Path)) {
            return (Get-Item $rootCandidate).FullName
        }
    }

    return $null
}

Import-DotEnv -Path (Join-Path $RootDir ".env")

if ($env:CS2_PATH) {
    $candidateServerDir = Resolve-CS2Root -CandidatePath $env:CS2_PATH
    if ($candidateServerDir) {
        $ServerDir = $candidateServerDir
        $CsgoDir = Join-Path $ServerDir "game\csgo"
        $Cs2Exe = Join-Path $ServerDir "game\bin\win64\cs2.exe"
    }
}

if (-not (Test-Path $Cs2Exe)) {
    Write-Host "ERRO: cs2.exe nao encontrado em $Cs2Exe" -ForegroundColor Red
    Write-Host "Execute .\install.ps1 primeiro ou defina CS2_PATH para uma instalacao valida." -ForegroundColor Red
    exit 1
}

$port = if ($env:PORT) { $env:PORT } else { "27015" }
$ip = if ($env:IP) { $env:IP } else { "0.0.0.0" }
$tickrate = if ($env:TICKRATE) { $env:TICKRATE } else { "64" }
$maxplayers = if ($env:MAXPLAYERS) { $env:MAXPLAYERS } else { "16" }
$apiKey = if ($env:API_KEY) { $env:API_KEY } else { "" }
$steamAccount = if ($env:STEAM_ACCOUNT) { $env:STEAM_ACCOUNT } else { "" }
$lan = if ($env:LAN) { $env:LAN } else { "0" }
$serverPassword = if ($env:SERVER_PASSWORD) { $env:SERVER_PASSWORD } else { "" }
$rconPassword = if ($env:RCON_PASSWORD) { $env:RCON_PASSWORD } else { "changeme" }
$execCfg = if ($env:EXEC) { $env:EXEC } else { "autoexec.cfg" }

Write-Host "===== Preparando arquivos do servidor =====" -ForegroundColor Cyan

$addonsDir = Join-Path $CsgoDir "addons"
$cfgSettingsDir = Join-Path $CsgoDir "cfg\settings"

if (Test-Path $addonsDir) {
    Remove-Item $addonsDir -Recurse -Force
}

if (Test-Path $cfgSettingsDir) {
    Remove-Item $cfgSettingsDir -Recurse -Force
}

Copy-Item "$ComponentsDir\*" $CsgoDir -Recurse -Force

$windowsAddons = Join-Path $ComponentsDir "addons\windows"
if (Test-Path $windowsAddons) {
    Copy-Item "$windowsAddons\*" $CsgoDir -Recurse -Force
}

Write-Host "Iniciando servidor CS2..." -ForegroundColor Green

$arguments = @(
    "-dedicated",
    "-console",
    "-usercon",
    "+game_type", "0",
    "+game_mode", "0",
    "+mapgroup", "mg_active",
    "+map", "de_mirage",
    "-port", $port,
    "-ip", $ip,
    "+net_public_adr", $ip,
    "-tickrate", $tickrate,
    "+sv_visiblemaxplayers", $maxplayers,
    "-authkey", $apiKey,
    "+sv_setsteamaccount", $steamAccount,
    "+sv_lan", $lan,
    "+sv_password", $serverPassword,
    "+rcon_password", $rconPassword,
    "+exec", $execCfg
)

& $Cs2Exe @arguments

Write-Host ""
Write-Host "CS2 foi encerrado." -ForegroundColor Yellow
