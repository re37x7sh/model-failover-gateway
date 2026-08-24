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
    public bool IsTaskCompleteNotificationEnabled { get; set; } = true;
    public bool IsSoundEnabled { get; set; } = true;

    private DesktopPetForm? _petForm;

    /// <summary>
    /// 启动系统托盘图标与桌面悬浮宠物
    /// </summary>
    public void Start(int port = 5000, IAlertService? alertService = null)
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

                // 启动原生置顶贴边灵动桌面宠物
                if (alertService != null)
                {
                    _petForm = new DesktopPetForm(alertService, this);
                    _petForm.Show();
                }

                var contextMenu = new ContextMenuStrip();

                var titleItem = new ToolStripMenuItem("⚡ Model Failover Gateway")
                {
                    Enabled = false,
                    Font = new Font(Control.DefaultFont, FontStyle.Bold)
                };

                var openDashboardItem = new ToolStripMenuItem("📊 打开 Web 控制台", null, (s, e) => OpenBrowser(_serverUrl));

                // 🐾 桌面悬浮宠物开关
                var togglePetItem = new ToolStripMenuItem("🐾 桌面灵动悬浮宠物 (已开启)")
                {
                    Checked = true,
                    CheckOnClick = true
                };
                togglePetItem.Click += (s, e) =>
                {
                    if (_petForm != null)
                    {
                        if (togglePetItem.Checked)
                        {
                            _petForm.Show();
                            togglePetItem.Text = "🐾 桌面灵动悬浮宠物 (已开启)";
                        }
                        else
                        {
                            _petForm.Hide();
                            togglePetItem.Text = "🐾 桌面灵动悬浮宠物 (已隐藏)";
                        }
                    }
                };

                var statusItem = new ToolStripMenuItem($"🟢 状态: 运行中 ({port})") { Enabled = false };

                // 🔔 渠道异常通知开关
                var toggleNotificationItem = new ToolStripMenuItem("🔔 渠道异常气泡通知 (已开启)")
                {
                    Checked = true,
                    CheckOnClick = true
                };
                toggleNotificationItem.Click += (s, e) =>
                {
                    IsNotificationEnabled = toggleNotificationItem.Checked;
                    toggleNotificationItem.Text = IsNotificationEnabled ? "🔔 渠道异常气泡通知 (已开启)" : "🔕 渠道异常气泡通知 (已关闭)";
                };

                // 🎉 长任务完成提醒开关
                var toggleTaskCompleteItem = new ToolStripMenuItem("🎉 长任务完成提醒 (已开启)")
                {
                    Checked = true,
                    CheckOnClick = true
                };
                toggleTaskCompleteItem.Click += (s, e) =>
                {
                    IsTaskCompleteNotificationEnabled = toggleTaskCompleteItem.Checked;
                    toggleTaskCompleteItem.Text = IsTaskCompleteNotificationEnabled ? "🎉 长任务完成提醒 (已开启)" : "💤 长任务完成提醒 (已关闭)";
                };

                // 🔊 声音提示开关
                var toggleSoundItem = new ToolStripMenuItem("🔊 播放提示音 (已开启)")
                {
                    Checked = true,
                    CheckOnClick = true
                };
                toggleSoundItem.Click += (s, e) =>
                {
                    IsSoundEnabled = toggleSoundItem.Checked;
                    toggleSoundItem.Text = IsSoundEnabled ? "🔊 播放提示音 (已开启)" : "🔇 播放提示音 (已静音)";
                    if (IsSoundEnabled) PlayChimeSound();
                };

                var copyClaudeItem = new ToolStripMenuItem("📋 复制 Claude 端点", null, (s, e) => SetClipboard($"{_serverUrl}/claude"));
                var copyCodexItem = new ToolStripMenuItem("📋 复制 Codex 端点", null, (s, e) => SetClipboard($"{_serverUrl}/codex"));
                var copyGeneralItem = new ToolStripMenuItem("📋 复制通用端点", null, (s, e) => SetClipboard(_serverUrl));

                var exitItem = new ToolStripMenuItem("❌ 退出网关", null, (s, e) =>
                {
                    _notifyIcon?.Dispose();
                    _petForm?.Dispose();
                    Application.Exit();
                    _lifetime.StopApplication();
                });

                contextMenu.Items.Add(titleItem);
                contextMenu.Items.Add(statusItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(openDashboardItem);
                contextMenu.Items.Add(togglePetItem);
                contextMenu.Items.Add(new ToolStripSeparator());
                contextMenu.Items.Add(toggleTaskCompleteItem);
                contextMenu.Items.Add(toggleNotificationItem);
                contextMenu.Items.Add(toggleSoundItem);
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

                Application.Run(new ApplicationContext());
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

    private static void OpenStandalonePetWindow(string serverUrl)
    {
        var petUrl = $"{serverUrl}/pet";
        try
        {
            // 尝试使用 Edge / Chrome 应用小窗模式启动（无地址栏、无标签页、纯净独立悬浮）
            var edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
            if (!File.Exists(edgePath))
            {
                edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe");
            }

            if (File.Exists(edgePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = edgePath,
                    Arguments = $"--app=\"{petUrl}\" --window-size=220,260",
                    UseShellExecute = true
                });
                return;
            }

            // 降级使用系统默认浏览器打开独立宠物页面
            Process.Start(new ProcessStartInfo(petUrl) { UseShellExecute = true });
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
    /// 在 Windows 系统托盘弹出长任务完成气泡通知并播放提示音
    /// </summary>
    public void ShowTaskCompleteNotification(string model, long durationMs, long tokens)
    {
        if (IsSoundEnabled)
        {
            PlayChimeSound();
        }

        if (!IsTaskCompleteNotificationEnabled)
        {
            return;
        }

        try
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                var durationSec = durationMs / 1000.0;
                var modelText = string.IsNullOrWhiteSpace(model) ? "AI 编程模型" : model;
                var tokenText = tokens > 0 ? $" | 消耗 {tokens:N0} Tokens" : "";

                _notifyIcon.ShowBalloonTip(
                    4500,
                    "🎉 AI 任务已生成完毕！",
                    $"模型: {modelText}\n耗时: {durationSec:F1} 秒{tokenText}",
                    ToolTipIcon.Info
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送长任务完成气泡通知失败");
        }
    }

    /// <summary>
    /// 播放清脆的提示音
    /// </summary>
    public void PlayChimeSound()
    {
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
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
