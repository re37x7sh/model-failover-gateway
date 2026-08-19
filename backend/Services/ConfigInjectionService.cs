using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ModelFailoverGateway.Services;

public class ClientStatusItem
{
    public bool IsInjected { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool HasBackup { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class SystemStatusDto
{
    public int Port { get; set; } = 5000;
    public ClientStatusItem ClaudeCode { get; set; } = new();
    public ClientStatusItem Codex { get; set; } = new();
    public ClientStatusItem VSCode { get; set; } = new();
    public ClientStatusItem Continue { get; set; } = new();
}

public interface IConfigInjectionService
{
    Task<SystemStatusDto> GetStatusAsync(int currentPort);
    Task<bool> InjectAsync(string target, int port, string group = "claude", string providerName = "gateway");
    Task<bool> RestoreAsync(string target);
    Task<bool> SavePortConfigAsync(int newPort);
    int GetConfiguredPort();
}

public class ConfigInjectionService : IConfigInjectionService
{
    private readonly ILogger<ConfigInjectionService> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly string _userProfile;
    private readonly string _appData;

    // 支持 VSCode 特有的 JSONC 格式（带注释、允许尾随逗号）
    private static readonly JsonDocumentOptions RelaxedJsonDocOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions RelaxedJsonWriteOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public ConfigInjectionService(ILogger<ConfigInjectionService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _env = env;
        _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    // Claude Code 配置文件路径（settings.json 优先级最高）
    private string ClaudeSettingsJsonPath => Path.Combine(_userProfile, ".claude", "settings.json");
    private string ClaudeSettingsJsonBakPath => Path.Combine(_userProfile, ".claude", "settings.json.bak");
    private string ClaudeJsonPath => Path.Combine(_userProfile, ".claude.json");
    private string ClaudeJsonBakPath => Path.Combine(_userProfile, ".claude.json.bak");

    // CodeX / Codex CLI 配置文件路径 (TOML)
    private string CodexConfigTomlPath => Path.Combine(_userProfile, ".codex", "config.toml");
    private string CodexConfigTomlBakPath => Path.Combine(_userProfile, ".codex", "config.toml.bak");

    // VSCode 设置路径（自动适配 Windows / macOS / Linux）
    private string VSCodeSettingsPath
    {
        get
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return Path.Combine(_userProfile, "Library", "Application Support", "Code", "User", "settings.json");
            }
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                return Path.Combine(_userProfile, ".config", "Code", "User", "settings.json");
            }
            return Path.Combine(_appData, "Code", "User", "settings.json");
        }
    }
    private string VSCodeSettingsBakPath => VSCodeSettingsPath + ".bak";

    // Continue 插件路径
    private string ContinueConfigPath => Path.Combine(_userProfile, ".continue", "config.json");
    private string ContinueConfigBakPath => Path.Combine(_userProfile, ".continue", "config.json.bak");

    private string AppSettingsPath => Path.Combine(_env.ContentRootPath, "appsettings.json");

    public int GetConfiguredPort()
    {
        try
        {
            if (File.Exists(AppSettingsPath))
            {
                var json = File.ReadAllText(AppSettingsPath);
                using var doc = JsonDocument.Parse(json, RelaxedJsonDocOptions);
                if (doc.RootElement.TryGetProperty("Gateway", out var gw) &&
                    gw.TryGetProperty("Port", out var portProp))
                {
                    return portProp.GetInt32();
                }
            }
        }
        catch { }
        return 5000;
    }

    public async Task<bool> SavePortConfigAsync(int newPort)
    {
        try
        {
            JsonObject root;
            if (File.Exists(AppSettingsPath))
            {
                var json = await File.ReadAllTextAsync(AppSettingsPath);
                root = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject() ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var gwNode = root["Gateway"]?.AsObject() ?? new JsonObject();
            gwNode["Port"] = newPort;
            root["Gateway"] = gwNode;

            await File.WriteAllTextAsync(AppSettingsPath, root.ToJsonString(RelaxedJsonWriteOptions));
            _logger.LogInformation("已成功将代理端口配置更新为 {Port}", newPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新 appsettings.json 端口配置失败");
            return false;
        }
    }

    public Task<SystemStatusDto> GetStatusAsync(int currentPort)
    {
        var status = new SystemStatusDto
        {
            Port = currentPort
        };

        // 1. 检测 Claude Code
        var claudeInjected = false;
        var claudeDetails = "未接管";
        var hasClaudeBackup = File.Exists(ClaudeSettingsJsonBakPath) || File.Exists(ClaudeJsonBakPath);

        if (File.Exists(ClaudeSettingsJsonPath))
        {
            try
            {
                var content = File.ReadAllText(ClaudeSettingsJsonPath);
                if (content.Contains("127.0.0.1"))
                {
                    claudeInjected = true;
                    claudeDetails = "已接管 ~/.claude/settings.json";
                }
            }
            catch { }
        }

        if (!claudeInjected)
        {
            var envVal = GetUserEnvVar("ANTHROPIC_BASE_URL");
            if (!string.IsNullOrEmpty(envVal) && envVal.Contains("127.0.0.1"))
            {
                claudeInjected = true;
                claudeDetails = $"已注入环境变量: {envVal}";
            }
        }

        status.ClaudeCode = new ClientStatusItem
        {
            IsInjected = claudeInjected,
            Path = File.Exists(ClaudeSettingsJsonPath) ? ClaudeSettingsJsonPath : ClaudeJsonPath,
            HasBackup = hasClaudeBackup,
            Details = claudeDetails
        };

        // 2. 检测 CodeX (TOML 配置 + VSCode 插件 + 环境变量)
        var codexInjected = false;
        var codexDetails = "未接管";
        var hasCodexBackup = File.Exists(CodexConfigTomlBakPath) || File.Exists(VSCodeSettingsBakPath);

        if (File.Exists(CodexConfigTomlPath))
        {
            try
            {
                var toml = File.ReadAllText(CodexConfigTomlPath);
                if (toml.Contains("127.0.0.1") && (toml.Contains("model_provider = \"gateway\"") || toml.Contains("[model_providers.gateway]")))
                {
                    codexInjected = true;
                    codexDetails = "已接管 ~/.codex/config.toml & 环境变量";
                }
            }
            catch { }
        }

        if (!codexInjected)
        {
            var openaiEnv = GetUserEnvVar("OPENAI_BASE_URL");
            var codexEnv = GetUserEnvVar("CODEX_BASE_URL");
            if (!string.IsNullOrEmpty(openaiEnv) && openaiEnv.Contains("127.0.0.1"))
            {
                codexInjected = true;
                codexDetails = $"已注入 OPENAI_BASE_URL: {openaiEnv}";
            }
            else if (!string.IsNullOrEmpty(codexEnv) && codexEnv.Contains("127.0.0.1"))
            {
                codexInjected = true;
                codexDetails = $"已注入 CODEX_BASE_URL: {codexEnv}";
            }
            else if (File.Exists(VSCodeSettingsPath))
            {
                try
                {
                    var content = File.ReadAllText(VSCodeSettingsPath);
                    if (content.Contains("codex.baseUrl") && content.Contains("127.0.0.1"))
                    {
                        codexInjected = true;
                        codexDetails = "VSCode CodeX 设置已注入端点";
                    }
                }
                catch { }
            }
        }

        status.Codex = new ClientStatusItem
        {
            IsInjected = codexInjected,
            Path = File.Exists(CodexConfigTomlPath) ? CodexConfigTomlPath : "OPENAI_BASE_URL & CodeX 设置",
            HasBackup = hasCodexBackup,
            Details = codexDetails
        };

        // 3. 检测 VSCode 全局设置
        var vscodeInjected = false;
        var vscodeDetails = "未接管";
        if (File.Exists(VSCodeSettingsPath))
        {
            try
            {
                var content = File.ReadAllText(VSCodeSettingsPath);
                if (content.Contains("127.0.0.1"))
                {
                    vscodeInjected = true;
                    vscodeDetails = "全局 settings.json 已注入端点";
                }
            }
            catch { }
        }

        status.VSCode = new ClientStatusItem
        {
            IsInjected = vscodeInjected,
            Path = VSCodeSettingsPath,
            HasBackup = File.Exists(VSCodeSettingsBakPath),
            Details = vscodeDetails
        };

        // 4. 检测 Continue
        var continueInjected = false;
        var continueDetails = "未接管";
        if (File.Exists(ContinueConfigPath))
        {
            try
            {
                var content = File.ReadAllText(ContinueConfigPath);
                if (content.Contains("127.0.0.1"))
                {
                    continueInjected = true;
                    continueDetails = "continue/config.json 已注入端点";
                }
            }
            catch { }
        }

        status.Continue = new ClientStatusItem
        {
            IsInjected = continueInjected,
            Path = ContinueConfigPath,
            HasBackup = File.Exists(ContinueConfigBakPath),
            Details = continueDetails
        };

        return Task.FromResult(status);
    }

    public async Task<bool> InjectAsync(string target, int port, string group = "claude", string providerName = "gateway")
    {
        var targetUrl = $"http://127.0.0.1:{port}/{group}";
        var t = target.ToLowerInvariant();
        var pName = string.IsNullOrWhiteSpace(providerName) ? "gateway" : providerName.Trim();

        var success = true;
        if (t is "all" or "claude")
        {
            success &= await InjectClaudeCodeInternalAsync(targetUrl);
        }
        if (t is "all" or "codex")
        {
            success &= await InjectCodexInternalAsync(port, pName);
        }
        if (t is "all" or "vscode")
        {
            success &= await InjectVSCodeInternalAsync(targetUrl, port);
        }
        if (t is "all" or "continue")
        {
            success &= await InjectContinueInternalAsync(targetUrl);
        }
        return success;
    }

    public async Task<bool> RestoreAsync(string target)
    {
        var t = target.ToLowerInvariant();
        var success = true;
        if (t is "all" or "claude")
        {
            success &= await RestoreClaudeCodeInternalAsync();
        }
        if (t is "all" or "codex")
        {
            success &= await RestoreCodexInternalAsync();
        }
        if (t is "all" or "vscode")
        {
            success &= await RestoreVSCodeInternalAsync();
        }
        if (t is "all" or "continue")
        {
            success &= await RestoreContinueInternalAsync();
        }
        return success;
    }

    private async Task<bool> InjectClaudeCodeInternalAsync(string targetUrl)
    {
        try
        {
            // 1. 核心接管：~/.claude/settings.json（Claude Code 官方最高优先级配置）
            var claudeDir = Path.GetDirectoryName(ClaudeSettingsJsonPath);
            if (claudeDir != null && !Directory.Exists(claudeDir))
            {
                Directory.CreateDirectory(claudeDir);
            }

            if (File.Exists(ClaudeSettingsJsonPath))
            {
                if (!File.Exists(ClaudeSettingsJsonBakPath))
                {
                    File.Copy(ClaudeSettingsJsonPath, ClaudeSettingsJsonBakPath, true);
                    _logger.LogInformation("已创建 ~/.claude/settings.json 备份文件");
                }

                var json = await File.ReadAllTextAsync(ClaudeSettingsJsonPath);
                var root = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject() ?? new JsonObject();
                var envNode = root["env"]?.AsObject() ?? new JsonObject();

                envNode["ANTHROPIC_BASE_URL"] = targetUrl;
                envNode["ANTHROPIC_AUTH_TOKEN"] = "sk-local-proxy";
                envNode["ANTHROPIC_API_KEY"] = "sk-local-proxy";

                root["env"] = envNode;
                await File.WriteAllTextAsync(ClaudeSettingsJsonPath, root.ToJsonString(RelaxedJsonWriteOptions));
                _logger.LogInformation("成功注入 ~/.claude/settings.json -> {Url}", targetUrl);
            }
            else
            {
                var root = new JsonObject
                {
                    ["env"] = new JsonObject
                    {
                        ["ANTHROPIC_BASE_URL"] = targetUrl,
                        ["ANTHROPIC_AUTH_TOKEN"] = "sk-local-proxy",
                        ["ANTHROPIC_API_KEY"] = "sk-local-proxy"
                    }
                };
                await File.WriteAllTextAsync(ClaudeSettingsJsonPath, root.ToJsonString(RelaxedJsonWriteOptions));
                _logger.LogInformation("成功创建并注入 ~/.claude/settings.json -> {Url}", targetUrl);
            }

            // 2. 辅助接管：~/.claude.json
            if (File.Exists(ClaudeJsonPath))
            {
                if (!File.Exists(ClaudeJsonBakPath))
                {
                    File.Copy(ClaudeJsonPath, ClaudeJsonBakPath, true);
                }
                var json = await File.ReadAllTextAsync(ClaudeJsonPath);
                var node = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject() ?? new JsonObject();
                node["baseUrl"] = targetUrl;
                node["apiKey"] = "sk-local-proxy";
                await File.WriteAllTextAsync(ClaudeJsonPath, node.ToJsonString(RelaxedJsonWriteOptions));
            }

            // 3. 注入 Windows 用户级环境变量
            SetUserEnvVar("ANTHROPIC_BASE_URL", targetUrl);
            SetUserEnvVar("ANTHROPIC_API_KEY", "sk-local-proxy");
            SetUserEnvVar("ANTHROPIC_AUTH_TOKEN", "sk-local-proxy");

            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", targetUrl, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-local-proxy", EnvironmentVariableTarget.Process);

            _logger.LogInformation("已全方位完成 Claude Code 配置接管！");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接管 Claude Code 配置失败");
            return false;
        }
    }

    private Task<bool> RestoreClaudeCodeInternalAsync()
    {
        try
        {
            // 1. 还原 ~/.claude/settings.json
            if (File.Exists(ClaudeSettingsJsonBakPath))
            {
                File.Copy(ClaudeSettingsJsonBakPath, ClaudeSettingsJsonPath, true);
                File.Delete(ClaudeSettingsJsonBakPath);
                _logger.LogInformation("已成功从备份还原 ~/.claude/settings.json");
            }

            // 2. 还原 ~/.claude.json
            if (File.Exists(ClaudeJsonBakPath))
            {
                File.Copy(ClaudeJsonBakPath, ClaudeJsonPath, true);
                File.Delete(ClaudeJsonBakPath);
                _logger.LogInformation("已成功从备份还原 ~/.claude.json");
            }

            // 3. 清理注册表环境变量
            DeleteUserEnvVar("ANTHROPIC_BASE_URL");
            DeleteUserEnvVar("ANTHROPIC_API_KEY");
            DeleteUserEnvVar("ANTHROPIC_AUTH_TOKEN");

            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null, EnvironmentVariableTarget.Process);

            _logger.LogInformation("已成功全面还原 Claude Code 原始配置与环境变量");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "还原 Claude Code 配置失败");
            return Task.FromResult(false);
        }
    }

    private async Task<bool> InjectCodexInternalAsync(int port, string providerName = "gateway")
    {
        try
        {
            var codexEndpoint = $"http://127.0.0.1:{port}/codex/v1";

            // 1. 注入 ~/.codex/config.toml (OpenAI Codex CLI / ccswitch 生效的核心配置文件)
            await InjectCodexTomlAsync(codexEndpoint, providerName);

            // 2. 注入 OpenAI / CodeX 标准环境变量
            SetUserEnvVar("OPENAI_BASE_URL", codexEndpoint);
            SetUserEnvVar("OPENAI_API_BASE", codexEndpoint);
            SetUserEnvVar("OPENAI_API_KEY", "sk-local-proxy");
            SetUserEnvVar("CODEX_BASE_URL", codexEndpoint);
            SetUserEnvVar("CODEX_API_KEY", "sk-local-proxy");

            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", codexEndpoint, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "sk-local-proxy", EnvironmentVariableTarget.Process);

            // 3. 注入 VSCode 中的 CodeX / ChatGPT 插件配置
            if (File.Exists(VSCodeSettingsPath))
            {
                if (!File.Exists(VSCodeSettingsBakPath))
                {
                    File.Copy(VSCodeSettingsPath, VSCodeSettingsBakPath, true);
                }

                var json = await File.ReadAllTextAsync(VSCodeSettingsPath);
                var settings = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject() ?? new JsonObject();
                settings["chatgpt.apiUrl"] = codexEndpoint;
                settings["chatgpt.apiKey"] = "sk-local-proxy";
                settings["codex.baseUrl"] = codexEndpoint;
                settings["codex.apiUrl"] = codexEndpoint;
                settings["openai.apiBase"] = codexEndpoint;

                await File.WriteAllTextAsync(VSCodeSettingsPath, settings.ToJsonString(RelaxedJsonWriteOptions));
            }

            _logger.LogInformation("成功为 CodeX (TOML [{Provider}] / 插件 / 环境变量) 全方位接管端点 -> {Url}", providerName, codexEndpoint);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "接管 CodeX 配置失败");
            return false;
        }
    }

    private async Task InjectCodexTomlAsync(string codexEndpoint, string providerName = "gateway")
    {
        if (!File.Exists(CodexConfigTomlPath))
        {
            var dir = Path.GetDirectoryName(CodexConfigTomlPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else if (!File.Exists(CodexConfigTomlBakPath))
        {
            File.Copy(CodexConfigTomlPath, CodexConfigTomlBakPath, true);
            _logger.LogInformation("已创建 ~/.codex/config.toml 备份文件");
        }

        var content = File.Exists(CodexConfigTomlPath)
            ? await File.ReadAllTextAsync(CodexConfigTomlPath)
            : "";

        var pName = string.IsNullOrWhiteSpace(providerName) ? "gateway" : providerName.Trim();
        var gatewaySection = $@"
[model_providers.{pName}]
name = ""{pName}""
base_url = ""{codexEndpoint}""
wire_api = ""responses""
requires_openai_auth = true
";

        var lines = content.Split('\n').ToList();
        var hasModelProvider = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("model_provider", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"model_provider = \"{pName}\"";
                hasModelProvider = true;
                break;
            }
        }

        if (!hasModelProvider)
        {
            lines.Insert(0, $"model_provider = \"{pName}\"");
        }

        var newContent = string.Join("\n", lines);
        var targetHeader = $"[model_providers.{pName}]";
        if (!newContent.Contains(targetHeader))
        {
            newContent += "\n" + gatewaySection;
        }
        else
        {
            var pattern = $@"\[model_providers\.{Regex.Escape(pName)}\][\s\S]*?(?=\n\[|\z)";
            newContent = Regex.Replace(newContent, pattern, gatewaySection.Trim());
        }

        await File.WriteAllTextAsync(CodexConfigTomlPath, newContent);
        _logger.LogInformation("成功为 ~/.codex/config.toml 注入 [{ProviderName}] 模型提供商并指向本地网关", pName);
    }

    private Task<bool> RestoreCodexInternalAsync()
    {
        try
        {
            // 1. 还原 ~/.codex/config.toml
            if (File.Exists(CodexConfigTomlBakPath))
            {
                File.Copy(CodexConfigTomlBakPath, CodexConfigTomlPath, true);
                File.Delete(CodexConfigTomlBakPath);
                _logger.LogInformation("已从备份还原 ~/.codex/config.toml");
            }

            // 2. 清理环境变量
            DeleteUserEnvVar("OPENAI_BASE_URL");
            DeleteUserEnvVar("OPENAI_API_BASE");
            DeleteUserEnvVar("OPENAI_API_KEY");
            DeleteUserEnvVar("CODEX_BASE_URL");
            DeleteUserEnvVar("CODEX_API_KEY");

            Environment.SetEnvironmentVariable("OPENAI_BASE_URL", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null, EnvironmentVariableTarget.Process);

            // 3. 还原 VSCode settings
            if (File.Exists(VSCodeSettingsBakPath))
            {
                File.Copy(VSCodeSettingsBakPath, VSCodeSettingsPath, true);
                File.Delete(VSCodeSettingsBakPath);
            }

            _logger.LogInformation("已成功全面还原 CodeX / ChatGPT 配置与环境变量");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "还原 CodeX 配置失败");
            return Task.FromResult(false);
        }
    }

    private async Task<bool> InjectVSCodeInternalAsync(string claudeUrl, int port)
    {
        try
        {
            if (!File.Exists(VSCodeSettingsPath))
            {
                var dir = Path.GetDirectoryName(VSCodeSettingsPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            else if (!File.Exists(VSCodeSettingsBakPath))
            {
                File.Copy(VSCodeSettingsPath, VSCodeSettingsBakPath, true);
            }

            JsonObject settings;
            if (File.Exists(VSCodeSettingsPath))
            {
                var json = await File.ReadAllTextAsync(VSCodeSettingsPath);
                settings = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject() ?? new JsonObject();
            }
            else
            {
                settings = new JsonObject();
            }

            settings["claudeCode.baseUrl"] = claudeUrl;
            settings["claude.apiUrl"] = claudeUrl;
            settings["codex.baseUrl"] = $"http://127.0.0.1:{port}/codex/v1";
            settings["chatgpt.apiUrl"] = $"http://127.0.0.1:{port}/codex/v1";

            await File.WriteAllTextAsync(VSCodeSettingsPath, settings.ToJsonString(RelaxedJsonWriteOptions));

            _logger.LogInformation("已成功为 VSCode settings.json 注入本地网关端点");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注入 VSCode 设置失败");
            return false;
        }
    }

    private Task<bool> RestoreVSCodeInternalAsync()
    {
        try
        {
            if (File.Exists(VSCodeSettingsBakPath))
            {
                File.Copy(VSCodeSettingsBakPath, VSCodeSettingsPath, true);
                File.Delete(VSCodeSettingsBakPath);
                _logger.LogInformation("已从备份成功还原 VSCode settings.json");
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "还原 VSCode 设置失败");
            return Task.FromResult(false);
        }
    }

    private async Task<bool> InjectContinueInternalAsync(string targetUrl)
    {
        try
        {
            if (!File.Exists(ContinueConfigPath))
            {
                return false;
            }

            if (!File.Exists(ContinueConfigBakPath))
            {
                File.Copy(ContinueConfigPath, ContinueConfigBakPath, true);
            }

            var json = await File.ReadAllTextAsync(ContinueConfigPath);
            var root = JsonNode.Parse(json, documentOptions: RelaxedJsonDocOptions)?.AsObject();
            if (root != null && root["models"] is JsonArray modelsArray)
            {
                foreach (var m in modelsArray)
                {
                    if (m is JsonObject modelObj)
                    {
                        modelObj["apiBase"] = targetUrl;
                        modelObj["apiKey"] = "sk-local-proxy";
                    }
                }
                await File.WriteAllTextAsync(ContinueConfigPath, root.ToJsonString(RelaxedJsonWriteOptions));
                _logger.LogInformation("已成功为 Continue 插件 config.json 注入本地端点");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "注入 Continue 设置失败");
            return false;
        }
    }

    private Task<bool> RestoreContinueInternalAsync()
    {
        try
        {
            if (File.Exists(ContinueConfigBakPath))
            {
                File.Copy(ContinueConfigBakPath, ContinueConfigPath, true);
                File.Delete(ContinueConfigBakPath);
                _logger.LogInformation("已从备份成功还原 Continue config.json");
            }
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "还原 Continue 设置失败");
            return Task.FromResult(false);
        }
    }

    private static string? GetUserEnvVar(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var envKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", false);
                return envKey?.GetValue(name)?.ToString();
            }
            catch { }
        }
        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
    }

    private static void SetUserEnvVar(string name, string value)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var envKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", true);
                envKey?.SetValue(name, value);
            }
            catch { }
        }
    }

    private static void DeleteUserEnvVar(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var envKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", true);
                envKey?.DeleteValue(name, false);
            }
            catch { }
        }
    }
}
