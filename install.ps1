$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$SteamCmdDir = Join-Path $RootDir "steamcmd"
$SteamCmdExe = Join-Path $SteamCmdDir "steamcmd.exe"
$ServerDir = Join-Path $RootDir "server"
$GameInfoPath = Join-Path $ServerDir "game\csgo\gameinfo.gi"

$SearchString = "Game`tcsgo/addons/metamod"
$InsertAfter = "Game_LowViolence`tcsgo_lv"

function Import-DotEnv {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

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

    $resolvedCandidate = [Environment]::ExpandEnvironmentVariables($CandidatePath.Trim())
    if (-not (Test-Path $resolvedCandidate)) {
        return $null
    }

    $candidateItem = Get-Item $resolvedCandidate
    $fullPath = $candidateItem.FullName
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

function Get-SteamLibraryRoots {
    $libraries = [System.Collections.Generic.List[string]]::new()
    $registryKeys = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )

    foreach ($key in $registryKeys) {
        if (-not (Test-Path $key)) {
            continue
        }

        $steamPath = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).SteamPath
        if ($steamPath) {
            $libraries.Add($steamPath)
        }
    }

    $discovered = [System.Collections.Generic.List[string]]::new()
    foreach ($library in $libraries) {
        if (-not $library) {
            continue
        }

        $expandedLibrary = [Environment]::ExpandEnvironmentVariables($library)
        if (-not (Test-Path $expandedLibrary)) {
            continue
        }

        $fullLibraryPath = (Get-Item $expandedLibrary).FullName
        if (-not $discovered.Contains($fullLibraryPath)) {
            $discovered.Add($fullLibraryPath)
        }

        $libraryFoldersPath = Join-Path $fullLibraryPath "steamapps\libraryfolders.vdf"
        if (-not (Test-Path $libraryFoldersPath)) {
            continue
        }

        foreach ($line in Get-Content $libraryFoldersPath) {
            if ($line -match '"path"\s+"([^"]+)"') {
                $parsedPath = $matches[1] -replace "\\\\", "\"
                if ((Test-Path $parsedPath) -and (-not $discovered.Contains((Get-Item $parsedPath).FullName))) {
                    $discovered.Add((Get-Item $parsedPath).FullName)
                }
            }
        }
    }

    return $discovered
}

function Find-ExistingCS2Install {
    $candidatePaths = [System.Collections.Generic.List[string]]::new()

    if ($env:CS2_PATH) {
        $candidatePaths.Add($env:CS2_PATH)
    }

    $candidatePaths.Add($ServerDir)

    foreach ($libraryRoot in Get-SteamLibraryRoots) {
        $candidatePaths.Add((Join-Path $libraryRoot "steamapps\common\Counter-Strike Global Offensive"))
    }

    foreach ($candidate in $candidatePaths) {
        $resolved = Resolve-CS2Root -CandidatePath $candidate
        if ($resolved) {
            return $resolved
        }
    }

    return $null
}

function Ensure-ServerJunction {
    param(
        [string]$TargetPath
    )

    $resolvedTarget = (Get-Item $TargetPath).FullName

    if (Test-Path $ServerDir) {
        $serverItem = Get-Item $ServerDir -Force
        $serverResolved = $serverItem.FullName

        if ($serverResolved -eq $resolvedTarget) {
            return
        }

        if ($serverItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            cmd /c rmdir $ServerDir | Out-Null
        } else {
            Write-Host "AVISO: pasta server/ ja existe e nao e um link. Reaproveitando ela como destino ativo." -ForegroundColor Yellow
            return
        }
    }

    if (-not (Test-Path $ServerDir)) {
        cmd /c mklink /J $ServerDir $resolvedTarget | Out-Null
        Write-Host "Junction criado: server -> $resolvedTarget" -ForegroundColor Green
    }
}

Import-DotEnv -Path (Join-Path $RootDir ".env")

# ===== [1/4] SteamCMD =====
Write-Host "===== [1/4] Instalando SteamCMD (se necessario) =====" -ForegroundColor Cyan

if (-not (Test-Path $SteamCmdExe)) {
    New-Item -ItemType Directory -Force -Path $SteamCmdDir | Out-Null
    $zipPath = Join-Path $SteamCmdDir "steamcmd.zip"
    Invoke-WebRequest -Uri "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $SteamCmdDir -Force
    Remove-Item $zipPath
    Write-Host "SteamCMD instalado." -ForegroundColor Green
} else {
    Write-Host "SteamCMD ja instalado." -ForegroundColor Green
}

# ===== [2/4] CS2 Dedicated Server =====
Write-Host "===== [2/4] Instalando/Atualizando CS2 (appid 730) =====" -ForegroundColor Cyan

$existingInstall = Find-ExistingCS2Install
if ($existingInstall) {
    Write-Host "Usando instalacao CS2 existente: $existingInstall" -ForegroundColor Yellow
    Ensure-ServerJunction -TargetPath $existingInstall
} else {
    Write-Host "Nenhuma instalacao existente encontrada. Baixando via SteamCMD..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $ServerDir | Out-Null
    & $SteamCmdExe +force_install_dir $ServerDir +login anonymous +app_update 730 validate +quit
}

# ===== [3/4] Patch gameinfo.gi =====
Write-Host "===== [3/4] Patch de gameinfo.gi =====" -ForegroundColor Cyan

if (-not (Test-Path $GameInfoPath)) {
    Write-Host "ERRO: gameinfo.gi nao encontrado em $GameInfoPath" -ForegroundColor Red
    exit 1
}

$bakFile = "$GameInfoPath.bak"
if (-not (Test-Path $bakFile)) {
    Copy-Item $GameInfoPath $bakFile
    Write-Host "Backup criado: $bakFile" -ForegroundColor Green
}

$content = Get-Content $GameInfoPath -Raw
if ($content -match "csgo/addons/metamod") {
    Write-Host "Patch ja aplicado." -ForegroundColor Green
} else {
    Write-Host "Aplicando patch no gameinfo.gi..." -ForegroundColor Yellow
    $lines = Get-Content $GameInfoPath
    $newLines = @()
    foreach ($line in $lines) {
        $newLines += $line
        if ($line -match "Game_LowViolence\s+csgo_lv") {
            $newLines += "`t`t`tGame`tcsgo/addons/metamod"
        }
    }
    $newLines | Set-Content $GameInfoPath -Encoding UTF8
    Write-Host "Patch aplicado." -ForegroundColor Green
}

# ===== [4/4] Deploy components =====
Write-Host "===== [4/4] Preparando arquivos do servidor =====" -ForegroundColor Cyan

$csgoDir = Join-Path $ServerDir "game\csgo"
$addonsDir = Join-Path $csgoDir "addons"
$cfgSettingsDir = Join-Path $csgoDir "cfg\settings"

if (Test-Path $addonsDir) { Remove-Item $addonsDir -Recurse -Force }
if (Test-Path $cfgSettingsDir) { Remove-Item $cfgSettingsDir -Recurse -Force }

$componentsDir = Join-Path $RootDir "components\csgo"
Copy-Item "$componentsDir\*" $csgoDir -Recurse -Force

$windowsAddons = Join-Path $componentsDir "addons\windows"
if (Test-Path $windowsAddons) {
    Copy-Item "$windowsAddons\*" $csgoDir -Recurse -Force
    Write-Host "Addons Windows aplicados." -ForegroundColor Green
}

Write-Host ""
Write-Host "Setup concluido. Para iniciar: .\start.ps1" -ForegroundColor Green
