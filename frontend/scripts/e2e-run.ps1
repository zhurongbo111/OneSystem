<#
.SYNOPSIS
  e2e 一键运行：每轮使用独立数据库（app_e2e_<时间戳>），跑完自动删除。

.DESCRIPTION
  流程：校验 5080 空闲 → 清理历史残留库 → 以本轮专用库启动后端（dev 下自动建库 + 迁移 + 种子）
        → 确保前端 dev 可用 → 运行 Playwright 全量用例 → 停止后端 → 删除本轮库。

  退出码：0 = 用例全通过；1 = Playwright 有用例失败；2 = 脚本级错误（端口被占、后端未就绪等）。

.PARAMETER KeepDatabase
  保留本轮数据库供排查。库名会记入历史清单，由下次运行开始时清理。

.PARAMETER SkipResidualCleanup
  跳过历史残留库清理（仅在排查脚本自身问题时使用）。

.PARAMETER BackendTimeoutSeconds
  等待后端就绪的超时秒数，默认 120。

.NOTES
  约定与失败处理见 specs/002-frontend-e2e/design.md §6。
  仅支持 Windows PowerShell（如需 Linux CI 需另写等价脚本）。
#>
[CmdletBinding()]
param(
    [switch]$KeepDatabase,
    [switch]$SkipResidualCleanup,
    [int]$BackendTimeoutSeconds = 120,
    [string]$DbPrefix = 'app_e2e_'
)

$ErrorActionPreference = 'Stop'

$scriptDir     = $PSScriptRoot                             # frontend/scripts
$frontendDir   = Split-Path -Parent $scriptDir             # frontend
$repoRoot      = Split-Path -Parent $frontendDir           # 仓库根
$backendDir    = Join-Path $repoRoot 'backend'
$apiProject    = 'src/App.Api'
$infraProject  = 'src/App.Infrastructure'
$settingsPath  = Join-Path $backendDir 'src/App.Api/appsettings.Development.json'
$historyFile   = Join-Path $scriptDir '.e2e-db-history'
$outputDir     = Join-Path $frontendDir 'test-results/e2e-run'
$backendHealth = 'http://localhost:5080/health'
$backendPort   = 5080
$frontendPort  = 5173

$startedAt = Get-Date

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# 监听指定端口的进程 id：Get-NetTCPConnection 为主，失败时退回 netstat 解析（两者都试，避免漏判占用）
function Get-PortListenerProcessId {
    param([int]$Port)
    $ids = @()
    $listening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listening) { $ids += $listening.OwningProcess }
    if ($ids.Count -eq 0) {
        foreach ($line in (netstat -ano | Select-String 'LISTENING' | Select-String ":$Port\s")) {
            $parts = @(($line.ToString() -split '\s+') | Where-Object { $_ -ne '' })
            if ($parts.Count -ge 5) { $ids += [int]$parts[$parts.Count - 1] }
        }
    }
    return @($ids | Sort-Object -Unique)
}

# 端口是否已被监听
function Test-PortInUse {
    param([int]$Port)
    return ((Get-PortListenerProcessId -Port $Port).Count -gt 0)
}

# 读取开发连接串：凭据只在 appsettings.Development.json 维护一处，脚本不重复维护
function Get-DevConnectionString {
    if (-not (Test-Path $settingsPath)) { throw "未找到后端开发配置：$settingsPath" }
    $json = Get-Content $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $connection = $json.ConnectionStrings.Default
    if ([string]::IsNullOrWhiteSpace($connection)) { throw "$settingsPath 未配置 ConnectionStrings:Default" }
    return $connection
}

# 仅替换连接串的 Database 段，得到本轮专用库的连接串
function New-ConnectionString {
    param([string]$BaseConnection, [string]$Database)
    if ($BaseConnection -notmatch '(?i)Database\s*=') { throw '连接串缺少 Database 段，无法切换数据库' }
    return ($BaseConnection -replace '(?i)Database\s*=\s*[^;]*', "Database=$Database")
}

# 用 dotnet ef 删除指定库，成功返回 $true。
# --no-build：避免与"仍在运行的其它后端"抢构建输出（Bin 被锁会让构建失败）；库不存在时返回非 0，按失败处理并由调用方兜底。
function Remove-E2eDatabase {
    param([string]$ConnectionString, [string]$Database)
    $previous = $env:ConnectionStrings__Default
    $env:ConnectionStrings__Default = $ConnectionString
    try {
        Push-Location $backendDir
        try {
            $output = & dotnet ef database drop --force --no-build --project $infraProject --startup-project $apiProject 2>&1
            $code = $LASTEXITCODE
        } finally {
            Pop-Location
        }
        if ($code -eq 0) {
            Write-Host "    已删除数据库 $Database"
            return $true
        }
        Write-Warning "    数据库 $Database 未删除：$($output | Select-Object -Last 1)"
        return $false
    } finally {
        $env:ConnectionStrings__Default = $previous
    }
}

# 轮询后端就绪：返回 ready / launcher-exited / timeout。
# launcher-exited 必须单独识别——端口被别的服务占用时 dotnet run 会立刻退出，
# 若只看健康检查会命中"别人的后端"（连的是开发库），用例会静默跑在错误数据集上。
function Wait-BackendReady {
    param([int]$TimeoutSeconds, $LauncherProcess)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($LauncherProcess -and $LauncherProcess.HasExited) { return 'launcher-exited' }
        Start-Sleep -Seconds 2
        try {
            $response = Invoke-WebRequest -Uri $backendHealth -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return 'ready' }
        } catch {
            # 未就绪：继续等待
        }
    }
    return 'timeout'
}

# 停止本次脚本启动的后端：taskkill 杀进程树（dotnet run 宿主 + App.Api 子进程），再等端口真正释放
function Stop-StartedBackend {
    param($LauncherProcess, [int]$Port)
    if ($LauncherProcess -and -not $LauncherProcess.HasExited) {
        & taskkill /PID $LauncherProcess.Id /T /F 2>&1 | Out-Null
    }
    # 兜底：仅停本次脚本开始后启动的 App.Api（避免误停手工启动的后端）
    Get-Process -Name 'App.Api' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.StartTime -ge $startedAt } catch { $false }
    } | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    # 端口未释放时删库必然失败（bin 仍被占用），先等
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-PortInUse -Port $Port)) { return }
        Start-Sleep -Seconds 1
    }
    Write-Warning "端口 $Port 在 15 秒内未释放，删库可能失败"
}

$exitCode = 1
$launcher = $null
$frontendLauncher = $null
$backendStarted = $false
$database = $DbPrefix + (Get-Date -Format 'yyyyMMddHHmmss')
$baseConnection = Get-DevConnectionString
$connection = New-ConnectionString -BaseConnection $baseConnection -Database $database

Write-Step "本轮数据库：$database"

try {
    # 1. 5080 必须空闲：占用者连的是开发库，静默复用会让用例打在错误的数据集上
    if (Test-PortInUse -Port $backendPort) {
        $holders = (Get-PortListenerProcessId -Port $backendPort) -join ', '
        throw "端口 $backendPort 已被占用（PID: $holders）。请先停止手工启动的后端（它连接的是开发库），再运行 e2e:run。"
    }

    # 2. 清理历史残留库（含 -KeepDatabase 保留的库与上一轮删除失败的库）
    if (-not $SkipResidualCleanup -and (Test-Path $historyFile)) {
        $history = @(Get-Content $historyFile -Encoding ascii | Where-Object { $_.Trim() -ne '' })
        if ($history.Count -gt 0) {
            Write-Step "清理历史残留库（$($history.Count) 个）"
            $remaining = @()
            foreach ($db in $history) {
                $name = $db.Trim()
                $ok = Remove-E2eDatabase -ConnectionString (New-ConnectionString -BaseConnection $baseConnection -Database $name) -Database $name
                if (-not $ok) { $remaining += $name }
            }
            Set-Content -Path $historyFile -Value $remaining -Encoding ascii
        } else {
            Set-Content -Path $historyFile -Value '' -Encoding ascii
        }
    }

    # 3. 启动后端（连接本轮库；库不存在时 dev 下自动建库 + 迁移 + 内置管理员种子）
    Write-Step "启动后端并等待就绪（库 $database）"
    $launcher = Start-Process -FilePath 'dotnet' `
        -ArgumentList "run --project $apiProject -- --ConnectionStrings:Default=`"$connection`"" `
        -WorkingDirectory $backendDir -PassThru -WindowStyle Hidden
    $backendStarted = $true
    $ready = Wait-BackendReady -TimeoutSeconds $BackendTimeoutSeconds -LauncherProcess $launcher
    if ($ready -eq 'launcher-exited') {
        throw "后端进程已退出（端口 $backendPort 被占用或启动失败），本轮终止。请确认端口空闲后重试。"
    }
    if ($ready -ne 'ready') {
        throw "后端在 $BackendTimeoutSeconds 秒内未就绪（$backendHealth）"
    }
    Write-Host "    后端已就绪：http://localhost:$backendPort"

    # 4. 前端 dev：未运行则由脚本启动（结束时一并停止），已在运行则复用
    if (-not (Test-PortInUse -Port $frontendPort)) {
        Write-Step "启动前端 dev（端口 $frontendPort）"
        $frontendLauncher = Start-Process -FilePath 'npm.cmd' -ArgumentList 'run', 'dev' `
            -WorkingDirectory $frontendDir -PassThru -WindowStyle Hidden
        $deadline = (Get-Date).AddSeconds(120)
        while ((Get-Date) -lt $deadline -and -not (Test-PortInUse -Port $frontendPort)) { Start-Sleep -Seconds 2 }
        if (-not (Test-PortInUse -Port $frontendPort)) { throw "前端 dev 在 120 秒内未就绪（端口 $frontendPort）" }
    } else {
        Write-Host "    复用已在运行的前端 dev（端口 $frontendPort）"
    }

    # 5. 运行用例：产物固定落 test-results/e2e-run，避免与手工运行互相覆盖
    Write-Step '运行 Playwright 全量用例'
    if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue }
    Push-Location $frontendDir
    try {
        & npx playwright test --output=$outputDir
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
} catch {
    Write-Host "错误：$($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 2
} finally {
    # 6. 清理：先停后端（否则删库会被构建锁挡住），再处理数据库
    if ($backendStarted) {
        Write-Step '清理现场'
        Stop-StartedBackend -LauncherProcess $launcher -Port $backendPort
        if ($frontendLauncher -and -not $frontendLauncher.HasExited) {
            Stop-Process -Id $frontendLauncher.Id -Force -ErrorAction SilentlyContinue
        }

        if ($KeepDatabase) {
            Write-Warning "已保留数据库 $database（下次运行 e2e:run 时自动清理）"
            Add-Content -Path $historyFile -Value $database -Encoding ascii
        } else {
            $deleted = Remove-E2eDatabase -ConnectionString $connection -Database $database
            if (-not $deleted) {
                # 兜底：删不掉就记进清单，避免无声残留（下次运行开始时清理）
                Add-Content -Path $historyFile -Value $database -Encoding ascii
                Write-Warning "本轮库 $database 已记入历史清单，下次运行 e2e:run 时自动清理"
            }
        }
    }
    Write-Step "完成（退出码 $exitCode：0=全部通过 / 1=有用例失败 / 2=脚本错误）"
}

exit $exitCode
