# syntax=docker/dockerfile:1
#
# OneSystem 一体化镜像：前端（Vue 3）与后端（.NET 8 ASP.NET Core）打进同一镜像。
# 后端直接托管前端静态产物（wwwroot），SPA 路由由 MapFallbackToFile("index.html") 兜底，
# 前端以同源 /api 请求后端（.env.production 的 VITE_API_BASE_URL=/api），无需 Nginx。
#
# 构建（仓库根目录执行）：
#   docker build -t onsystem-app .
#
# 运行（PostgreSQL 须先可达；容器启动时自动执行 EF 迁移与种子数据）：
#   docker run -d --name onsystem-app -p 5080:5080 \
#     -e ASPNETCORE_ENVIRONMENT=Production \
#     -e ConnectionStrings__Default="Host=<db-host>;Port=5432;Database=app;Username=<user>;Password=<pwd>" \
#     -e JWT__SECRET="<jwt-secret>" \
#     onsystem-app
#   # ASPNETCORE_ENVIRONMENT 运行时指定（不指定时框架默认为 Production）；
#   # Development 可开 Swagger（/swagger），不推荐用于共享环境

ARG NODE_VERSION=20
ARG DOTNET_VERSION=8.0

# ---------- 阶段 1：前端构建 ----------
FROM node:${NODE_VERSION}-alpine AS frontend-build
WORKDIR /build
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# ---------- 阶段 2：后端发布（并入前端静态产物） ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS backend-build
WORKDIR /src
COPY backend/ ./
# 构建期关闭代码风格强制（Linux 下 IDE0005 等风格规则在生成文件上报错；本地开发仍由 IDE 与 CI 约束）
RUN dotnet publish src/App.Api/App.Api.csproj -c Release -o /src/publish /p:EnforceCodeStyleInBuild=false
# 前端构建产物作为静态站点放入发布输出，由 UseStaticFiles 托管
COPY --from=frontend-build /build/dist ./publish/wwwroot

# ---------- 阶段 3：运行时 ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=backend-build /src/publish ./
# 环境由运行时 -e ASPNETCORE_ENVIRONMENT 指定（缺省即 Production；Development 开 Swagger）
# 与 dev 端口保持一致，容器内监听所有网卡
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "App.Api.dll"]
