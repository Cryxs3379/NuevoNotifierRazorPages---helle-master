using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using NotifierDesktop.Services;

namespace NotifierDesktop.UI;

public sealed class ToastNotificationForm : Form
{
    private readonly System.Windows.Forms.Timer _fadeInTimer;
    private readonly System.Windows.Forms.Timer _fadeOutTimer;
    private readonly System.Windows.Forms.Timer _closeTimer;
    
    private const int DisplayDurationMs = 6000; // 6 segundos
    private const int CornerRadius = 10;
    private const int AccentBarWidth = 4;
    private const int ActionRowHeight = 32;
    private const int ActionRowPadding = 10;
    
    private readonly Color _accentColor;
    private readonly ToastVisualStyle _style;
    private readonly IReadOnlyList<ToastAction> _actions;

    public ToastNotificationForm(
        string title,
        string body,
        int width = 380,
        int? customHeight = null,
        Color? accentColor = null,
        ToastVisualStyle style = ToastVisualStyle.Neutral,
        IReadOnlyList<ToastAction>? actions = null)
    {
        _style = style;
        _actions = actions ?? Array.Empty<ToastAction>();
        _accentColor = accentColor ?? GetDefaultAccent(style);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        
        // Fondo con color de acento muy sutil (98% blanco, 2% color)
        var r = (int)(255 * 0.98 + _accentColor.R * 0.02);
        var g = (int)(255 * 0.98 + _accentColor.G * 0.02);
        var b = (int)(255 * 0.98 + _accentColor.B * 0.02);
        BackColor = Color.FromArgb(r, g, b);
        
        Opacity = 0;
        Width = width;
        var defaultHeight = _actions.Count > 0 ? 130 : 90;
        Height = customHeight ?? defaultHeight;

        // Habilitar double buffering
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);

        // Crear región redondeada
        ApplyRoundedRegion();

        // Label para título
        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 22, 22),
            Location = new Point(16, 14),
            Size = new Size(width - 32, 22),
            AutoSize = false
        };
        Controls.Add(lblTitle);

        // Label para body
        var bodyHeight = Height - 50 - (_actions.Count > 0 ? (ActionRowHeight + ActionRowPadding) : 0);
        var lblBody = new Label
        {
            Text = body,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(90, 90, 90),
            Location = new Point(16, 38),
            Size = new Size(width - 32, bodyHeight),
            AutoSize = false
        };
        Controls.Add(lblBody);

        if (_actions.Count > 0)
        {
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Height = ActionRowHeight,
                Width = width - 32,
                Location = new Point(16, Height - ActionRowHeight - ActionRowPadding),
                BackColor = Color.Transparent
            };

            foreach (var action in _actions)
            {
                var btn = new Button
                {
                    Text = action.Label,
                    AutoSize = true,
                    Height = ActionRowHeight,
                    FlatStyle = FlatStyle.Standard
                };

                btn.Click += async (_, __) =>
                {
                    try
                    {
                        if (action.OnClickAsync != null)
                        {
                            await action.OnClickAsync();
                        }
                    }
                    catch
                    {
                        // Ignore action errors in toast UI
                    }
                    finally
                    {
                        CloseToast();
                    }
                };

                panel.Controls.Add(btn);
            }

            Controls.Add(panel);
        }

        // Click para cerrar
        Click += (s, e) => CloseToast();
        lblTitle.Click += (s, e) => CloseToast();
        lblBody.Click += (s, e) => CloseToast();

        // Timers
        _fadeInTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _fadeInTimer.Tick += FadeInTimer_Tick;
        
        _closeTimer = new System.Windows.Forms.Timer { Interval = DisplayDurationMs };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            StartFadeOut();
        };

        _fadeOutTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _fadeOutTimer.Tick += FadeOutTimer_Tick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRoundedRegion();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        
        // Dibujar borde lateral izquierdo con color de acento (4px)
        using var accentBrush = new SolidBrush(_accentColor);
        var accentRect = new Rectangle(0, 0, AccentBarWidth, Height);
        g.FillRectangle(accentBrush, accentRect);
        
        // Dibujar borde general sutil
        var borderColor = Color.FromArgb(220, 223, 227);
        using var borderPen = new Pen(borderColor, 1);
        var borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var borderPath = RoundedRect(borderRect, CornerRadius);
        g.DrawPath(borderPen, borderPath);
    }

    public void ShowToast()
    {
        Show();
        _fadeInTimer.Start();
        _closeTimer.Start();
    }

    private void FadeInTimer_Tick(object? sender, EventArgs e)
    {
        Opacity = Math.Min(1.0, Opacity + 0.05);
        if (Opacity >= 1.0)
        {
            Opacity = 1.0;
            _fadeInTimer.Stop();
        }
    }

    private void StartFadeOut()
    {
        _fadeOutTimer.Start();
    }

    private void FadeOutTimer_Tick(object? sender, EventArgs e)
    {
        Opacity = Math.Max(0.0, Opacity - 0.05);
        if (Opacity <= 0.0)
        {
            Opacity = 0.0;
            _fadeOutTimer.Stop();
            CloseToast();
        }
    }

    public void CloseToast()
    {
        _fadeInTimer?.Stop();
        _fadeOutTimer?.Stop();
        _closeTimer?.Stop();
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _fadeInTimer?.Dispose();
        _fadeOutTimer?.Dispose();
        _closeTimer?.Dispose();
        base.OnFormClosed(e);
    }

    private void ApplyRoundedRegion()
    {
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color GetDefaultAccent(ToastVisualStyle style)
    {
        return style switch
        {
            ToastVisualStyle.Success => Color.FromArgb(40, 167, 69),
            ToastVisualStyle.Info    => Color.FromArgb(0, 122, 255),
            ToastVisualStyle.Warning => Color.FromArgb(255, 152, 0),
            ToastVisualStyle.Danger  => Color.FromArgb(220, 53, 69),
            _                        => Color.FromArgb(120, 120, 120),
        };
    }
}

public sealed class ToastAction
{
    public string Label { get; init; } = string.Empty;
    public Func<Task>? OnClickAsync { get; init; }
}
