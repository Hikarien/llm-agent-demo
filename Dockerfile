# =============================================================================
# hyit-kg-agent — .NET 10 WebAPI (multi-stage build)
# Stage 1: SDK build & publish → Stage 2: ASP.NET runtime
# =============================================================================

# ---- Stage 1: 构建 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先复制项目文件，利用 Docker layer cache 加速后续构建
COPY *.csproj .
RUN dotnet restore

# 复制全部源码并发布
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Stage 2: 运行时 ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 创建非 root 用户（安全最佳实践）
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

# ASP.NET 默认监听 8080（.NET 8+ 镜像已预设 ASPNETCORE_URLS=http://+:8080）
EXPOSE 8080

# 启动应用
ENTRYPOINT ["dotnet", "llm-agent-demo.dll"]
