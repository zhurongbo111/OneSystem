# RUNBOOK.md — 快速试跑（Docker Compose）

> 目的：**不改代码、不装 SDK**，用 Docker 一条命令把系统在本机跑起来（镜像已在本地时约十秒起；首次需拉取镜像，视网速几分钟）。
> 本文件只讲"跑起来"，不讲开发流程；要改代码 / 跑单测与 e2e，见 `.codebuddy/CONTEXT.md` §4 与 `AGENTS.md`。
> 系统构成：PostgreSQL + 后端（.NET 8）+ 前端（Vue 3，构建产物由后端同端口托管），一键启动清单见仓库根 `docker-compose.yml`。

## 1. 前置条件

| 项 | 要求 |
|---|---|
| Docker | 已安装 Docker Desktop / Docker Engine，且**正在运行**（含 `docker compose`，v2） |
| 网络 | 首次需拉取 `postgres:16` 与应用镜像 `390992476/onesystem:latest`（公开仓库，无需登录） |
| 端口 | 宿主 **5080** 空闲（被占时见 §5） |
| 无需 | .NET SDK、Node.js、本机 PostgreSQL 都不需要 |

自检：

```bash
docker --version && docker compose version    # 两条都能打印版本即可
```

## 2. 启动

在**仓库根目录**执行：

```bash
docker compose up -d
docker compose logs -f app        # 跟启动日志；看到「App.Api 启动完成」即就绪（Ctrl+C 退出查看，不影响容器）
```

首次启动会自动完成：等数据库就绪（`pg_isready` 健康检查）→ 执行数据库迁移与建表 → 写入初始数据（内置管理员、内置角色、预置会计科目 / 期间 / 科目映射 / 现金账户）。

## 3. 验证与访问

```bash
curl http://localhost:5080/health
# 期望：{"code":0,"message":"success","data":"healthy"}
```

浏览器打开 <http://localhost:5080>，用内置管理员登录：

| 账号 | 密码 |
|---|---|
| `admin` | `admin123` |

能看到首页与侧边菜单即成功。

> Windows PowerShell 里 `curl` 是 `Invoke-WebRequest` 的别名，请用 `curl.exe http://localhost:5080/health`，或直接看浏览器。

## 4. 常用开关

| 想做的事 | 做法 |
|---|---|
| 换宿主端口（5080 被占） | 改 `docker-compose.yml` 里 app 的 `ports`（如 `"8080:80"`），或加端口映射；访问地址随之改变 |
| 固定到某次构建的镜像版本 | 在仓库根 `.env` 写 `ONESYSTEM_IMAGE=390992476/onesystem:sha-1a2b3c4`；或单次传入（macOS / Linux：`ONESYSTEM_IMAGE=... docker compose up -d`；PowerShell：先 `$env:ONESYSTEM_IMAGE='...'` 再 `docker compose up -d`） |
| 覆盖敏感默认值（JWT 密钥 / 数据库口令） | 在仓库根建 `.env`（已 gitignore）写 `JWT__SECRET=` / `POSTGRES_PASSWORD=`；或用环境变量传入。默认值**只面向本地试用**，共享 / 生产环境必须覆盖 |
| 用图形工具直连数据库 | 在 `docker-compose.yml` 的 `postgres` 服务打开 `ports: ["5433:5432"]`（宿主 5433，避免与本机 5432 冲突），用户 / 口令 / 库见该文件 `POSTGRES_*` |

## 5. 排错

| 现象 | 原因 | 处置 |
|---|---|---|
| `docker compose up` 报端口被占 / 应用起不来 | 宿主 5080 已被别的程序占用 | 按 §4 换端口，或先停掉占用者 |
| 拉镜像失败 / 卡在 `Pulling` | 网络到 Docker Hub 不通 | 配置镜像加速或代理后重试 |
| 容器反复重启 | 数据库未就绪或凭据不匹配 | `docker compose logs postgres app` 看原因；确认 `.env` 里 `POSTGRES_*` 与 `JWT__SECRET` 没写成非法值 |
| 页面能打开但接口报错 / 空白 | 迁移或种子失败 | `docker compose logs -f app` 查异常栈；必要时 `docker compose down -v` 重置数据后重来 |
| 想回到全新状态 | 数据存在 Docker 卷里 | `docker compose down -v` 再 `docker compose up -d`（**会删除全部数据**） |
| 端口正常但浏览器打不开 | 访问了 5173 之类开发端口 | 本形态只有 **5080** 一个端口（前端由后端托管） |

## 6. 停止与说明

```bash
docker compose down        # 停止并删除容器（数据卷保留，下次启动数据还在）
docker compose down -v     # 连数据卷一起删除（重置为全新状态）
```

- 这是**生产形态**：容器内单端口 80 映射到宿主 5080，前端静态产物由后端一起托管，**没有 5173，也不开 Swagger**。
- 拉取的是 Docker Hub 上**已发布**的镜像，**不含仓库里未提交 / 未发布的改动**；想跑本地改动请走开发流程（见 `AGENTS.md`、`.codebuddy/CONTEXT.md` §4）。
- 数据持久化在 Docker 卷 `onsystem-pgdata`，容器删了数据仍在。