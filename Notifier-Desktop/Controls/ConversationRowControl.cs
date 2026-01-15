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

    public ConversationRowControl()
    {
        Height = 70;
        Padding = new Padding(10, 5, 10, 5);
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Cursor = Cursors.Hand;

        _lblPhone = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Location = new Point(10, 8),
            AutoSize = true
        };

        _lblPreview = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 9),
            ForeColor = Color.Gray,
            Location = new Point(10, 28),
            Size = new Size(Width - 120, 20),
            AutoEllipsis = true
        };

        _lblTime = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 8),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.TopRight,
            Location = new Point(Width - 80, 8),
            Size = new Size(70, 15),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _badgeUnread = new Panel
        {
            Size = new Size(20, 20),
            BackColor = Color.FromArgb(0, 122, 255), // Azul WhatsApp
            Location = new Point(Width - 30, 35),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };

        _lblBadgePending = new Label
        {
            Text = "Pendiente",
            Font = new Font(Font.FontFamily, 7, FontStyle.Bold),
            ForeColor = Color.Orange,
            Location = new Point(10, 48),
            AutoSize = true,
            Visible = false
        };

        _lblAssigned = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 7),
            ForeColor = Color.DarkGreen,
            Location = new Point(Width - 100, 48),
            Size = new Size(90, 15),
            TextAlign = ContentAlignment.TopRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Visible = false
        };

        Controls.AddRange(new Control[] {
            _lblPhone, _lblPreview, _lblTime,
            _badgeUnread, _lblBadgePending, _lblAssigned
        });

        Click += (s, e) => ConversationSelected?.Invoke(this, EventArgs.Empty);
        foreach (Control ctrl in Controls)
        {
            ctrl.Click += (s, e) => ConversationSelected?.Invoke(this, EventArgs.Empty);
        }

        MouseEnter += (s, e) => BackColor = Color.FromArgb(245, 245, 245);
        MouseLeave += (s, e) => BackColor = Color.White;
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

        _lblPhone.Text = _conversation.Phone;
        _lblPhone.Font = _conversation.Unread 
            ? new Font(Font.FontFamily, 10, FontStyle.Bold) 
            : new Font(Font.FontFamily, 10, FontStyle.Regular);

        _lblPreview.Text = _conversation.Preview.Length > 50 
            ? _conversation.Preview.Substring(0, 50) + "..." 
            : _conversation.Preview;
        _lblPreview.ForeColor = _conversation.Unread ? Color.Black : Color.Gray;

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
        if (_lblPreview != null)
        {
            _lblPreview.Width = Width - 120;
        }
    }
}
