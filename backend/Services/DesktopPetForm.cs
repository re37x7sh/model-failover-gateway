using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// Windows 原生置顶、透明、贴边自动吸附收缩的经典「Bongo Cat 敲键盘猫」
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

    // 动画帧计数
    private int _tickCount = 0;
    private bool _isBlinking = false;
    private int _blinkTimer = 0;
    private int _celebrateTicks = 0;

    // 气泡对话框
    private string _bubbleText = "今天也要元气满满敲代码哦！🐾";
    private int _bubbleFadeTicks = 120; // 约 4 秒

    // 经典 Bongo 皮肤模式 (classic: 敲键盘猫, cyber: 赛博猫, shiba: 柴犬)
    private string _avatar = "bongo";

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

        // 窗体基础属性
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(180, 180);

        // 高质量透明底色与双缓冲防闪烁
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

        // 初始位置：屏幕右下角 (工作区内)
        var wa = GetWorkingArea();
        Location = new Point(wa.Right - 200, wa.Bottom - 220);

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

    public void ShowBubble(string text, int durationMs = 4000)
    {
        _bubbleText = text;
        _bubbleFadeTicks = durationMs / 33;
        Invalidate();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
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
        if (status.State == "completed" && _celebrateTicks == 0 && status.SessionDurationMs > 0)
        {
            _celebrateTicks = 90; // 庆祝撒花 3 秒
            var durSec = status.SessionDurationMs / 1000.0;
            var durText = durSec >= 60 ? $"{(int)(durSec / 60)}分{(int)(durSec % 60)}秒" : $"{durSec:F1}s";
            var turnInfo = status.TurnCount > 1 ? $"共 {status.TurnCount} 步，" : "";
            ShowBubble($"任务全部搞定！{turnInfo}总耗时 {durText} 🎉", 6000);
        }
        else if (status.State == "tool_use" && _tickCount % 60 == 0 && _bubbleFadeTicks <= 0)
        {
            ShowBubble($"🔍 正在执行工具: 第 {status.TurnCount} 步...", 2000);
        }

        Invalidate();
    }

    private void OnDockCheckTick(object? sender, EventArgs e)
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
        const int PEEK_WIDTH = 28; // 贴边仅露出 28 像素的软萌猫耳
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var status = _alertService.GetCurrentTaskStatus();
        var isThinking = status.State == "thinking";
        var isToolUse = status.State == "tool_use";
        var isCompleted = status.State == "completed" || _celebrateTicks > 0;

        // 1. 绘制顶部气泡对话框
        if (_bubbleFadeTicks > 0 && !string.IsNullOrEmpty(_bubbleText))
        {
            DrawSpeechBubble(g, _bubbleText);
        }

        // 2. 绘制萌宠形象（核心：经典 Bongo Cat 纯净萌系画风）
        var petRect = new Rectangle(30, 48, 120, 100);
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

        // 3. 绘制状态底座徽章（正在思考、执行工具或庆祝）
        if (isThinking || isToolUse)
        {
            var totalElapsedSec = (int)(DateTime.Now - (status.SessionStartTime != DateTime.MinValue ? status.SessionStartTime : status.Timestamp)).TotalSeconds;
            DrawTimerBadge(g, totalElapsedSec, status.TurnCount, isToolUse);
        }
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
            // 工具态：左爪搭桌，右爪举放大镜
            g.FillEllipse(whiteBrush, r.X + 22, deskY + 2, 18, 16);
            g.DrawEllipse(blackPen, r.X + 22, deskY + 2, 18, 16);

            // 举起的右爪
            g.FillEllipse(whiteBrush, r.X + 80, cy + 30, 18, 18);
            g.DrawEllipse(blackPen, r.X + 80, cy + 30, 18, 18);

            // 放大镜图标
            using var glassPen = new Pen(Color.FromArgb(99, 102, 241), 2.6f);
            g.DrawEllipse(glassPen, r.X + 90, cy + 18, 14, 14);
            g.DrawLine(glassPen, r.X + 100, cy + 30, r.X + 108, cy + 38);
        }
        else if (isThinking)
        {
            // ⚡ 招牌 Bongo 疯狂交替拍打！
            // 奇数帧左手拍下/右手抬起，偶数帧右手拍下/左手抬起
            var isLeftDown = (_tickCount % 6) < 3;
            var leftPawY = isLeftDown ? deskY + 4 : deskY - 10;
            var rightPawY = isLeftDown ? deskY - 10 : deskY + 4;

            // 左爪
            g.FillEllipse(whiteBrush, r.X + 26, leftPawY, 20, 18);
            g.DrawEllipse(blackPen, r.X + 26, leftPawY, 20, 18);

            // 右爪
            g.FillEllipse(whiteBrush, r.X + 74, rightPawY, 20, 18);
            g.DrawEllipse(blackPen, r.X + 74, rightPawY, 20, 18);

            // 拍打敲击小火花 ✨
            if (isLeftDown)
            {
                g.FillEllipse(Brushes.Gold, r.X + 24, deskY + 2, 6, 6);
            }
            else
            {
                g.FillEllipse(Brushes.Gold, r.X + 90, deskY + 2, 6, 6);
            }
        }
        else
        {
            // 空闲状态：双爪乖乖搭在小桌前
            g.FillEllipse(whiteBrush, r.X + 26, deskY + 2, 20, 16);
            g.DrawEllipse(blackPen, r.X + 26, deskY + 2, 20, 16);

            g.FillEllipse(whiteBrush, r.X + 74, deskY + 2, 20, 16);
            g.DrawEllipse(blackPen, r.X + 74, deskY + 2, 20, 16);
        }
    }

    private void DrawCyberCat(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.45)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        using var bodyBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        using var neonPen = new Pen(Color.FromArgb(56, 189, 248), 2.5f);
        g.FillRoundedRectangle(bodyBrush, r.X + 20, cy + 12, 80, 65, 12);
        g.DrawRoundedRectangle(neonPen, r.X + 20, cy + 12, 80, 65, 12);

        // 护目镜
        using var visorBrush = new SolidBrush(isCompleted ? Color.FromArgb(74, 222, 128) : Color.FromArgb(56, 189, 248));
        g.FillRoundedRectangle(visorBrush, r.X + 30, cy + 28, 60, 20, 6);

        // 耳朵
        var earLeft = new PointF[] { new(r.X + 25, cy + 14), new(r.X + 16, cy - 6), new(r.X + 45, cy + 12) };
        var earRight = new PointF[] { new(r.X + 75, cy + 12), new(r.X + 104, cy - 6), new(r.X + 95, cy + 14) };
        g.FillPolygon(bodyBrush, earLeft);
        g.FillPolygon(bodyBrush, earRight);
        g.DrawPolygon(neonPen, earLeft);
        g.DrawPolygon(neonPen, earRight);
    }

    private void DrawDog(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.45)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        using var shibaBrush = new SolidBrush(Color.FromArgb(245, 158, 11));
        using var whiteBrush = new SolidBrush(Color.FromArgb(254, 243, 199));
        using var outlinePen = new Pen(Color.FromArgb(120, 53, 15), 2.5f);

        g.FillEllipse(shibaBrush, r.X + 18, cy + 12, 84, 70);
        g.FillEllipse(whiteBrush, r.X + 26, cy + 34, 68, 42);
        g.DrawArc(outlinePen, r.X + 18, cy + 12, 84, 70, 20, 320);

        // 耳朵
        var earLeft = new PointF[] { new(r.X + 22, cy + 20), new(r.X + 12, cy - 6), new(r.X + 42, cy + 12) };
        var earRight = new PointF[] { new(r.X + 78, cy + 12), new(r.X + 108, cy - 6), new(r.X + 98, cy + 20) };
        g.FillPolygon(shibaBrush, earLeft);
        g.FillPolygon(shibaBrush, earRight);
        g.DrawPolygon(outlinePen, earLeft);
        g.DrawPolygon(outlinePen, earRight);

        // 眼睛鼻子
        g.FillEllipse(Brushes.Black, r.X + 38, cy + 32, 9, 10);
        g.FillEllipse(Brushes.Black, r.X + 73, cy + 32, 9, 10);
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

    private void DrawSpeechBubble(Graphics g, string text)
    {
        using var font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var bubbleW = Math.Min(Math.Max(size.Width + 18, 90), Width - 10);
        var bubbleH = size.Height + 10;
        var bx = (Width - bubbleW) / 2;
        var by = 4;

        using var bgBrush = new SolidBrush(Color.FromArgb(245, 15, 23, 42)); // 高透深黑底
        using var borderPen = new Pen(Color.FromArgb(99, 102, 241), 1.8f);

        var rect = new RectangleF(bx, by, bubbleW, bubbleH);
        g.FillRoundedRectangle(bgBrush, rect, 8);
        g.DrawRoundedRectangle(borderPen, rect, 8);

        // 气泡小箭头
        var arrowPoints = new PointF[]
        {
            new(Width / 2 - 5, by + bubbleH),
            new(Width / 2 + 5, by + bubbleH),
            new(Width / 2, by + bubbleH + 6)
        };
        g.FillPolygon(bgBrush, arrowPoints);

        g.DrawString(text, font, Brushes.White, bx + 9, by + 5);
    }

    private void DrawTimerBadge(Graphics g, int totalSeconds, int turnCount, bool isToolUse)
    {
        var timeText = totalSeconds >= 60 
            ? $"{(totalSeconds / 60):D2}:{(totalSeconds % 60):D2}" 
            : $"{totalSeconds}s";

        var turnText = turnCount > 1 ? $" (第 {turnCount} 步)" : "";
        var icon = isToolUse ? "🔍" : "⚡";
        var text = $"{icon} {timeText}{turnText}";

        using var font = new Font("Consolas", 8.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var badgeW = size.Width + 14;
        var badgeH = 18;
        var bx = (Width - badgeW) / 2;
        var by = Height - 22;

        var color = isToolUse ? Color.FromArgb(234, 88, 12) : Color.FromArgb(239, 68, 68);
        using var bgBrush = new SolidBrush(color);
        using var borderPen = new Pen(Color.FromArgb(254, 240, 138), 1.2f);

        var rect = new RectangleF(bx, by, badgeW, badgeH);
        g.FillRoundedRectangle(bgBrush, rect, 5);
        g.DrawRoundedRectangle(borderPen, rect, 5);
        g.DrawString(text, font, Brushes.White, bx + 7, by + 2);
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
