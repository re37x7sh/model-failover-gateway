using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ModelFailoverGateway.Services;

/// <summary>
/// Windows 系统托盘与原生桌面萌宠管理服务
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

    internal void SetNotifyIcon(NotifyIcon icon)
    {
        _notifyIcon = icon;
    }

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
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => { };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 启动基于专用 ApplicationContext 的消息循环，确保托盘和宠物永不退出
                var appContext = new TrayApplicationContext(port, _serverUrl, alertService, this, _lifetime);
                Application.Run(appContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动 Windows 系统托盘图标失败");
            }
        })
        {
            IsBackground = false // 设置为主前台 UI 线程，防止被 CLR 静默销毁
        };

        // NOTE: Windows Forms 消息循环必须在 STA 单元线程上运行
        _trayThread.SetApartmentState(ApartmentState.STA);
        _trayThread.Start();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// 动态生成高对比度且 100% 兼容 Windows Shell 的托盘原生图标（紫底白色闪电 ⚡）
    /// NOTE: 必须通过 Icon.Clone 复制托管副本并调用 DestroyIcon 释放原始非托管句柄，防止 GC 后图标句柄失效被系统移除
    /// </summary>
    internal static Icon CreateGatewayIcon()
    {
        try
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 1. 绘制高对比度渐变背景圆形 (靛蓝/紫色 #6366f1)
                using var brush = new SolidBrush(Color.FromArgb(99, 102, 241));
                g.FillEllipse(brush, 1, 1, 30, 30);

                using var borderPen = new Pen(Color.FromArgb(199, 210, 254), 2f);
                g.DrawEllipse(borderPen, 1, 1, 30, 30);

                // 2. 绘制醒目的白色能量闪电 ⚡ 矢量图标
                var lightningPoints = new PointF[]
                {
                    new(17, 5), new(9, 17), new(15, 17),
                    new(13, 27), new(23, 14), new(17, 14)
                };
                g.FillPolygon(Brushes.White, lightningPoints);
            }

            // 3. 生成原生 HICON 句柄并深拷贝为持久的托管 Icon
            var hIcon = bmp.GetHicon();
            using var tempIcon = Icon.FromHandle(hIcon);
            var permanentIcon = (Icon)tempIcon.Clone();
            DestroyIcon(hIcon);

            return permanentIcon;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    internal static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    internal static void SetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch { }
    }

    /// <summary>
    /// 展示长任务完成气泡提醒与播放清脆提示音
    /// </summary>
    public void ShowTaskCompleteNotification(string model, long durationMs, long tokens, int turnCount = 1)
    {
        if (!IsTaskCompleteNotificationEnabled) return;

        var durationSec = durationMs / 1000.0;
        var durationText = durationSec >= 60 
            ? $"{(int)(durationSec / 60)}分{(int)(durationSec % 60)}秒" 
            : $"{durationSec:F1} 秒";

        var tokensText = tokens > 0 ? $" | 消耗 {tokens:N0} Tokens" : "";
        var turnText = turnCount > 1 ? $" (共 {turnCount} 步工具调用)" : "";

        ShowBalloonNotification(
            "🎉 AI 任务已全部完成！",
            $"模型: {model}{turnText}\n累计耗时: {durationText}{tokensText}",
            ToolTipIcon.Info
        );

        if (IsSoundEnabled)
        {
            PlayChimeSound();
        }
    }

    /// <summary>
    /// 在 Windows 系统托盘弹出通用气泡通知
    /// </summary>
    public void ShowBalloonNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (!IsNotificationEnabled) return;

        try
        {
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                _notifyIcon.ShowBalloonTip(4500, title, message, icon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发送系统托盘气泡通知失败");
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

    public void Dispose()
    {
        try
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
        catch { }
    }
}

/// <summary>
/// 专用于管理托盘图标与桌面悬浮窗生命周期的 ApplicationContext
/// </summary>
internal class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly DesktopPetForm? _petForm;
    private readonly IHostApplicationLifetime _lifetime;

    public TrayApplicationContext(int port, string serverUrl, IAlertService? alertService, TrayIconManager manager, IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;

        var contextMenu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("⚡ Model Failover Gateway")
        {
            Enabled = false,
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };

        var openDashboardItem = new ToolStripMenuItem("📊 打开 Web 控制台", null, (s, e) => TrayIconManager.OpenBrowser(serverUrl));

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
            manager.IsNotificationEnabled = toggleNotificationItem.Checked;
            toggleNotificationItem.Text = manager.IsNotificationEnabled ? "🔔 渠道异常气泡通知 (已开启)" : "🔕 渠道异常气泡通知 (已关闭)";
        };

        // 🎉 长任务完成提醒开关
        var toggleTaskCompleteItem = new ToolStripMenuItem("🎉 长任务完成提醒 (已开启)")
        {
            Checked = true,
            CheckOnClick = true
        };
        toggleTaskCompleteItem.Click += (s, e) =>
        {
            manager.IsTaskCompleteNotificationEnabled = toggleTaskCompleteItem.Checked;
            toggleTaskCompleteItem.Text = manager.IsTaskCompleteNotificationEnabled ? "🎉 长任务完成提醒 (已开启)" : "💤 长任务完成提醒 (已关闭)";
        };

        // 🔊 声音提示开关
        var toggleSoundItem = new ToolStripMenuItem("🔊 播放提示音 (已开启)")
        {
            Checked = true,
            CheckOnClick = true
        };
        toggleSoundItem.Click += (s, e) =>
        {
            manager.IsSoundEnabled = toggleSoundItem.Checked;
            toggleSoundItem.Text = manager.IsSoundEnabled ? "🔊 播放提示音 (已开启)" : "🔇 播放提示音 (已静音)";
            if (manager.IsSoundEnabled) manager.PlayChimeSound();
        };

        var copyClaudeItem = new ToolStripMenuItem("📋 复制 Claude 端点", null, (s, e) => TrayIconManager.SetClipboard($"{serverUrl}/claude"));
        var copyCodexItem = new ToolStripMenuItem("📋 复制 Codex 端点", null, (s, e) => TrayIconManager.SetClipboard($"{serverUrl}/codex"));
        var copyGeneralItem = new ToolStripMenuItem("📋 复制通用端点", null, (s, e) => TrayIconManager.SetClipboard(serverUrl));

        var exitItem = new ToolStripMenuItem("❌ 退出网关", null, (s, e) =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            _petForm?.Dispose();
            ExitThread();
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
            Icon = TrayIconManager.CreateGatewayIcon(),
            ContextMenuStrip = contextMenu,
            Text = $"Model Failover Gateway ({port})",
            Visible = true
        };

        manager.SetNotifyIcon(_notifyIcon);

        _notifyIcon.DoubleClick += (s, e) => TrayIconManager.OpenBrowser(serverUrl);

        if (alertService != null)
        {
            _petForm = new DesktopPetForm(alertService, manager);
            _petForm.Show();
        }

        _notifyIcon.ShowBalloonTip(
            2000,
            "Model Failover Gateway",
            $"本地智能故障转移网关已启动并在托盘常驻 (127.0.0.1:{port})",
            ToolTipIcon.Info
        );
    }
}
