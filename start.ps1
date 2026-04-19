$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServerDir = Join-Path $RootDir "server"
$ComponentsDir = Join-Path $RootDir "components\csgo"
$CsgoDir = Join-Path $ServerDir "game\csgo"
$Cs2Exe = Join-Path $ServerDir "game\bin\win64\cs2.exe"
$Cs2WorkingDir = Join-Path $ServerDir "game"
$LogsDir = Join-Path $ServerDir "logs"

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

if ((-not (Test-Path $Cs2Exe)) -and $env:CS2_PATH) {
    $candidateServerDir = Resolve-CS2Root -CandidatePath $env:CS2_PATH
    if ($candidateServerDir) {
        $ServerDir = $candidateServerDir
        $CsgoDir = Join-Path $ServerDir "game\csgo"
        $Cs2Exe = Join-Path $ServerDir "game\bin\win64\cs2.exe"
        $Cs2WorkingDir = Join-Path $ServerDir "game"
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
Write-Host "Runtime local : $ServerDir" -ForegroundColor DarkGray
Write-Host "Executavel    : $Cs2Exe" -ForegroundColor DarkGray
Write-Host "Working dir   : $Cs2WorkingDir" -ForegroundColor DarkGray

New-Item -ItemType Directory -Force -Path $LogsDir | Out-Null

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

function Add-ArgumentPair {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Key,
        [string]$Value,
        [switch]$AllowEmpty
    )

    if ($AllowEmpty -or -not [string]::IsNullOrWhiteSpace($Value)) {
        $List.Add($Key)
        if ($null -ne $Value) {
            $List.Add($Value)
        }
    }
}

$arguments = [System.Collections.Generic.List[string]]::new()
$baseArguments = @(
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
    "+sv_lan", $lan,
    "+rcon_password", $rconPassword,
    "+exec", $execCfg
)

foreach ($argument in $baseArguments) {
    $arguments.Add($argument)
}

Add-ArgumentPair -List $arguments -Key "-authkey" -Value $apiKey
Add-ArgumentPair -List $arguments -Key "+sv_setsteamaccount" -Value $steamAccount
Add-ArgumentPair -List $arguments -Key "+sv_password" -Value $serverPassword

$quotedArguments = $arguments | ForEach-Object {
    if ($_ -match '\s') { '"{0}"' -f $_ } else { $_ }
}

if (Test-Path $ServerDir) {
    $LogsDir = Join-Path $ServerDir "logs"
    New-Item -ItemType Directory -Force -Path $LogsDir | Out-Null
}

$stdoutLog = Join-Path $LogsDir "cs2-stdout.log"
$stderrLog = Join-Path $LogsDir "cs2-stderr.log"

if (Test-Path $stdoutLog) { Remove-Item $stdoutLog -Force }
if (Test-Path $stderrLog) { Remove-Item $stderrLog -Force }

Write-Host "Argumentos    : $($quotedArguments -join ' ')" -ForegroundColor DarkGray
Write-Host "Stdout log    : $stdoutLog" -ForegroundColor DarkGray
Write-Host "Stderr log    : $stderrLog" -ForegroundColor DarkGray

$process = Start-Process -FilePath $Cs2Exe -ArgumentList $arguments -WorkingDirectory $Cs2WorkingDir -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog -Wait -PassThru
$exitCode = $process.ExitCode

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "CS2 foi encerrado. ExitCode=0" -ForegroundColor Yellow
} else {
    Write-Host "CS2 encerrou com falha. ExitCode=$exitCode" -ForegroundColor Red
}

if ((Test-Path $stderrLog) -and ((Get-Item $stderrLog).Length -gt 0)) {
    Write-Host "Ultimas linhas de stderr:" -ForegroundColor Yellow
    Get-Content $stderrLog | Select-Object -Last 20
}
