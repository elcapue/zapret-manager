# Ставит свежую локальную сборку поверх установленной копии — для тестирования.
# Установщик внутри программы сравнивает только номер версии, а у тестовых сборок он одинаковый.
# Запускать после tools/publish.ps1. Работающий менеджер закрывается сам, новый запускается
# с запросом UAC. Данные установленной копии (runtime, настройки) не трогаются.

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$source = Join-Path $projectRoot "artifacts\ZapretManager\win-x64\Zapret Manager.exe"
$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\Zapret Manager"
$target = Join-Path $installDirectory "Zapret Manager.exe"

if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Сначала соберите exe: tools/publish.ps1"
}

if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    # Ещё не установлено — обычный путь: первый запуск предложит установку.
    Start-Process -FilePath $source
    return
}

# Работающий менеджер запущен с правами администратора, убить его отсюда нельзя. Просим его выйти
# штатно тем же сигналом, что и удаление программы (SingleInstanceService, pipe привязан к папке).
function Request-ManagerExit {
    $normalized = [System.IO.Path]::GetFullPath($installDirectory).TrimEnd('\', '/').ToUpperInvariant()
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normalized))
    $pipeName = "ZapretManager.SingleInstance." + ([System.BitConverter]::ToString($hash) -replace '-', '')
    $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, [System.IO.Pipes.PipeDirection]::Out)
    try {
        $client.Connect(2000)
        $writer = New-Object System.IO.StreamWriter($client)
        $writer.WriteLine('exit')
        $writer.Flush()
        return $true
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

if (Get-Process -Name "Zapret Manager" -ErrorAction SilentlyContinue) {
    if (Request-ManagerExit) {
        Write-Host "Zapret Manager закрывается..."
    }
    else {
        Write-Host "Не удалось попросить Zapret Manager закрыться. Выйдите через меню трея -> Выход."
    }
}

$deadline = (Get-Date).AddMinutes(2)
while (Get-Process -Name "Zapret Manager" -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) {
        throw "Zapret Manager не закрылся (возможно, не смог остановить zapret). Выйдите через меню трея и запустите скрипт снова."
    }

    Start-Sleep -Milliseconds 500
}

Copy-Item -LiteralPath $source -Destination $target -Force
$version = (Get-Item -LiteralPath $target).VersionInfo.ProductVersion
Write-Host "Установлена сборка $version"
Start-Process -FilePath $target
