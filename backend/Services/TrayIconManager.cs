using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ModelFailoverGateway.Services;

/// <summary>
/// Windows 系统托盘管理服务
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayIconManager> _logger;
    private NotifyIcon? _notifyIcon;
    private Thread? _trayThread;
    private string _serverUrl = "http://127.0.0.1:5000";

    public TrayIconManager(IHostApplicationLifetime lifetime, ILogger<TrayIconManager> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    public bool IsNotificationEnabled { get; set; } = true;

    /// <summary>
    /// 启动系统托盘图标
    /// </summary>
    public void Start(int port = 5000)
    {
        _serverUrl = $"http://127.0.0.1:{port}";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        _trayThread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var contextMenu = new ContextMenuStrip();

                var titleItem = new ToolStripMenuItem("⚡ Model Failover Gateway")
                {
                    Enabled = false,
                    Font = new Font(Control.DefaultFont, FontStyle.Bold)
                };

                var openDashboardItem = new ToolStripMenuItem("📊 打开 Web 控制台", null, (s, e) => OpenBrowser(_serverUrl));
                var statusItem = new ToolStripMenuItem($"🟢 状态: 运行中 ({port})") { Enabled = false };

                // 🔔 渠道异常通知开关菜单项
                var toggleNotificationItem = new ToolStripMenuItem("🔔 渠道异常气泡通知 (已开启)")
                {
                    Checked = true,
                    CheckOnClick = true
                };

                toggleNotificationItem.Click += (s, e) =>
                {
                    IsNotificationEnabled = toggleNotificationItem.Checked;
                    if (IsNotificationEnabled)
                    {
                        toggleNotificationItem.Text = "🔔 渠道异常气泡通知 (已开启)";
                        ShowBalloonNotification("通知已开启", "当渠道发生异常或欠费时将通过气泡提醒", ToolTipIcon.Info);
                    }
                    else
                    {
                        toggleNotificationItem.Text = "🔕 渠道异常气泡通知 (已关闭)";
                    }
                };

                var copyClaudeItem = new ToolStripMenuItem("📋 复制 Claude 端点", null, (s, e) => SetClipboard($"{_serverUrl}/claude"));
                var copyCodexItem = new ToolStripMenuItem("📋 复制 Codex 端点", null, (s, e) => SetClipboard($"{_serverUrl}/codex"));
                var copyGeneralItem = new ToolStripMenuItem("📋 复制通用端点", null, (s, e) => SetClipboard(_serverUrl));

                var exitItem = new ToolStripMenuItem("❌ 退出网关", null, (s, e) =>
                {
                    _notifyIcon?.Dispose();
                    Application.Exit();
                    _lifetime.StopApplication();
                });

                contextMenu.Items.Add(titleItem);
                contextMenu.Items.Add(statusItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(openDashboardItem);
                contextMenu.Items.Add(toggleNotificationItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(copyClaudeItem);
                contextMenu.Items.Add(copyCodexItem);
                contextMenu.Items.Add(copyGeneralItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(exitItem);

                _notifyIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Shield,
                    ContextMenuStrip = contextMenu,
                    Text = $"Model Failover Gateway (127.0.0.1:{port})",
                    Visible = true
                };

                // NOTE: 双击托盘图标直接唤起 Web 控制台
                _notifyIcon.DoubleClick += (s, e) => OpenBrowser(_serverUrl);

                _notifyIcon.ShowBalloonTip(
                    2000,
                    "Model Failover Gateway",
                    $"本地智能故障转移网关已启动并在托盘常驻 (127.0.0.1:{port})",
                    ToolTipIcon.Info
                );

                Application.Run();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动 Windows 系统托盘图标失败");
            }
        })
        {
            IsBackground = true
        };

        // NOTE: Windows Forms 消息循环必须在 STA 单元线程上运行
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private static void SetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch { }
    }

    /// <summary>
    /// 在 Windows 系统托盘弹出告警气泡通知
    /// </summary>
    public void ShowBalloonNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Warning)
    {
        // 若用户已手动关闭气泡通知，且非重要系统提示则直接跳过
        if (!IsNotificationEnabled && icon != ToolTipIcon.Info)
        {
            return;
        }

        try
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(4000, title, text, icon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送系统托盘告警通知失败");
        }
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
