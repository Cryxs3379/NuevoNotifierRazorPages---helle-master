using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NotifierDesktop.UI;

public class ToastNotificationForm : Form
{
    private readonly System.Windows.Forms.Timer _fadeInTimer;
    private readonly System.Windows.Forms.Timer _fadeOutTimer;
    private readonly System.Windows.Forms.Timer _closeTimer;
    private double _opacityStep = 0.05;
    private const int FadeInDuration = 200; // ms
    private const int DisplayDuration = 4500; // ms
    private const int FadeOutDuration = 200; // ms

    public ToastNotificationForm(string title, string body, int width = 360)
    {
        // Configuración básica del formulario
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(width, 90);
        BackColor = Color.White;
        Opacity = 0;
        
        // Crear región redondeada
        var path = new GraphicsPath();
        int radius = 8;
        path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
        path.AddArc(Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
        path.AddArc(Width - radius * 2, Height - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(0, Height - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseAllFigures();
        Region = new Region(path);

        // Label para título
        var lblTitle = new Label
        {
            Text = title,
            Font = Theme.TitleSmall,
            ForeColor = Theme.TextPrimary,
            Location = new Point(12, 12),
            Size = new Size(width - 24, 20),
            AutoSize = false
        };
        Controls.Add(lblTitle);

        // Label para body
        var lblBody = new Label
        {
            Text = body,
            Font = Theme.Body,
            ForeColor = Theme.TextSecondary,
            Location = new Point(12, 32),
            Size = new Size(width - 24, 50),
            AutoSize = false
        };
        Controls.Add(lblBody);

        // Click para cerrar
        Click += (s, e) => CloseToast();
        lblTitle.Click += (s, e) => CloseToast();
        lblBody.Click += (s, e) => CloseToast();

        // Timer para fade-in
        _fadeInTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _fadeInTimer.Tick += FadeInTimer_Tick;
        
        // Timer para mantener visible
        _closeTimer = new System.Windows.Forms.Timer { Interval = DisplayDuration };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            StartFadeOut();
        };

        // Timer para fade-out
        _fadeOutTimer = new System.Windows.Forms.Timer { Interval = 10 };
        _fadeOutTimer.Tick += FadeOutTimer_Tick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        // Dibujar borde sutil
        using var pen = new Pen(Color.FromArgb(230, 233, 237), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    public void ShowToast()
    {
        Show();
        _fadeInTimer.Start();
        _closeTimer.Start();
    }

    private void FadeInTimer_Tick(object? sender, EventArgs e)
    {
        Opacity += _opacityStep;
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
        Opacity -= _opacityStep;
        if (Opacity <= 0)
        {
            Opacity = 0;
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
}
