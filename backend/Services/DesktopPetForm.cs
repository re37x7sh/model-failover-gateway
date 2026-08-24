using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ModelFailoverGateway.Models;

namespace ModelFailoverGateway.Services;

/// <summary>
/// Windows 原生置顶、透明、贴边自动吸附收缩的灵动桌面宠物窗体
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
    private float _earAngle = 0f;
    private float _pawOffset = 0f;
    private int _celebrateTicks = 0;

    // 气泡对话框
    private string _bubbleText = "今天也要元气满满哦！✨";
    private int _bubbleFadeTicks = 120; // 约 4 秒

    private string _avatar = "cat"; // cat, robot, dog

    private static Rectangle GetWorkingArea()
    {
        return Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
    }

    // Windows 无激活置顶样式支持
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
        Size = new Size(160, 160);

        // 透明粉红底色与双缓冲防闪烁
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

        // 初始位置：屏幕右下角 (工作区内)
        var wa = GetWorkingArea();
        Location = new Point(wa.Right - 180, wa.Bottom - 200);

        // 动画刷新定时器 (30 FPS)
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
        var catItem = new ToolStripMenuItem("🐱 切换为赛博猫咪", null, (s, e) => { _avatar = "cat"; ShowBubble("切换为赛博猫咪啦！🐾"); });
        var robotItem = new ToolStripMenuItem("🤖 切换为灵动机器人", null, (s, e) => { _avatar = "robot"; ShowBubble("机器人模式已启动！⚡"); });
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

        menu.Items.Add(catItem);
        menu.Items.Add(robotItem);
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

        // 呼吸与打字摇摆
        _earAngle = (float)Math.Sin(_tickCount * 0.1) * 3f;
        _pawOffset = (float)Math.Sin(_tickCount * 0.5) * 4f;

        if (_celebrateTicks > 0) _celebrateTicks--;
        if (_bubbleFadeTicks > 0) _bubbleFadeTicks--;

        // 获取实时状态
        var status = _alertService.GetCurrentTaskStatus();
        if (status.State == "completed" && _celebrateTicks == 0 && status.DurationMs > 0)
        {
            _celebrateTicks = 60; // 庆祝撒花 2 秒
            var durSec = (status.DurationMs / 1000.0).ToString("F1");
            ShowBubble($"任务完成啦！耗时 {durSec}s 🎉", 5000);
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
        const int PEEK_WIDTH = 20; // 贴边仅露出 20 像素的小耳朵/标签
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

            // 检测贴边吸附 (靠近边缘 30 像素内自动吸附)
            if (Left <= wa.Left + 30)
            {
                _dockSide = DockSide.Left;
                Location = new Point(wa.Left, Top);
            }
            else if (Right >= wa.Right - 30)
            {
                _dockSide = DockSide.Right;
                Location = new Point(wa.Right - Width, Top);
            }
            else if (Top <= wa.Top + 30)
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
        var isCompleted = status.State == "completed" || _celebrateTicks > 0;

        // 1. 绘制气泡对话框
        if (_bubbleFadeTicks > 0 && !string.IsNullOrEmpty(_bubbleText))
        {
            DrawSpeechBubble(g, _bubbleText);
        }

        // 2. 绘制萌宠形象
        var petRect = new Rectangle(30, 45, 100, 100);
        if (_avatar == "robot")
        {
            DrawRobot(g, petRect, isThinking, isCompleted);
        }
        else if (_avatar == "dog")
        {
            DrawDog(g, petRect, isThinking, isCompleted);
        }
        else
        {
            DrawCat(g, petRect, isThinking, isCompleted);
        }

        // 3. 绘制思考打字计时器底座 (思考时展示)
        if (isThinking)
        {
            DrawTimerBadge(g, (int)(DateTime.Now - status.Timestamp).TotalSeconds);
        }
    }

    private void DrawCat(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.4)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        // 身体
        using var bodyBrush = new SolidBrush(Color.FromArgb(99, 102, 241));
        g.FillEllipse(bodyBrush, r.X + 15, cy + 30, 70, 55);

        // 肚皮
        using var bellyBrush = new SolidBrush(Color.FromArgb(224, 231, 255));
        g.FillEllipse(bellyBrush, r.X + 25, cy + 38, 50, 40);

        // 头部
        using var headBrush = new SolidBrush(Color.FromArgb(79, 70, 229));
        g.FillEllipse(headBrush, r.X + 18, cy + 5, 64, 56);

        // 耳朵
        using var earBrush = new SolidBrush(Color.FromArgb(67, 56, 202));
        using var innerEarBrush = new SolidBrush(Color.FromArgb(244, 63, 94));
        var earLeft = new PointF[] { new(r.X + 22, cy + 18), new(r.X + 12, cy - 8 + _earAngle), new(r.X + 38, cy + 8) };
        var earRight = new PointF[] { new(r.X + 78, cy + 18), new(r.X + 88, cy - 8 - _earAngle), new(r.X + 62, cy + 8) };
        g.FillPolygon(earBrush, earLeft);
        g.FillPolygon(earBrush, earRight);

        // 眼睛
        using var eyeBrush = new SolidBrush(Color.FromArgb(30, 27, 75));
        if (_isBlinking)
        {
            using var eyePen = new Pen(Color.FromArgb(30, 27, 75), 2);
            g.DrawLine(eyePen, r.X + 35, cy + 30, r.X + 45, cy + 30);
            g.DrawLine(eyePen, r.X + 55, cy + 30, r.X + 65, cy + 30);
        }
        else if (isCompleted)
        {
            using var eyePen = new Pen(Color.FromArgb(74, 222, 128), 2.5f);
            g.DrawArc(eyePen, r.X + 34, cy + 24, 12, 10, 200, 140);
            g.DrawArc(eyePen, r.X + 54, cy + 24, 12, 10, 200, 140);
        }
        else
        {
            g.FillEllipse(eyeBrush, r.X + 35, cy + 25, 10, 12);
            g.FillEllipse(eyeBrush, r.X + 55, cy + 25, 10, 12);
            g.FillEllipse(Brushes.White, r.X + 37, cy + 26, 4, 4);
            g.FillEllipse(Brushes.White, r.X + 57, cy + 26, 4, 4);
        }

        // 鼻子与小嘴
        g.FillPolygon(innerEarBrush, new PointF[] { new(r.X + 47, cy + 38), new(r.X + 53, cy + 38), new(r.X + 50, cy + 42) });

        // 爪子与打字动效
        using var pawBrush = new SolidBrush(Color.FromArgb(224, 231, 255));
        if (isThinking)
        {
            // 小键盘
            using var kbBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
            g.FillRectangle(kbBrush, r.X + 25, cy + 68, 50, 14);
            g.DrawRectangle(Pens.Cyan, r.X + 25, cy + 68, 50, 14);

            g.FillEllipse(pawBrush, r.X + 30, cy + 64 + _pawOffset, 12, 12);
            g.FillEllipse(pawBrush, r.X + 58, cy + 64 - _pawOffset, 12, 12);
        }
        else
        {
            g.FillEllipse(pawBrush, r.X + 30, cy + 70, 12, 12);
            g.FillEllipse(pawBrush, r.X + 58, cy + 70, 12, 12);
        }
    }

    private void DrawRobot(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.4)) * 10f : 0f;
        var cy = r.Y - jumpOffset;

        // 身体
        using var bodyBrush = new SolidBrush(Color.FromArgb(51, 65, 85));
        g.FillRectangle(bodyBrush, r.X + 24, cy + 42, 52, 40);

        // 头盔
        using var headBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
        using var borderPen = new Pen(Color.FromArgb(56, 189, 248), 2f);
        g.FillRectangle(headBrush, r.X + 20, cy + 6, 60, 40);
        g.DrawRectangle(borderPen, r.X + 20, cy + 6, 60, 40);

        // 天线
        g.DrawLine(borderPen, r.X + 50, cy + 6, r.X + 50, cy - 8);
        using var lightBrush = new SolidBrush((_tickCount % 10 < 5) ? Color.Red : Color.Cyan);
        g.FillEllipse(lightBrush, r.X + 46, cy - 14, 8, 8);

        // 眼睛
        using var eyeBrush = new SolidBrush(isCompleted ? Color.LightGreen : Color.Cyan);
        g.FillRectangle(eyeBrush, r.X + 32, cy + 18, 12, 12);
        g.FillRectangle(eyeBrush, r.X + 56, cy + 18, 12, 12);
    }

    private void DrawDog(Graphics g, Rectangle r, bool isThinking, bool isCompleted)
    {
        var jumpOffset = isCompleted ? (float)Math.Abs(Math.Sin(_tickCount * 0.4)) * 12f : 0f;
        var cy = r.Y - jumpOffset;

        // 身体与柴犬黄
        using var bodyBrush = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillEllipse(bodyBrush, r.X + 16, cy + 30, 68, 54);

        // 头部
        g.FillEllipse(bodyBrush, r.X + 20, cy + 6, 60, 54);

        // 白色脸颊
        using var whiteBrush = new SolidBrush(Color.FromArgb(254, 243, 199));
        g.FillEllipse(whiteBrush, r.X + 26, cy + 24, 48, 32);

        // 耳朵
        using var earBrush = new SolidBrush(Color.FromArgb(180, 83, 9));
        g.FillPolygon(earBrush, new PointF[] { new(r.X + 24, cy + 18), new(r.X + 16, cy - 6), new(r.X + 38, cy + 10) });
        g.FillPolygon(earBrush, new PointF[] { new(r.X + 76, cy + 18), new(r.X + 84, cy - 6), new(r.X + 62, cy + 10) });

        // 眼睛与鼻子
        g.FillEllipse(Brushes.Black, r.X + 36, cy + 26, 8, 8);
        g.FillEllipse(Brushes.Black, r.X + 56, cy + 26, 8, 8);
        g.FillEllipse(Brushes.Black, r.X + 46, cy + 36, 8, 6);
    }

    private void DrawSpeechBubble(Graphics g, string text)
    {
        using var font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var bubbleW = Math.Max(size.Width + 16, 80);
        var bubbleH = size.Height + 10;
        var bx = (Width - bubbleW) / 2;
        var by = 4;

        using var bgBrush = new SolidBrush(Color.FromArgb(240, 15, 23, 42));
        using var borderPen = new Pen(Color.FromArgb(99, 102, 241), 1.5f);

        // 圆角气泡底框
        var rect = new RectangleF(bx, by, bubbleW, bubbleH);
        g.FillRectangle(bgBrush, rect);
        g.DrawRectangle(borderPen, bx, by, bubbleW, bubbleH);

        // 文本
        g.DrawString(text, font, Brushes.White, bx + 8, by + 5);
    }

    private void DrawTimerBadge(Graphics g, int seconds)
    {
        var text = $"⚡ {seconds}s";
        using var font = new Font("Consolas", 8.5f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        var bx = (Width - size.Width - 14) / 2;
        var by = Height - 22;

        using var bgBrush = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillRectangle(bgBrush, bx, by, size.Width + 14, 16);
        g.DrawRectangle(Pens.Orange, bx, by, size.Width + 14, 16);
        g.DrawString(text, font, Brushes.White, bx + 7, by + 1);
    }
}
