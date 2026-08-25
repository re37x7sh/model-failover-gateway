using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// 请求头动态模板解析器
/// 支持从客户端原始请求中动态提取指定请求头、插值当前 API Key、模型名、客户端 IP、时间戳与唯一 UUID
/// 语法：
/// - {header:HeaderName}              : 提取客户端指定的 Header 值
/// - {header:HeaderName:-defaultValue}: 提取客户端指定的 Header 值，若不存在则回退至默认值
/// - {apiKey}                         : 当前正在尝试的渠道 API Key
/// - {model}                          : 当前调用的模型名称
/// - {group}                          : 当前请求的分组名称
/// - {client_ip}                      : 客户端真实 IP 地址
/// - {timestamp}                      : 当前 Unix 毫秒时间戳
/// - {uuid}                           : 自动生成不带中划线的 32 位 GUID
/// </summary>
public static class HeaderTemplateResolver
{
    private static readonly Regex VarRegex = new(
        @"\{(?<type>header|apiKey|model|group|client_ip|timestamp|uuid)(:(?<param>[^:\-]+))?(-(?<default>[^}]+))?\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 解析单个模板字符串并替换占位符
    /// </summary>
    public static string Resolve(
        string template,
        HttpContext? context,
        string currentApiKey,
        string targetModel,
        string requestedGroup)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return VarRegex.Replace(template, match =>
        {
            var type = match.Groups["type"].Value.ToLowerInvariant();
            var param = match.Groups["param"].Value;
            var defVal = match.Groups["default"].Value;

            switch (type)
            {
                case "header":
                    if (context != null && !string.IsNullOrWhiteSpace(param))
                    {
                        if (context.Request.Headers.TryGetValue(param, out var vals) && vals.Count > 0)
                        {
                            var headerStr = vals.ToString();
                            if (!string.IsNullOrWhiteSpace(headerStr))
                            {
                                return headerStr;
                            }
                        }
                    }
                    // 若从客户端未提取到，且设置了嵌套变量（例如 -{apiKey}），递归解析默认值
                    if (!string.IsNullOrEmpty(defVal))
                    {
                        return Resolve(defVal, context, currentApiKey, targetModel, requestedGroup);
                    }
                    return string.Empty;

                case "apikey":
                    return currentApiKey ?? string.Empty;

                case "model":
                    return targetModel ?? string.Empty;

                case "group":
                    return requestedGroup ?? "all";

                case "client_ip":
                    return context?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                case "timestamp":
                    return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                case "uuid":
                    return Guid.NewGuid().ToString("N");

                default:
                    return match.Value;
            }
        });
    }
}
