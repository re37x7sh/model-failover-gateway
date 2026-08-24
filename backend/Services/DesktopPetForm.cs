using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// Windows 原生置顶·贴边吸附隐藏·经典 Bongo Cat 敲键盘灵动桌面宠物
/// 支持双爪交替敲击、工具态放大镜、欢呼撒花、多行高保真气泡卡片与紧凑贴合状态底座
/// </summary>
public class DesktopPetForm : Form
{
    private readonly IAlertService _alertService;
    private readonly TrayIconManager _trayManager;
    private readonly System.Windows.Forms.Timer _animTimer;
    private readonly System.Windows.Forms.Timer _dockTimer;

    // 拖拽与贴边状态
    private bool _isDragging = false;
    private Point _dragCursorStart;
    private Point _dragFormStart;
    private enum DockSide { None, Left, Right, Top }
    private DockSide _dockSide = DockSide.None;
    private bool _isCollapsed = false;
    private int _hoverStayTicks = 0;

    // 动画帧计数与状态机
    private int _tickCount = 0;
    private bool _isBlinking = false;
    private int _blinkTimer = 0;
    private int _celebrateTicks = 0;
    private string _prevState = "idle";

    // 气泡对话框 (支持高颜值玻璃拟态、多行自适应卡片与实时状态提示)
    private string _bubbleText = "今天也要元气满满写代码哦！✨";
    private int _bubbleFadeTicks = 180; // 约 6 秒
    private RectangleF _bubbleCloseRect = RectangleF.Empty;
    private float _renderedBubbleBottom = 0;

    // 经典 Bongo 皮肤模式 (classic: 敲键盘猫, cyber: 赛博猫, shiba: 柴犬)
    private string _avatar = "bongo";

    private readonly string[] _idleQuotes = new[]
    {
        "🍵 代码写累了吗？记得喝口水哦~",
        "⚡ 网关正在全天候守护您的 API 链路！",
        "🚀 今天也是效率拉满的一天！",
        "✨ 点击我可以切换专属鼓励语哦~",
        "🐱 喵~ 随时准备为您敲键盘！",
        "⚡ Claude / Codex 链路畅通无阻！"
    };

    private static Rectangle GetWorkingArea()
    {
        return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
    }

    // Windows 无激活置顶样式支持 (不抢占 VSCode 输入焦点)
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (不在 Alt+Tab 出现)
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (点击不抢占 VSCode 焦点)
            return cp;
        }
    }

    public DesktopPetForm(IAlertService alertService, TrayIconManager trayManager)
    {
        _alertService = alertService;
        _trayManager = trayManager;

        // 窗体基础属性 (扩充到 250x220 确保多行气泡与底部徽章完整展示且紧凑)
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(250, 220);

        // 高质量透明底色与双缓冲防闪烁
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

        // 初始位置：屏幕右下角 (工作区内)
        var wa = GetWorkingArea();
        Location = new Point(wa.Right - 260, wa.Bottom - 240);

        // 动画刷新定时器 (33ms 约 30 FPS，实现丝滑 Bongo 拍打节奏)
        _animTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _animTimer.Tick += OnAnimationTick;
        _animTimer.Start();

        // 贴边自动收缩检测定时器
        _dockTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _dockTimer.Tick += OnDockCheckTick;
        _dockTimer.Start();

        // 鼠标右键快捷菜单
        InitContextMenu();
    }

    private void InitContextMenu()
    {
        var menu = new ContextMenuStrip();
        var bongoItem = new ToolStripMenuItem("🐱 切换为 Bongo Cat 敲鼓猫", null, (s, e) => { _avatar = "bongo"; ShowBubble("经典 Bongo 敲键盘猫就位！🐾"); });
        var cyberItem = new ToolStripMenuItem("🤖 切换为赛博机甲猫", null, (s, e) => { _avatar = "cyber"; ShowBubble("赛博机甲已连接！⚡"); });
        var dogItem = new ToolStripMenuItem("🐶 切换为忠诚柴犬", null, (s, e) => { _avatar = "dog"; ShowBubble("汪汪！柴犬为您护航~ ✨"); });

        var soundItem = new ToolStripMenuItem("🔊 提示音开关", null, (s, e) => {
            _trayManager.IsSoundEnabled = !_trayManager.IsSoundEnabled;
            ShowBubble(_trayManager.IsSoundEnabled ? "提示音已开启 🔊" : "提示音已静音 🔇");
        });

        var dashboardItem = new ToolStripMenuItem("📊 打开 Web 控制台", null, (s, e) => {
            try { Process.Start(new ProcessStartInfo("http://127.0.0.1:5000") { UseShellExecute = true }); } catch { }
        });

        var hideItem = new ToolStripMenuItem("❌ 隐藏桌面宠物", null, (s, e) => {
            Hide();
        });

        menu.Items.Add(bongoItem);
        menu.Items.Add(cyberItem);
        menu.Items.Add(dogItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(soundItem);
        menu.Items.Add(dashboardItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(hideItem);

        ContextMenuStrip = menu;
    }

    public void ShowBubble(string text, int durationMs = 6000)
    {
        _bubbleText = text;
        _bubbleFadeTicks = durationMs / 33;
        Invalidate();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        try
        {
            _tickCount++;

            // 眨眼逻辑
            if (_blinkTimer > 0)
            {
                _blinkTimer--;
                if (_blinkTimer == 0) _isBlinking = false;
            }
            else if (_tickCount % 90 == 0) // 每约 3 秒眨眼一次
            {
                _isBlinking = true;
                _blinkTimer = 4; // 闭眼 4 帧
            }

            if (_celebrateTicks > 0) _celebrateTicks--;
            if (_bubbleFadeTicks > 0) _bubbleFadeTicks--;

            // 获取实时状态
            var status = _alertService.GetCurrentTaskStatus();

            // 智能状态变迁与实时气泡同步展示（支持多行完整详细信息）
            if (status.State == "thinking")
            {
                if (_prevState != "thinking" && _prevState != "tool_use")
                {
                    var turnTip = status.TurnCount > 1 ? $" (第 {status.TurnCount} 步)" : "";
                    var modelTip = !string.IsNullOrEmpty(status.Model) ? $"\n模型: {status.Model}" : "";
                    ShowBubble($"⚡ 正在思考生成中...{turnTip}{modelTip}", 15000);
                }
            }
            else if (status.State == "tool_use")
            {
                if (_prevState != "tool_use")
                {
                    var turnTip = status.TurnCount > 0 ? $" (第 {status.TurnCount} 步)" : "";
                    ShowBubble($"🔍 正在调用工具处理中...{turnTip}", 10000);
                }
            }
            else if (status.State == "completed")
            {
                if (_prevState == "thinking" || _prevState == "tool_use")
                {
                    _celebrateTicks = 90; // 庆祝撒花 3 秒
                    var totalMs = status.SessionDurationMs > 0 ? status.SessionDurationMs : status.DurationMs;
                    var durSec = totalMs / 1000.0;
                    var durText = durSec >= 60 ? $"{(int)(durSec / 60)}分{(int)(durSec % 60)}秒" : $"{durSec:F1}s";
                    var tokenStr = status.SessionTotalTokens > 0 
                        ? $"\n(累计 {status.SessionTotalTokens:N0} Tokens)" 
                        : (status.TotalTokens > 0 ? $"\n(消耗 {status.TotalTokens:N0} Tokens)" : "");
                    var turnInfo = status.TurnCount > 1 ? $"共 {status.TurnCount} 步，" : "";
                    ShowBubble($"🎉 任务全部完成啦！\n{turnInfo}总耗时 {durText}{tokenStr}", 8000);
                }
            }
            else if (status.State == "failover")
            {
                if (_prevState != "failover")
                {
                    ShowBubble($"⚠️ 触发渠道故障转移，已切换备用！", 6000);
                }
            }

            _prevState = status.State;

            Invalidate();
        }
        catch { }
    }

    private void OnDockCheckTick(object? sender, EventArgs e)
    {
        try
        {
            if (_isDragging) return;

            var cursorPos = Cursor.Position;
            var isMouseOver = Bounds.Contains(cursorPos);
            var wa = GetWorkingArea();

            // 判断当前吸附边缘
            if (_dockSide != DockSide.None)
            {
                if (isMouseOver)
                {
                    _hoverStayTicks = 10;
                    if (_isCollapsed)
                    {
                        // 展开滑出
                        SlideOut(wa);
                    }
                }
                else
                {
                    if (_hoverStayTicks > 0)
                    {
                        _hoverStayTicks--;
                    }
                    else if (!_isCollapsed)
                    {
                        // 收缩进屏幕边框
                        CollapseToEdge(wa);
                    }
                }
            }
        }
        catch { }
    }

    private void SlideOut(Rectangle wa)
    {
        _isCollapsed = false;
        switch (_dockSide)
        {
            case DockSide.Left:
                Location = new Point(wa.Left, Top);
                break;
            case DockSide.Right:
                Location = new Point(wa.Right - Width, Top);
                break;
            case DockSide.Top:
                Location = new Point(Left, wa.Top);
                break;
        }
    }

    private void CollapseToEdge(Rectangle wa)
    {
        _isCollapsed = true;
        const int PEEK_WIDTH = 32; // 贴边仅露出 32 像素的软萌猫耳
        switch (_dockSide)
        {
            case DockSide.Left:
                Location = new Point(wa.Left - Width + PEEK_WIDTH, Top);
                break;
            case DockSide.Right:
                Location = new Point(wa.Right - PEEK_WIDTH, Top);
                break;
            case DockSide.Top:
                Location = new Point(Left, wa.Top - Height + PEEK_WIDTH);
                break;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            // 判断是否点击了气泡关闭按钮 ✕
            if (_bubbleFadeTicks > 0 && _bubbleCloseRect.Contains(e.Location))
            {
                _bubbleFadeTicks = 0;
                Invalidate();
                return;
            }

            _isDragging = true;
            _dragCursorStart = Cursor.Position;
            _dragFormStart = Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isDragging)
        {
            var deltaX = Cursor.Position.X - _dragCursorStart.X;
            var deltaY = Cursor.Position.Y - _dragCursorStart.Y;
            Location = new Point(_dragFormStart.X + deltaX, _dragFormStart.Y + deltaY);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isDragging)
        {
            _isDragging = false;
            var deltaX = Math.Abs(Location.X - _dragFormStart.X);
            var deltaY = Math.Abs(Location.Y - _dragFormStart.Y);

            // 如果位移极小（< 5px），判定为单纯的左键点击小猫交互
            if (deltaX < 5 && deltaY < 5)
            {
                HandlePetClick();
                return;
            }

            var wa = GetWorkingArea();

            // 检测贴边吸附 (靠近边缘 35 像素内自动吸附)
            if (Left <= wa.Left + 35)
            {
                _dockSide = DockSide.Left;
                Location = new Point(wa.Left, Top);
            }
            else if (Right >= wa.Right - 35)
            {
                _dockSide = DockSide.Right;
                Location = new Point(wa.Right - Width, Top);
            }
            else if (Top <= wa.Top + 35)
            {
                _dockSide = DockSide.Top;
                Location = new Point(Left, wa.Top);
            }
            else
            {
                _dockSide = DockSide.None;
                _isCollapsed = false;
            }
        }
    }

    private void HandlePetClick()
    {
        var status = _alertService.GetCurrentTaskStatus();
        if (status.State == "idle")
        {
            var q = _idleQuotes[new Random().Next(_idleQuotes.Length)];
            ShowBubble(q, 5000);
            if (_trayManager.IsSoundEnabled)
            {
                _trayManager.PlayChimeSound();
            }
        }
        else if (status.State == "thinking" || status.State == "tool_use")
        {
            var turnTip = status.TurnCount > 1 ? $" (第 {status.TurnCount} 步)" : "";
            var modelTip = !string.IsNullOrEmpty(status.Model) ? $"\n模型: {status.Model}" : "";
            ShowBubble($"⚡ 正在思考生成中...{turnTip}{modelTip}", 5000);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        try
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var status = _alertService.GetCurrentTaskStatus();
            var isThinking = status.State == "thinking";
            var isToolUse = status.State == "tool_use";
            var isCompleted = status.State == "completed" || _celebrateTicks > 0;

            // 1. 绘制顶部高颜值气泡对话框（支持多行完整展示与 ✕ 关闭按钮）
            var hasBubble = _bubbleFadeTicks > 0 && !string.IsNullOrEmpty(_bubbleText);
            if (hasBubble)
            {
                DrawSpeechBubble(g, _bubbleText);
            }
            else
            {
                _bubbleCloseRect = RectangleF.Empty;
                _renderedBubbleBottom = 8;
            }

            // 2. 猫咪主体垂直紧随气泡底部，消除多余空白
            var catY = (int)Math.Max(_renderedBubbleBottom, 12);
            var petRect = new Rectangle((Width - 120) / 2, catY, 120, 96);

            if (_avatar == "bongo")
            {
                DrawBongoCat(g, petRect, isThinking, isToolUse, isCompleted);
            }
            else if (_avatar == "cyber")
            {
                DrawCyberCat(g, petRect, isThinking, isCompleted);
            }
            else
            {
                DrawDog(g, petRect, isThinking, isCompleted);
            }

            // 3. 状态底座徽章：紧贴键盘桌面下方（仅向下偏移 18px），彻底消除下方大空隙！
            if (isThinking || isToolUse)
            {
                var totalElapsedSec = (int)(DateTime.Now - (status.SessionStartTime != DateTime.MinValue ? status.SessionStartTime : status.Timestamp)).TotalSeconds;
                var badgeY = petRect.Y + 68 + 18; // 键盘底边直接吸附
                DrawTimerBadge(g, totalElapsedSec, status.TurnCount, isToolUse, badgeY);
            }
        }
        catch { }
    }

    /// <summary>
    /// 经典 Bongo Cat 敲键盘猫完整绘制逻辑
    /// </summary>
    private void DrawBongoCat(Graphics g, Rectangle r, bool isThinking, bool isToolUse, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.45)) * 14f : 0f;
        var earTwitch = (float)Math.Sin(_tickCount * 0.12) * 2.5f;
        var cy = r.Y - jumpOffset;

        using var blackPen = new Pen(Color.FromArgb(30, 41, 59), 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
        using var pinkBrush = new SolidBrush(Color.FromArgb(253, 164, 175)); // #FDA4AF 腮红粉
        using var innerEarBrush = new SolidBrush(Color.FromArgb(254, 205, 211)); // 软耳粉

        // 1. 身体与头部轮廓（纯白圆润底座）
        var headRect = new RectangleF(r.X + 18, cy + 8, 84, 70);
        g.FillEllipse(whiteBrush, headRect);

        // 2. 猫耳朵（带小内耳与晃动）
        var leftEar = new PointF[] { new(r.X + 22, cy + 24), new(r.X + 14, cy - 4 + earTwitch), new(r.X + 44, cy + 12) };
        var rightEar = new PointF[] { new(r.X + 76, cy + 12), new(r.X + 106, cy - 4 - earTwitch), new(r.X + 98, cy + 24) };
        g.FillPolygon(whiteBrush, leftEar);
        g.FillPolygon(whiteBrush, rightEar);
        g.DrawPolygon(blackPen, leftEar);
        g.DrawPolygon(blackPen, rightEar);

        // 内耳粉色三角形
        var innerLeftEar = new PointF[] { new(r.X + 24, cy + 20), new(r.X + 18, cy + 1 + earTwitch), new(r.X + 38, cy + 13) };
        var innerRightEar = new PointF[] { new(r.X + 82, cy + 13), new(r.X + 102, cy + 1 - earTwitch), new(r.X + 96, cy + 20) };
        g.FillPolygon(innerEarBrush, innerLeftEar);
        g.FillPolygon(innerEarBrush, innerRightEar);

        // 头部主轮廓描边
        g.DrawArc(blackPen, headRect.X, headRect.Y, headRect.Width, headRect.Height, 20, 320);

        // 3. 腮红
        g.FillEllipse(pinkBrush, r.X + 24, cy + 42, 14, 8);
        g.FillEllipse(pinkBrush, r.X + 82, cy + 42, 14, 8);

        // 4. 眼睛
        if (_isBlinking)
        {
            // 眨眼闭眼线
            g.DrawArc(blackPen, r.X + 36, cy + 32, 12, 8, 190, 160);
            g.DrawArc(blackPen, r.X + 72, cy + 32, 12, 8, 190, 160);
        }
        else if (isCompleted)
        {
            // 欢呼开心弯弯眼 ^ ^
            using var happyPen = new Pen(Color.FromArgb(30, 41, 59), 3f);
            g.DrawArc(happyPen, r.X + 34, cy + 26, 16, 12, 200, 140);
            g.DrawArc(happyPen, r.X + 70, cy + 26, 16, 12, 200, 140);
        }
        else
        {
            // 经典黑色大水灵豆豆眼
            g.FillEllipse(Brushes.Black, r.X + 37, cy + 28, 11, 13);
            g.FillEllipse(Brushes.Black, r.X + 72, cy + 28, 11, 13);
            // 眼睛高光闪亮小白点
            g.FillEllipse(Brushes.White, r.X + 39, cy + 30, 4, 4);
            g.FillEllipse(Brushes.White, r.X + 74, cy + 30, 4, 4);
        }

        // 5. 鼻子与嘴巴 (ω 形状)
        g.FillPolygon(pinkBrush, new PointF[] { new(r.X + 57, cy + 40), new(r.X + 63, cy + 40), new(r.X + 60, cy + 44) });
        if (isCompleted)
        {
            // 开心张嘴笑
            g.FillEllipse(pinkBrush, r.X + 54, cy + 45, 12, 10);
            g.DrawEllipse(blackPen, r.X + 54, cy + 45, 12, 10);
        }
        else
        {
            g.DrawArc(blackPen, r.X + 51, cy + 42, 9, 8, 20, 150);
            g.DrawArc(blackPen, r.X + 60, cy + 42, 9, 8, 10, 150);
        }

        // 6. 木质小桌面与键盘 (桌面固定在下方)
        var deskY = r.Y + 68;
        using var deskBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
        using var deskBorderPen = new Pen(Color.FromArgb(148, 163, 184), 2f);
        g.FillRoundedRectangle(deskBrush, r.X + 6, deskY, 108, 24, 6);
        g.DrawRoundedRectangle(deskBorderPen, r.X + 6, deskY, 108, 24, 6);

        // 迷你赛博键盘
        using var kbBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        g.FillRoundedRectangle(kbBrush, r.X + 26, deskY + 4, 68, 16, 4);
        // 键盘上彩色小键帽
        var keyColors = new[] { Color.FromArgb(56, 189, 248), Color.FromArgb(168, 85, 247), Color.FromArgb(236, 72, 153), Color.FromArgb(34, 197, 94) };
        for (int i = 0; i < 4; i++)
        {
            using var kBrush = new SolidBrush(keyColors[i % keyColors.Length]);
            g.FillRectangle(kBrush, r.X + 32 + i * 15, deskY + 7, 10, 8);
        }

        // 7. Bongo Cat 核心灵魂：两只白色小肉爪拍打动画！
        if (isCompleted)
        {
            // 完成时：双爪举过头顶欢呼！
            g.FillEllipse(whiteBrush, r.X + 16, cy + 2, 18, 22);
            g.DrawEllipse(blackPen, r.X + 16, cy + 2, 18, 22);

            g.FillEllipse(whiteBrush, r.X + 86, cy + 2, 18, 22);
            g.DrawEllipse(blackPen, r.X + 86, cy + 2, 18, 22);

            // 撒花彩带粒子
            DrawConfetti(g, r, _tickCount);
        }
        else if (isToolUse)
        {
            // 工具调用态：左手撑桌，右手举放大镜 🔍
            g.FillEllipse(whiteBrush, r.X + 18, deskY - 4, 20, 16);
            g.DrawEllipse(blackPen, r.X + 18, deskY - 4, 20, 16);

            // 右手举起放大镜
            var glassX = r.X + 86;
            var glassY = cy + 18;
            g.FillEllipse(whiteBrush, glassX - 4, glassY + 12, 18, 16);
            g.DrawEllipse(blackPen, glassX - 4, glassY + 12, 18, 16);

            // 绘制小放大镜 🔍
            using var glassPen = new Pen(Color.FromArgb(99, 102, 241), 2.5f);
            g.DrawEllipse(glassPen, glassX, glassY, 14, 14);
            g.DrawLine(glassPen, glassX + 11, glassY + 11, glassX + 18, glassY + 18);
        }
        else if (isThinking)
        {
            // 思考态：极速交替拍打打字 (6 帧一个节拍循环，啪嗒啪嗒敲键盘)
            var beat = (_tickCount / 3) % 2; // 0 或 1 节拍
            var leftPawDown = beat == 0;
            var rightPawDown = beat == 1;

            var leftPawY = leftPawDown ? deskY + 1 : deskY - 10;
            var rightPawY = rightPawDown ? deskY + 1 : deskY - 10;

            // 左肉爪
            g.FillEllipse(whiteBrush, r.X + 18, leftPawY, 20, 18);
            g.DrawEllipse(blackPen, r.X + 18, leftPawY, 20, 18);

            // 右肉爪
            g.FillEllipse(whiteBrush, r.X + 82, rightPawY, 20, 18);
            g.DrawEllipse(blackPen, r.X + 82, rightPawY, 20, 18);

            // 敲击键帽闪烁小火花
            var activeKeyX = leftPawDown ? r.X + 34 : r.X + 78;
            using var sparkBrush = new SolidBrush(Color.Gold);
            g.FillPolygon(sparkBrush, new PointF[] {
                new(activeKeyX, deskY - 2), new(activeKeyX + 3, deskY - 6), new(activeKeyX + 6, deskY - 2), new(activeKeyX + 3, deskY + 2)
            });
        }
        else
        {
            // 待机态：双爪乖巧平放在小键盘上
            g.FillEllipse(whiteBrush, r.X + 22, deskY + 2, 20, 16);
            g.DrawEllipse(blackPen, r.X + 22, deskY + 2, 20, 16);

            g.FillEllipse(whiteBrush, r.X + 78, deskY + 2, 20, 16);
            g.DrawEllipse(blackPen, r.X + 78, deskY + 2, 20, 16);
        }
    }

    private void DrawCyberCat(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.45)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        using var bodyBrush = new SolidBrush(Color.FromArgb(30, 41, 59)); // #1E293B
        using var bodyBorder = new Pen(Color.FromArgb(99, 102, 241), 2.5f); // #6366F1
        using var neonCyan = new SolidBrush(Color.FromArgb(56, 189, 248)); // #38BDF8

        var headRect = new RectangleF(r.X + 20, cy + 16, 80, 68);
        g.FillRoundedRectangle(bodyBrush, headRect, 14);
        g.DrawRoundedRectangle(bodyBorder, headRect, 14);

        // 机器人耳朵
        var leftEar = new PointF[] { new(r.X + 24, cy + 18), new(r.X + 16, cy + 2), new(r.X + 44, cy + 16) };
        var rightEar = new PointF[] { new(r.X + 76, cy + 16), new(r.X + 104, cy + 2), new(r.X + 96, cy + 18) };
        g.FillPolygon(bodyBrush, leftEar);
        g.FillPolygon(bodyBrush, rightEar);
        g.DrawPolygon(bodyBorder, leftEar);
        g.DrawPolygon(bodyBorder, rightEar);

        // 护目镜面罩
        var visorRect = new RectangleF(r.X + 28, cy + 30, 64, 26);
        using var visorBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillRoundedRectangle(visorBrush, visorRect, 8);
        g.DrawRoundedRectangle(new Pen(Color.FromArgb(56, 189, 248), 1.5f), visorRect, 8);

        // 电子眼睛
        if (_isBlinking)
        {
            g.DrawLine(new Pen(Color.FromArgb(56, 189, 248), 2.5f), r.X + 36, cy + 43, r.X + 48, cy + 43);
            g.DrawLine(new Pen(Color.FromArgb(56, 189, 248), 2.5f), r.X + 72, cy + 43, r.X + 84, cy + 43);
        }
        else
        {
            g.FillEllipse(neonCyan, r.X + 36, cy + 38, 12, 10);
            g.FillEllipse(neonCyan, r.X + 72, cy + 38, 12, 10);
        }
    }

    private void DrawDog(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.45)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        using var bodyBrush = new SolidBrush(Color.FromArgb(245, 158, 11)); // 柴犬黄
        using var bodyBorder = new Pen(Color.FromArgb(180, 83, 9), 2.5f);
        using var whiteBrush = new SolidBrush(Color.White);

        var headRect = new RectangleF(r.X + 20, cy + 16, 80, 68);
        g.FillEllipse(bodyBrush, headRect);
        g.DrawEllipse(bodyBorder, headRect);

        // 白脸颊
        g.FillEllipse(whiteBrush, r.X + 26, cy + 38, 68, 42);

        // 眼睛
        g.FillEllipse(Brushes.Black, r.X + 38, cy + 34, 9, 11);
        g.FillEllipse(Brushes.Black, r.X + 73, cy + 34, 9, 11);

        // 鼻子
        g.FillEllipse(Brushes.Black, r.X + 55, cy + 44, 10, 8);
    }

    private void DrawConfetti(Graphics g, Rectangle r, int tick)
    {
        var colors = new[] { Color.Gold, Color.DeepSkyBlue, Color.HotPink, Color.LimeGreen, Color.Coral };
        for (int i = 0; i < 8; i++)
        {
            var angle = (tick * 0.15 + i * 0.8);
            var dist = 36 + (i * 5) % 25;
            var cx = r.X + 60 + (float)Math.Cos(angle) * dist;
            var cy = r.Y + 25 + (float)Math.Sin(angle) * dist;

            using var brush = new SolidBrush(colors[i % colors.Length]);
            g.FillEllipse(brush, cx, cy, 6, 6);
        }
    }

    /// <summary>
    /// 绘制高颜值暗色玻璃拟态气泡对话框（支持多行自适应、向下指示箭头与 ✕ 关闭按钮）
    /// </summary>
    private void DrawSpeechBubble(Graphics g, string text)
    {
        // 1. 分离前缀 Emoji 与文字内容，防止 GDI+ 缺少中文字体 Emoji 回退导致方框乱码 ▯
        string icon = "";
        string message = text;

        var match = System.Text.RegularExpressions.Regex.Match(text, @"^([\uD800-\uDBFF][\uDC00-\uDFFF]|[\u2600-\u27BF]|\p{So}|\p{Cs})\s*");
        if (match.Success)
        {
            icon = match.Groups[1].Value;
            message = text.Substring(match.Length).Trim();
        }

        using var fontText = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        using var fontEmoji = new Font("Segoe UI Emoji", 8.5f);

        var sizeEmoji = !string.IsNullOrEmpty(icon) ? g.MeasureString(icon, fontEmoji) : SizeF.Empty;
        
        // 测量文本多行尺寸（最大文本宽度 185px）
        var maxTextW = 185f;
        var sizeText = g.MeasureString(message, fontText, (int)maxTextW);

        var closeBtnWidth = 18f;
        var totalContentWidth = (sizeEmoji.Width > 0 ? sizeEmoji.Width : 0) + sizeText.Width;
        var bubbleW = Math.Min(Math.Max(totalContentWidth + closeBtnWidth + 24, 120), Width - 14);
        var bubbleH = Math.Max(sizeText.Height + 12, 28f);
        var bx = (Width - bubbleW) / 2;
        var by = 4f;

        // 2. 深色半透明玻璃拟态背景
        using var bgBrush = new SolidBrush(Color.FromArgb(240, 15, 23, 42)); // #0F172A 深海军蓝
        using var borderPen = new Pen(Color.FromArgb(99, 102, 241), 1.6f);   // #6366F1 紫蓝微光边框

        var rect = new RectangleF(bx, by, bubbleW, bubbleH);
        g.FillRoundedRectangle(bgBrush, rect, 12);
        g.DrawRoundedRectangle(borderPen, rect, 12);

        // 3. 气泡下方向下小箭头
        var arrowPoints = new PointF[]
        {
            new(Width / 2 - 5, by + bubbleH - 1),
            new(Width / 2 + 5, by + bubbleH - 1),
            new(Width / 2, by + bubbleH + 5)
        };
        g.FillPolygon(bgBrush, arrowPoints);
        g.DrawLine(borderPen, Width / 2 - 5, by + bubbleH - 1, Width / 2, by + bubbleH + 5);
        g.DrawLine(borderPen, Width / 2 + 5, by + bubbleH - 1, Width / 2, by + bubbleH + 5);

        // 4. 绘制图标 (采用 Segoe UI Emoji 专属字体，彻底根除 ▯ 乱码方框)
        float curX = bx + 9;
        using var textBrush = new SolidBrush(Color.FromArgb(248, 250, 252));

        if (!string.IsNullOrEmpty(icon))
        {
            g.DrawString(icon, fontEmoji, textBrush, curX, by + 5f);
            curX += sizeEmoji.Width - 4;
        }

        // 5. 绘制多行文本（支持换行，完整呈现全部信息）
        var textRect = new RectangleF(curX, by + 5f, bubbleW - (curX - bx) - closeBtnWidth - 4, bubbleH - 8);
        using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
        g.DrawString(message, fontText, textBrush, textRect, sf);

        // 6. ✕ 关闭按钮
        var closeX = bx + bubbleW - 17;
        var closeY = by + 6f;
        _bubbleCloseRect = new RectangleF(closeX - 4, by + 2, 20, 24);

        using var closePen = new Pen(Color.FromArgb(148, 163, 184), 1.5f);
        g.DrawLine(closePen, closeX, closeY, closeX + 8, closeY + 8);
        g.DrawLine(closePen, closeX + 8, closeY, closeX, closeY + 8);

        _renderedBubbleBottom = by + bubbleH + 5;
    }

    /// <summary>
    /// 绘制底部发光渐变状态徽章 (⚡ 02:10 (第 4 步))
    /// </summary>
    private void DrawTimerBadge(Graphics g, int totalSeconds, int turnCount, bool isToolUse, float badgeY)
    {
        var timeText = totalSeconds >= 60 
            ? $"{(totalSeconds / 60):D2}:{(totalSeconds % 60):D2}" 
            : $"{totalSeconds}s";

        var turnText = turnCount > 1 ? $" (第 {turnCount} 步)" : "";
        var icon = isToolUse ? "🔍" : "⚡";
        var text = $" {timeText}{turnText}";

        using var fontEmoji = new Font("Segoe UI Emoji", 8f);
        using var fontMono = new Font("Consolas", 8.8f, FontStyle.Bold);

        var sizeIcon = g.MeasureString(icon, fontEmoji);
        var sizeText = g.MeasureString(text, fontMono);
        var badgeW = sizeIcon.Width + sizeText.Width + 10;
        var badgeH = 20f;
        var bx = (Width - badgeW) / 2;

        var rect = new RectangleF(bx, badgeY, badgeW, badgeH);

        // 渐变金橙色/红底色
        var startColor = isToolUse ? Color.FromArgb(234, 88, 12) : Color.FromArgb(245, 158, 11);
        var endColor = isToolUse ? Color.FromArgb(249, 115, 22) : Color.FromArgb(239, 68, 68);

        using var gradBrush = new LinearGradientBrush(rect, startColor, endColor, LinearGradientMode.Horizontal);
        using var borderPen = new Pen(Color.FromArgb(254, 240, 138), 1.2f); // 金黄高亮外圈

        g.FillRoundedRectangle(gradBrush, rect, 10);
        g.DrawRoundedRectangle(borderPen, rect, 10);

        // 绘制图标与时间文本
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(icon, fontEmoji, textBrush, bx + 6, badgeY + 2f);
        g.DrawString(text, fontMono, textBrush, bx + 6 + sizeIcon.Width - 4, badgeY + 2f);
    }
}

/// <summary>
/// GDI+ 圆角矩形绘制扩展工具
/// </summary>
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        using var path = CreateRoundedRectanglePath(x, y, width, height, radius);
        g.FillPath(brush, path);
    }

    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF r, float radius)
    {
        g.FillRoundedRectangle(brush, r.X, r.Y, r.Width, r.Height, radius);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, float x, float y, float width, float height, float radius)
    {
        using var path = CreateRoundedRectanglePath(x, y, width, height, radius);
        g.DrawPath(pen, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF r, float radius)
    {
        g.DrawRoundedRectangle(pen, r.X, r.Y, r.Width, r.Height, radius);
    }

    private static GraphicsPath CreateRoundedRectanglePath(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
