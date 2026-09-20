<#
.SYNOPSIS
  e2e 一键运行：每轮使用独立数据库（app_e2e_<时间戳>），跑完自动删除。

.DESCRIPTION
  流程：校验 5080 空闲 → 清理历史残留库 → 以本轮专用库启动后端（dev 下自动建库 + 迁移 + 种子）
        → 确保前端 dev 可用 → 运行 Playwright 全量用例 → 停止后端 → 删除本轮库。

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

# 端口是否已被监听
function Test-PortInUse {
    param([int]$Port)
    $listening = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return [bool]$listening
}

# 用 dotnet ef 删除指定库；库不存在时 dotnet ef 返回非 0，按正常情况处理
function Remove-E2eDatabase {
    param([string]$ConnectionString, [string]$Database)
    $previous = $env:ConnectionStrings__Default
    $env:ConnectionStrings__Default = $ConnectionString
    try {
        Push-Location $backendDir
        try {
            $output = & dotnet ef database drop --force --project $infraProject --startup-project $apiProject 2>&1
            $code = $LASTEXITCODE
        } finally {
            Pop-Location
        }
        if ($code -eq 0) {
            Write-Host "    已删除数据库 $Database"
        } else {
            Write-Warning "    数据库 $Database 未删除（可能本就不存在）：$($output | Select-Object -Last 1)"
        }
    } finally {
        $env:ConnectionStrings__Default = $previous
    }
}

# 轮询后端健康检查直到就绪
function Wait-BackendReady {
    param([int]$TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        try {
            $response = Invoke-WebRequest -Uri $backendHealth -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $true }
        } catch {
            # 未就绪：继续等待
        }
    }
    return $false
}

# 停止本次脚本启动的后端（dotnet run 宿主 + 其 App.Api 子进程）
function Stop-StartedBackend {
    param($LauncherProcess)
    if ($LauncherProcess -and -not $LauncherProcess.HasExited) {
        Stop-Process -Id $LauncherProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-Process -Name 'App.Api' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.StartTime -ge $startedAt } catch { $false }
    } | ForEach-Object {
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
}

$exitCode = 1
$launcher = $null
$frontendLauncher = $null
$database = $DbPrefix + (Get-Date -Format 'yyyyMMddHHmmss')
$baseConnection = Get-DevConnectionString
$connection = New-ConnectionString -BaseConnection $baseConnection -Database $database

Write-Step "本轮数据库：$database"

try {
    # 1. 5080 必须空闲：占用者连的是开发库，静默复用会让用例打在错误的数据集上
    if (Test-PortInUse $backendPort) {
        throw "端口 $backendPort 已被占用。请先停止手工启动的后端（它连接的是开发库），再运行 e2e:run。"
    }

    # 2. 清理历史残留库（-KeepDatabase 留下的库在下一次运行时被清掉）
    if (-not $SkipResidualCleanup -and (Test-Path $historyFile)) {
        $history = @(Get-Content $historyFile -Encoding ascii | Where-Object { $_.Trim() -ne '' })
        if ($history.Count -gt 0) {
            Write-Step "清理历史残留库（$($history.Count) 个）"
            foreach ($db in $history) {
                $name = $db.Trim()
                Remove-E2eDatabase -ConnectionString (New-ConnectionString -BaseConnection $baseConnection -Database $name) -Database $name
            }
        }
        Set-Content -Path $historyFile -Value '' -Encoding ascii
    }

    # 3. 启动后端（连接本轮库；库不存在时 dev 下自动建库 + 迁移 + 内置管理员种子）
    Write-Step "启动后端并等待就绪（库 $database）"
    $launcher = Start-Process -FilePath 'dotnet' `
        -ArgumentList "run --project $apiProject -- --ConnectionStrings:Default=`"$connection`"" `
        -WorkingDirectory $backendDir -PassThru -WindowStyle Hidden
    if (-not (Wait-BackendReady -TimeoutSeconds $BackendTimeoutSeconds)) {
        throw "后端在 $BackendTimeoutSeconds 秒内未就绪（$backendHealth）"
    }
    Write-Host "    后端已就绪：http://localhost:$backendPort"

    # 4. 前端 dev：未运行则由脚本启动（结束时一并停止），已在运行则复用
    if (-not (Test-PortInUse $frontendPort)) {
        Write-Step "启动前端 dev（端口 $frontendPort）"
        $frontendLauncher = Start-Process -FilePath 'npm.cmd' -ArgumentList 'run', 'dev' `
            -WorkingDirectory $frontendDir -PassThru -WindowStyle Hidden
        $deadline = (Get-Date).AddSeconds(120)
        while ((Get-Date) -lt $deadline -and -not (Test-PortInUse $frontendPort)) { Start-Sleep -Seconds 2 }
        if (-not (Test-PortInUse $frontendPort)) { throw "前端 dev 在 120 秒内未就绪（端口 $frontendPort）" }
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
} finally {
    # 6. 清理：先停后端（否则 dotnet ef 无法构建），再处理数据库
    Write-Step '清理现场'
    Stop-StartedBackend -LauncherProcess $launcher
    if ($frontendLauncher -and -not $frontendLauncher.HasExited) {
        Stop-Process -Id $frontendLauncher.Id -Force -ErrorAction SilentlyContinue
    }

    if ($KeepDatabase) {
        Write-Warning "已保留数据库 $database（下次运行 e2e:run 时自动清理）"
        Add-Content -Path $historyFile -Value $database -Encoding ascii
    } else {
        Remove-E2eDatabase -ConnectionString $connection -Database $database
    }
    Write-Step "完成（Playwright 退出码 $exitCode）"
}

exit $exitCode
