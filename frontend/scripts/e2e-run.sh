#!/usr/bin/env bash
#
# e2e 一键运行（Linux / macOS 等价实现；Windows 见 e2e-run.ps1，两端由 e2e-run.mjs 分发）：
# 每轮使用独立数据库（app_e2e_<时间戳>），跑完自动删除。
#
# 流程：校验 5080 空闲 → 清理历史残留库 → 以本轮专用库启动后端（自动建库 + 迁移 + 种子）
#       → 确保前端 dev 可用 → 运行 Playwright 全量用例 → 停止后端 → 删除本轮库。
#
# 退出码：0 = 用例全通过；1 = Playwright 有用例失败；2 = 脚本级错误（端口被占、后端未就绪等）。
#
# 参数（与 e2e-run.ps1 同名，便于两端使用同一组过滤参数）：
#   -Spec <文件>                只跑指定用例文件（可重复，路径相对 e2e/，如 e2e/login.spec.ts）
#   -Grep <关键字>              按用例标题过滤（playwright --grep）
#   -KeepDatabase               保留本轮数据库供排查（库名记入历史清单，下次运行开始时清理）
#   -SkipResidualCleanup        跳过历史残留库清理（仅排查脚本自身问题时使用）
#   -ReuseFrontend              复用已在运行的前端 dev（默认先停后起：陈旧 dev server 会产生假失败）
#   -BackendTimeoutSeconds <秒> 等待后端就绪的超时，默认 120
#   -DbPrefix <前缀>            本轮库名前缀，默认 app_e2e_
#
# 约定与失败处理见 specs/002-frontend-e2e/design.md §6；CI 运行方式见其 §6.4。

# 关掉调用方可能带来的 errexit（CI 步骤以 `bash -e` 执行，errexit 会经 SHELLOPTS 传到本脚本：
# 那样首个非致命失败就会让脚本中途退出、连 finally 清理都跑不完），失败一律由本脚本显式判定。
set +e
set -uo pipefail

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)   # frontend/scripts
frontend_dir=$(dirname "$script_dir")                      # frontend
repo_root=$(dirname "$frontend_dir")                       # 仓库根
backend_dir="$repo_root/backend"
api_project='src/App.Api'
infra_project='src/App.Infrastructure'
settings_path="$backend_dir/src/App.Api/appsettings.Development.json"
history_file="$script_dir/.e2e-db-history"
output_dir="$frontend_dir/test-results/e2e-run"
log_dir="$frontend_dir/test-results/e2e-run-logs"
backend_health="http://localhost:5080/health"
backend_port=5080
frontend_port=5173

# dotnet ef 为全局工具，非交互 shell 下不一定在 PATH 中
[ -d "$HOME/.dotnet/tools" ] && PATH="$HOME/.dotnet/tools:$PATH"

exit_code=1
backend_started=0
backend_pid=''
frontend_pid=''
spawned_pid=''
keep_database=0
skip_residual=0
reuse_frontend=0
backend_timeout=120
db_prefix='app_e2e_'
specs=()
grep_text=''

log_info()  { printf '%s\n' "$*"; }
log_warn()  { printf '警告：%s\n' "$*" >&2; }
write_step() { printf '==> %s\n' "$*"; }

usage() {
    cat <<'EOF'
e2e 一键运行（每轮独立数据库 app_e2e_<时间戳>，跑完自动删除）

用法：npm run e2e:run [-- <参数>]（Windows 走 e2e-run.ps1，Linux / macOS 走 e2e-run.sh）

  -Spec <文件>                只跑指定用例文件（可重复，路径相对 e2e/）
  -Grep <关键字>              按用例标题过滤（playwright --grep）
  -KeepDatabase               保留本轮数据库供排查（库名记入历史清单，下次运行开始时清理）
  -SkipResidualCleanup        跳过历史残留库清理（仅排查脚本自身问题时使用）
  -ReuseFrontend              复用已在运行的前端 dev（默认先停后起）
  -BackendTimeoutSeconds <秒> 等待后端就绪的超时，默认 120
  -DbPrefix <前缀>            本轮库名前缀，默认 app_e2e_

退出码：0 = 用例全通过；1 = 有用例失败；2 = 脚本级错误。
EOF
}

abort() {
    printf '错误：%s\n' "$1" >&2
    exit_code=2
    exit 2
}

# 解析参数：与本脚本头部说明一致（未知参数直接报错，避免静默跑成全量）
parse_args() {
    while [ $# -gt 0 ]; do
        case "$1" in
            -Spec)
                [ $# -ge 2 ] || abort '-Spec 缺少取值'
                specs+=("$2"); shift 2 ;;
            -Grep)
                [ $# -ge 2 ] || abort '-Grep 缺少取值'
                grep_text="$2"; shift 2 ;;
            -KeepDatabase) keep_database=1; shift ;;
            -SkipResidualCleanup) skip_residual=1; shift ;;
            -ReuseFrontend) reuse_frontend=1; shift ;;
            -BackendTimeoutSeconds)
                [ $# -ge 2 ] || abort '-BackendTimeoutSeconds 缺少取值'
                backend_timeout="$2"; shift 2 ;;
            -DbPrefix)
                [ $# -ge 2 ] || abort '-DbPrefix 缺少取值'
                db_prefix="$2"; shift 2 ;;
            -h|--help) usage; exit 0 ;;
            *) abort "未知参数：$1（用 -h 查看用法）" ;;
        esac
    done
}

check_prerequisites() {
    local cmd
    for cmd in dotnet node npm npx; do
        command -v "$cmd" >/dev/null 2>&1 || abort "未找到命令 $cmd，请先安装后重试"
    done
}

# 监听指定端口的进程 id：ss 为主，lsof 兜底（macOS 无 ss；两者都试，避免漏判占用）
port_listener_pids() {
    local port="$1" pids=''
    if command -v ss >/dev/null 2>&1; then
        pids=$(ss -ltnp "sport = :$port" 2>/dev/null | grep -oE 'pid=[0-9]+' | cut -d= -f2)
    fi
    if [ -z "$pids" ] && command -v lsof >/dev/null 2>&1; then
        pids=$(lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null)
    fi
    [ -n "$pids" ] && printf '%s\n' $pids | sort -u
}

# 端口是否已被监听（探测手段缺失时退化为直接建连）
port_in_use() {
    local port="$1"
    if command -v ss >/dev/null 2>&1; then
        ss -ltn "sport = :$port" 2>/dev/null | grep -q LISTEN && return 0
    fi
    if command -v lsof >/dev/null 2>&1; then
        lsof -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1 && return 0
    fi
    (exec 3<>"/dev/tcp/127.0.0.1/$port") >/dev/null 2>&1
}

process_cmdline() {
    local pid="$1"
    if [ -r "/proc/$pid/cmdline" ]; then
        tr '\0' ' ' < "/proc/$pid/cmdline"
    else
        ps -o command= -p "$pid" 2>/dev/null
    fi
}

wait_port_released() {
    local port="$1" timeout="$2"
    # 注意：同一 local 语句里的展开先于赋值（set -u 下引用同句的局部变量会报 unbound），故分两句写
    local deadline=$((SECONDS + timeout))
    while [ "$SECONDS" -lt "$deadline" ]; do
        port_in_use "$port" || return 0
        sleep 1
    done
    ! port_in_use "$port"
}

# 清理占用端口的遗留进程，返回是否已清理干净。
# 只停「命令行匹配预期」的进程（本项目后端 / 前端 dev）；占用者是别的程序时不动它，
# 由调用方报错——避免把恰好用同一端口的无关服务杀掉。
clear_port_occupant() {
    local port="$1" pattern="$2" description="$3" skip="${4:-0}"
    [ "$skip" = "1" ] && return 0
    port_in_use "$port" || return 0

    local pid targets=() cmd
    for pid in $(port_listener_pids "$port"); do
        cmd=$(process_cmdline "$pid")
        if [[ "$cmd" =~ $pattern ]]; then
            targets+=("$pid")
        else
            log_warn "端口 $port 被非本项目进程占用（PID $pid）：$cmd"
            return 1
        fi
    done
    [ "${#targets[@]}" -gt 0 ] || { log_warn "端口 $port 被占用但无法识别占用进程（缺少 ss/lsof 或权限不足）"; return 1; }

    for pid in "${targets[@]}"; do
        log_info "    清理占用端口 $port 的$description（PID $pid）"
        kill -9 "$pid" 2>/dev/null || true
    done
    if wait_port_released "$port" 15; then return 0; fi
    log_warn "端口 $port 在 15 秒内未释放"
    return 1
}

# 后台拉起命令：setsid 另起会话（可用负 pid 整组停止），进程输出落日志文件
spawn_detached() {
    local log="$1"; shift
    if command -v setsid >/dev/null 2>&1; then
        setsid "$@" >"$log" 2>&1 &
    else
        "$@" >"$log" 2>&1 &
    fi
    spawned_pid=$!
}

# 读取开发连接串：凭据只在 appsettings.Development.json 维护一处，脚本不重复维护
dev_connection_string() {
    [ -f "$settings_path" ] || abort "未找到后端开发配置：$settings_path"
    local connection
    if ! connection=$(node -e 'const fs=require("fs");const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8"));const v=j.ConnectionStrings&&j.ConnectionStrings.Default;if(!v||!String(v).trim()){process.exit(1)}process.stdout.write(String(v));' "$settings_path"); then
        abort "$settings_path 未配置 ConnectionStrings:Default"
    fi
    printf '%s' "$connection"
}

# 仅替换连接串的 Database 段，得到指定库的连接串
new_connection_string() {
    local base="$1" database="$2"
    case "$base" in
        *[Dd]atabase*=*) ;;
        *) abort '连接串缺少 Database 段，无法切换数据库' ;;
    esac
    printf '%s' "$base" | sed -E "s/([Dd]atabase[[:space:]]*=[[:space:]]*)[^;]*/\1$database/"
}

# 删除指定库（dotnet ef database drop），成功返回 0。
# --no-build：避免与"仍在运行的其它后端"抢构建输出（Bin 被锁会让构建失败）。
drop_database() {
    local connection="$1" database="$2" out
    out=$(cd "$backend_dir" && ConnectionStrings__Default="$connection" \
        dotnet ef database drop --force --no-build --project "$infra_project" --startup-project "$api_project" 2>&1)
    if [ $? -eq 0 ]; then
        log_info "    已删除数据库 $database"
        return 0
    fi
    log_warn "    数据库 $database 未删除：$(printf '%s\n' "$out" | tail -n 1)"
    return 1
}

# 清理历史残留库（含 -KeepDatabase 保留的库与上一轮删除失败的库）
clean_residual_databases() {
    [ "$skip_residual" = "1" ] && return 0
    [ -f "$history_file" ] || return 0

    local names=() remaining=() line name
    while IFS= read -r line; do
        name="${line//[[:space:]]/}"
        [ -n "$name" ] && names+=("$name")
    done < "$history_file"
    if [ "${#names[@]}" -eq 0 ]; then
        : > "$history_file"
        return 0
    fi

    write_step "清理历史残留库（${#names[@]} 个）"
    for name in "${names[@]}"; do
        drop_database "$(new_connection_string "$base_connection" "$name")" "$name" || remaining+=("$name")
    done
    if [ "${#remaining[@]}" -eq 0 ]; then
        : > "$history_file"
    else
        printf '%s\n' "${remaining[@]}" > "$history_file"
    fi
}

http_ok() {
    local url="$1"
    if command -v curl >/dev/null 2>&1; then
        curl -fsS --max-time 5 "$url" >/dev/null 2>&1
    else
        node -e 'fetch(process.argv[1]).then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))' "$url" >/dev/null 2>&1
    fi
}

# 轮询后端就绪：返回 ready / launcher-exited / timeout。
# launcher-exited 必须单独识别——端口被别的服务占用时 dotnet run 会立刻退出，
# 若只看健康检查会命中"别人的后端"（连的是别的库），用例会静默跑在错误数据集上。
wait_backend_ready() {
    local timeout="$1"
    local deadline=$((SECONDS + timeout))
    while [ "$SECONDS" -lt "$deadline" ]; do
        kill -0 "$backend_pid" 2>/dev/null || return 1
        sleep 2
        http_ok "$backend_health" && return 0
    done
    return 2
}

# 停止由 spawn_detached 拉起的进程树：setsid 另起会话后组 id == pid，整组停止
stop_process_tree() {
    local pid="$1" description="$2"
    [ -n "$pid" ] || return 0
    pkill -9 -g "$pid" >/dev/null 2>&1 || true   # 整组（npm / dotnet run 及其子进程）
    kill -9 "$pid" 2>/dev/null || true           # 组不存在时至少停直接子进程
    log_info "    已停止$description（PID $pid）"
}

stop_started_backend() {
    stop_process_tree "$backend_pid" '后端'
    # 兜底：本次残留的 App.Api（命令行匹配本仓库路径，避免误停其它进程）
    pkill -9 -f "$api_project" >/dev/null 2>&1 || true
    # 端口未释放时删库必然失败（bin 仍被占用），先等
    if wait_port_released "$backend_port" 15; then return 0; fi
    log_warn "端口 $backend_port 在 15 秒内未释放，删库可能失败"
}

stop_started_frontend() {
    stop_process_tree "$frontend_pid" '前端 dev'
    # 兜底：vite 进程（npm 只是包装，子进程可能已成孤儿）
    pkill -9 -f 'node_modules/vite/bin/vite.js' >/dev/null 2>&1 || true
}

cleanup() {
    if [ "$backend_started" = "1" ]; then
        write_step '清理现场'
        stop_started_backend
        stop_started_frontend
        if [ "$keep_database" = "1" ]; then
            log_warn "已保留数据库 $database（下次运行 e2e:run 时自动清理）"
            printf '%s\n' "$database" >> "$history_file"
        elif ! drop_database "$connection" "$database"; then
            # 兜底：删不掉就记进清单，避免无声残留（下次运行开始时清理）
            printf '%s\n' "$database" >> "$history_file"
            log_warn "本轮库 $database 已记入历史清单，下次运行 e2e:run 时自动清理"
        fi
    fi
    if [ "$exit_code" != "0" ] && [ -s "$log_dir/backend.log" ]; then
        log_info '--- backend.log 末尾 40 行 ---'
        tail -n 40 "$log_dir/backend.log"
    fi
    write_step "完成（退出码 $exit_code：0=全部通过 / 1=有用例失败 / 2=脚本错误）"
}

parse_args "$@"
check_prerequisites

database="${db_prefix}$(date +%Y%m%d%H%M%S)"
base_connection=$(dev_connection_string)
connection=$(new_connection_string "$base_connection" "$database")

trap cleanup EXIT

write_step "本轮数据库：$database"
rm -rf "$log_dir" "$output_dir"
mkdir -p "$log_dir"

# 1. 清理遗留进程：本脚本自己拉起前后端，从干净起点开始。
#    后端必须换成本轮库（遗留的开发后端连的是开发库）；前端一并重启，避免陈旧 dev server 让用例跑在旧代码上。
write_step '清理占用端口的遗留进程'
if ! clear_port_occupant "$backend_port" 'App\.Api' '后端'; then
    abort "端口 $backend_port 被非本项目进程占用，为避免误杀未做清理；请手工处理后重试。"
fi
if ! clear_port_occupant "$frontend_port" 'vite' '前端 dev' "$reuse_frontend"; then
    abort "端口 $frontend_port 被非本项目进程占用，为避免误杀未做清理；请手工处理后重试。"
fi

# 2. 清理历史残留库
clean_residual_databases

# 3. 构建后端 → 以本轮库启动后端（库不存在时自动建库 + 迁移 + 内置管理员种子）
#    -p:EnforceCodeStyleInBuild=false：Linux 下 IDE0005 等风格规则会在生成文件上报错（同 Dockerfile）；
#    先构建再 --no-build 启动，避免 dotnet run 以默认属性重新构建而失败。
write_step '构建后端'
if ! build_out=$(cd "$backend_dir" && dotnet build "$api_project/App.Api.csproj" -p:EnforceCodeStyleInBuild=false -v:m 2>&1); then
    printf '%s\n' "$build_out" | tail -n 40
    abort '后端构建失败，本轮终止。'
fi
printf '%s\n' "$build_out" > "$log_dir/backend-build.log"

write_step "启动后端并等待就绪（库 $database）"
spawn_detached "$log_dir/backend.log" env \
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="http://localhost:$backend_port" \
    dotnet run --project "$api_project" --no-build \
    -- "--ConnectionStrings:Default=$connection"
backend_pid="$spawned_pid"
backend_started=1

wait_backend_ready "$backend_timeout"
ready_code=$?
if [ "$ready_code" -eq 1 ]; then
    abort "后端进程已退出（端口 $backend_port 被占用或启动失败），本轮终止。请确认端口空闲后重试。"
fi
if [ "$ready_code" -ne 0 ]; then
    abort "后端在 $backend_timeout 秒内未就绪（$backend_health）"
fi
log_info "    后端已就绪：http://localhost:$backend_port"

# 4. 前端 dev：未运行则由脚本启动（结束时一并停止），已在运行则复用
if ! port_in_use "$frontend_port"; then
    write_step "启动前端 dev（端口 $frontend_port）"
    spawn_detached "$log_dir/frontend.log" npm run dev
    frontend_pid="$spawned_pid"
    frontend_deadline=$((SECONDS + 120))
    while [ "$SECONDS" -lt "$frontend_deadline" ] && ! port_in_use "$frontend_port"; do sleep 2; done
    port_in_use "$frontend_port" || abort "前端 dev 在 120 秒内未就绪（端口 $frontend_port）"
else
    log_info "    复用已在运行的前端 dev（端口 $frontend_port）"
fi

# 5. 运行用例：产物固定落 test-results/e2e-run，避免与手工运行互相覆盖
#    过滤（-Spec / -Grep）只改变用例范围，数据库仍为本轮独立库（见文件头说明）
filter_text=''
if [ "${#specs[@]}" -gt 0 ]; then filter_text="文件：$(printf '%s、' "${specs[@]}")"; fi
if [ -n "$grep_text" ]; then
    filter_text="${filter_text:+$filter_text；}标题：$grep_text"
fi
if [ -n "$filter_text" ]; then
    write_step "运行 Playwright 用例（过滤：$filter_text）"
else
    write_step '运行 Playwright 全量用例'
fi

playwright_args=(test "--output=$output_dir")
[ -n "$grep_text" ] && playwright_args+=(--grep "$grep_text")
[ "${#specs[@]}" -gt 0 ] && playwright_args+=("${specs[@]}")
(cd "$frontend_dir" && npx playwright "${playwright_args[@]}")
exit_code=$?

exit "$exit_code"
