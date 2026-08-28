using ModelFailoverGateway.Services;

// 智能自适应 ContentRoot 路径（方案A：便携独立模式）：基于 exe 真实绝对物理目录，彻底避免因启动快捷方式/脚本工作目录漂移而导致数据分散
var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
var currentDir = Directory.GetCurrentDirectory().TrimEnd('\\', '/');
string contentRoot = baseDir;

// 1. 若是从源码输出目录 (bin/Debug 或 bin/Release) 启动，自动向上回溯至源码 backend 目录
var devBackendDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
if (File.Exists(Path.Combine(devBackendDir, "ModelFailoverGateway.csproj")) || 
    (Directory.Exists(Path.Combine(devBackendDir, "data")) && File.Exists(Path.Combine(devBackendDir, "appsettings.json"))))
{
    contentRoot = devBackendDir;
}
// 2. 若是从项目根目录启动（例如包含 backend 子目录）
else if (Directory.Exists(Path.Combine(currentDir, "backend")) && File.Exists(Path.Combine(currentDir, "backend", "appsettings.json")))
{
    contentRoot = Path.Combine(currentDir, "backend");
}
// 3. 若当前工作目录就是有效根目录（包含 wwwroot 与 appsettings.json）
else if (File.Exists(Path.Combine(currentDir, "appsettings.json")) && Directory.Exists(Path.Combine(currentDir, "wwwroot")))
{
    contentRoot = currentDir;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

// 1. 注册核心服务与控制器
builder.Services.AddControllers();

// 2. 配置无超时限制的代理 HttpClient，支持长时间 SSE 打字机流式交互
builder.Services.AddHttpClient("ModelProxyClient", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});

builder.Services.AddHttpClient("ModelTestClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 3. 注册单例服务与系统日志拦截器
var sysLogService = new SystemLogService();
builder.Services.AddSingleton<ISystemLogService>(sysLogService);
builder.Logging.AddProvider(new SystemLogProvider(sysLogService));

builder.Services.AddSingleton<IConfigInjectionService, ConfigInjectionService>();
builder.Services.AddSingleton<IChannelService, ChannelService>();
builder.Services.AddSingleton<ITokenStatsService, TokenStatsService>();
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<TrayIconManager>();
builder.Services.AddSingleton<IAlertService, AlertService>();
builder.Services.AddSingleton<IProxyEngine, ProxyEngine>();

// 4. 允许跨域（方便开发调试与各种客户端调用）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 获取当前配置的端口（默认 5000）
var configService = app.Services.GetRequiredService<IConfigInjectionService>();
var port = configService.GetConfiguredPort();

// 启动 Windows 系统托盘驻留图标与原生桌面萌宠
var trayManager = app.Services.GetRequiredService<TrayIconManager>();
var alertService = app.Services.GetRequiredService<IAlertService>();
trayManager.Start(port, alertService);

app.UseCors("AllowAll");

// 5. 静态文件与 SPA 默认页托管（用于承载 Vue 3 管理界面）
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers.Pragma = "no-cache";
            ctx.Context.Response.Headers.Expires = "0";
        }
    }
});

app.UseRouting();

app.MapControllers();

// 6. 核心透明代理路由：捕获所有 /v1/*、/{group}/v1/* 以及非管理 API 的模型请求
app.Map("/{**catchAll}", async (HttpContext context, IProxyEngine proxyEngine) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // NOTE: 如果是管理后台 API 请求，未匹配到 Controller 则返回 404
    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    // NOTE: 判断是否属于模型代理请求路径（/v1/* 或 /{group}/v1/*）
    var isModelProxyPath = path.StartsWith("/v1", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("/v1/", StringComparison.OrdinalIgnoreCase);

    // 前端 SPA 页面路由回退（浏览器访问根路径或刷新前端子页面时返回 index.html）
    if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
        !isModelProxyPath &&
        !Path.HasExtension(path))
    {
        var indexPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
            await context.Response.SendFileAsync(indexPath);
            return;
        }
    }

    // 转发模型请求并执行分组过滤、模型映射与故障转移逻辑
    await proxyEngine.ForwardRequestAsync(context);
});

// 注册应用优雅停机事件，确保关机或退出时内存数据立即同步落盘
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var tokenStatsService = app.Services.GetRequiredService<ITokenStatsService>();
var logService = app.Services.GetRequiredService<ILogService>();

lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        tokenStatsService.Flush();
        logService.Flush();
    }
    catch { }
});

// 监听动态配置的端口
app.Urls.Clear();
app.Urls.Add($"http://127.0.0.1:{port}");
app.Run();
