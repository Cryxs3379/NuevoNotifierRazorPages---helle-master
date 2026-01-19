using NotifierDesktop.UI;
using NotifierDesktop.ViewModels;

using System.Drawing;

namespace NotifierDesktop.Controls;

public partial class ConversationRowControl : UserControl
{
    private ConversationVm? _conversation;
    private readonly Label _lblPhone;
    private readonly Label _lblPreview;
    private readonly Label _lblTime;
    private readonly Panel _badgeUnread;
    private readonly Label _lblBadgePending;
    private readonly Label _lblAssigned;
    private bool _isSelected;
    private bool _isHovered;
    
    // Cached fonts
    private static readonly Font _fontPhoneBold = Theme.BodyBold;
    private static readonly Font _fontPhoneRegular = Theme.Body;
    private static readonly Font _fontPreview = Theme.Small;
    private static readonly Font _fontTime = Theme.Tiny;
    private static readonly Font _fontBadge = Theme.Tiny;

    public ConversationVm? Conversation
    {
        get => _conversation;
        set
        {
            _conversation = value;
            UpdateUI();
        }
    }

    public event EventHandler? ConversationSelected;
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                UpdateBackColor();
            }
        }
    }

    public ConversationRowControl()
    {
        Height = 76;
        Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8);
        BackColor = Theme.Surface;
        BorderStyle = BorderStyle.None;
        Cursor = Cursors.Hand;
        
        Theme.EnableDoubleBuffer(this);

        _lblPhone = new Label
        {
            Text = "",
            Font = _fontPhoneBold,
            Location = new Point(Theme.Spacing12, Theme.Spacing8),
            AutoSize = false,
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ForeColor = Theme.TextPrimary
        };

        _lblPreview = new Label
        {
            Text = "",
            Font = _fontPreview,
            ForeColor = Theme.TextSecondary,
            Location = new Point(Theme.Spacing12, 30),
            Size = new Size(Width - 120, 20),
            AutoEllipsis = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblTime = new Label
        {
            Text = "",
            Font = _fontTime,
            ForeColor = Theme.TextTertiary,
            TextAlign = ContentAlignment.TopRight,
            Location = new Point(Width - 80, Theme.Spacing8),
            Size = new Size(70, 15),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        // Paint border on bottom
        Paint += (s, e) =>
        {
            using var pen = new Pen(Theme.BorderLight, 1);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        };

        _badgeUnread = new Panel
        {
            Size = new Size(10, 10),
            BackColor = Theme.AccentBlue,
            Location = new Point(Width - 28, 38),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };
        Theme.EnableDoubleBuffer(_badgeUnread);

        _lblBadgePending = new Label
        {
            Text = "Pendiente",
            Font = _fontBadge,
            ForeColor = Theme.Surface,
            BackColor = Theme.Warning,
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true,
            Location = new Point(Theme.Spacing12, 52),
            Visible = false
        };
        Theme.EnableDoubleBuffer(_lblBadgePending);

        _lblAssigned = new Label
        {
            Text = "",
            Font = _fontBadge,
            ForeColor = Theme.Surface,
            BackColor = Theme.Success,
            Padding = new Padding(6, 2, 6, 2),
            Location = new Point(Width - 140, 52),
            Size = new Size(120, 15),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoEllipsis = true,
            Visible = false
        };
        Theme.EnableDoubleBuffer(_lblAssigned);

        Controls.AddRange(new Control[] {
            _lblPhone, _lblPreview, _lblTime,
            _badgeUnread, _lblBadgePending, _lblAssigned
        });

        Click += (s, e) => ConversationSelected?.Invoke(this, EventArgs.Empty);
        foreach (Control ctrl in Controls)
        {
            ctrl.Click += (s, e) => ConversationSelected?.Invoke(this, EventArgs.Empty);
            ctrl.MouseEnter += (s, e) => { _isHovered = true; UpdateBackColor(); };
            ctrl.MouseLeave += (s, e) => { _isHovered = false; UpdateBackColor(); };
        }

        MouseEnter += (s, e) => { _isHovered = true; UpdateBackColor(); };
        MouseLeave += (s, e) => { _isHovered = false; UpdateBackColor(); };
    }
    
    private void UpdateBackColor()
    {
        if (_isSelected)
            BackColor = Theme.SurfaceSelected;
        else if (_isHovered)
            BackColor = Theme.SurfaceHover;
        else
            BackColor = Theme.Surface;
    }

    private void UpdateUI()
    {
        if (_conversation == null)
        {
            _lblPhone.Text = "";
            _lblPreview.Text = "";
            _lblTime.Text = "";
            _badgeUnread.Visible = false;
            _lblBadgePending.Visible = false;
            _lblAssigned.Visible = false;
            return;
        }

        _lblPhone.Text = _conversation.Phone ?? "";
        _lblPhone.Font = _conversation.Unread ? _fontPhoneBold : _fontPhoneRegular;
        _lblPhone.ForeColor = Theme.TextPrimary;

        var previewText = _conversation.Preview ?? "";
        _lblPreview.Text = previewText.Length > 50 
            ? previewText.Substring(0, 50) + "..." 
            : previewText;
        _lblPreview.ForeColor = _conversation.Unread ? Theme.TextPrimary : Theme.TextSecondary;

        if (_conversation.LastMessageAt.HasValue)
        {
            var local = _conversation.LastMessageAt.Value.ToLocalTime();
            var diff = DateTime.Now - local;
            if (diff.TotalMinutes < 1)
                _lblTime.Text = "Ahora";
            else if (diff.TotalMinutes < 60)
                _lblTime.Text = $"{(int)diff.TotalMinutes}m";
            else if (diff.TotalHours < 24)
                _lblTime.Text = $"{(int)diff.TotalHours}h";
            else if (diff.TotalDays < 7)
                _lblTime.Text = $"{(int)diff.TotalDays}d";
            else
                _lblTime.Text = local.ToString("dd/MM");
        }
        else
        {
            _lblTime.Text = "";
        }

        _badgeUnread.Visible = _conversation.Unread;
        _lblBadgePending.Visible = _conversation.PendingReply;
        _lblAssigned.Visible = _conversation.AssignedTo != null && 
            _conversation.AssignedUntil.HasValue && 
            _conversation.AssignedUntil > DateTime.UtcNow;
        
        if (_lblAssigned.Visible)
        {
            _lblAssigned.Text = $"Atendiendo: {_conversation.AssignedTo}";
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Width > 0)
        {
            if (_lblPhone != null)
            {
                _lblPhone.Width = Math.Max(100, Width - 100); // Reserva 100px para hora + padding
            }
            if (_lblPreview != null)
            {
                _lblPreview.Width = Width - 120;
            }
            Invalidate(); // Redraw border
        }
    }
}
