using ModelFailoverGateway.Services;

var builder = WebApplication.CreateBuilder(args);

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

// 启动 Windows 系统托盘驻留图标
var trayManager = app.Services.GetRequiredService<TrayIconManager>();
trayManager.Start(port);

app.UseCors("AllowAll");

// 5. 静态文件与 SPA 默认页托管（用于承载 Vue 3 管理界面）
app.UseDefaultFiles();
app.UseStaticFiles();

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
            await context.Response.SendFileAsync(indexPath);
            return;
        }
    }

    // 转发模型请求并执行分组过滤、模型映射与故障转移逻辑
    await proxyEngine.ForwardRequestAsync(context);
});

// 监听动态配置的端口
app.Run($"http://127.0.0.1:{port}");
