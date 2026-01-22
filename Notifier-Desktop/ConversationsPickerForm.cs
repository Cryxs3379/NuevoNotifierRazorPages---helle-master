using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NotifierDesktop.Controllers;
using NotifierDesktop.Controls;
using NotifierDesktop.UI;

namespace NotifierDesktop;

public partial class ConversationsPickerForm : Form
{
    private readonly ConversationsController _controller;
    private readonly TextBox _txtSearch;
    private readonly FlowLayoutPanel _flowReceived;
    private readonly Dictionary<string, ConversationRowControl> _conversationControls = new();
    private string? _selectedPhone;

    public event Action<string>? ConversationPicked;

    public ConversationsPickerForm(ConversationsController controller, string? selectedPhone)
    {
        _controller = controller;
        _selectedPhone = selectedPhone;

        Text = "Conversaciones";
        Size = new Size(520, 760);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Background;
        KeyPreview = true;

        Theme.EnableDoubleBuffer(this);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing16, Theme.Spacing12, Theme.Spacing16, Theme.Spacing12)
        };
        Theme.EnableDoubleBuffer(headerPanel);

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

        var lblTitle = new Label
        {
            Text = "Conversaciones",
            Font = Theme.Title,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0)
        };

        var lblHint = new Label
        {
            Text = "ESC para cerrar",
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0)
        };

        headerLayout.Controls.Add(lblTitle, 0, 0);
        headerLayout.Controls.Add(lblHint, 1, 0);
        headerPanel.Controls.Add(headerLayout);

        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Theme.Background,
            Padding = new Padding(Theme.Spacing16, Theme.Spacing8, Theme.Spacing16, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(searchPanel);

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 38,
            Text = "Buscar por teléfono...",
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Padding = new Padding(Theme.Spacing8)
        };
        _txtSearch.GotFocus += (s, e) =>
        {
            if (_txtSearch.Text == "Buscar por teléfono...")
            {
                _txtSearch.Clear();
                _txtSearch.ForeColor = Theme.TextPrimary;
            }
        };
        _txtSearch.LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(_txtSearch.Text))
            {
                _txtSearch.Text = "Buscar por teléfono...";
                _txtSearch.ForeColor = Theme.TextSecondary;
            }
        };
        _txtSearch.TextChanged += async (s, e) => await SearchConversationsAsync();
        searchPanel.Controls.Add(_txtSearch);

        _flowReceived = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background
        };
        Theme.EnableDoubleBuffer(_flowReceived);
        _flowReceived.SizeChanged += (s, e) => UpdateRowWidths();

        Controls.Add(_flowReceived);
        Controls.Add(searchPanel);
        Controls.Add(headerPanel);

        Shown += (s, e) =>
        {
            RefreshConversationsList();
            _txtSearch.Focus();
            _txtSearch.SelectAll();
        };

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && ActiveControl != _txtSearch)
            {
                if (!string.IsNullOrWhiteSpace(_selectedPhone))
                {
                    PickConversation(_selectedPhone);
                    e.Handled = true;
                }
            }
        };
    }

    public void RefreshConversationsList()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(RefreshConversationsList));
            return;
        }

        _flowReceived.SuspendLayout();

        var currentPhones = new HashSet<string>(_controller.Conversations.Select(c => c.Phone));

        foreach (var kvp in _conversationControls.ToList())
        {
            if (!currentPhones.Contains(kvp.Key))
            {
                _flowReceived.Controls.Remove(kvp.Value);
                _conversationControls.Remove(kvp.Key);
            }
        }

        var scrollbarWidth = _flowReceived.Controls.Count > 0 && _flowReceived.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        var targetWidth = Math.Max(200, _flowReceived.ClientSize.Width - scrollbarWidth - 16);

        foreach (var conv in _controller.Conversations)
        {
            if (_conversationControls.TryGetValue(conv.Phone, out var existingControl))
            {
                existingControl.Conversation = conv;
                existingControl.IsSelected = _selectedPhone == conv.Phone;
                existingControl.Width = targetWidth;
            }
            else
            {
                var rowControl = new ConversationRowControl
                {
                    Conversation = conv,
                    Width = targetWidth,
                    Margin = new Padding(0, 0, 0, Theme.Spacing4)
                };
                rowControl.IsSelected = _selectedPhone == conv.Phone;
                rowControl.ConversationSelected += (s, e) => SetSelectedPhone(conv.Phone);
                AttachDoubleClick(rowControl, () => PickConversation(conv.Phone));

                _conversationControls[conv.Phone] = rowControl;
                _flowReceived.Controls.Add(rowControl);
            }
        }

        var conversationOrder = _controller.Conversations
            .Select((c, idx) => new { Phone = c.Phone, Index = idx })
            .ToDictionary(x => x.Phone, x => x.Index);

        var controlsList = _flowReceived.Controls.Cast<ConversationRowControl>().ToList();
        controlsList.Sort((a, b) =>
        {
            var idxA = conversationOrder.GetValueOrDefault(a.Conversation?.Phone ?? "", int.MaxValue);
            var idxB = conversationOrder.GetValueOrDefault(b.Conversation?.Phone ?? "", int.MaxValue);
            return idxA.CompareTo(idxB);
        });

        for (int i = 0; i < controlsList.Count; i++)
        {
            if (_flowReceived.Controls[i] != controlsList[i])
            {
                _flowReceived.Controls.SetChildIndex(controlsList[i], i);
            }
        }

        _flowReceived.ResumeLayout(true);
    }

    private void UpdateRowWidths()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(UpdateRowWidths));
            return;
        }

        if (_flowReceived.Controls.Count == 0) return;

        var scrollbarWidth = _flowReceived.VerticalScroll.Visible
            ? SystemInformation.VerticalScrollBarWidth
            : 0;
        var targetWidth = Math.Max(200, _flowReceived.ClientSize.Width - scrollbarWidth - 16);

        foreach (Control ctrl in _flowReceived.Controls)
        {
            if (ctrl is ConversationRowControl row)
            {
                row.Width = targetWidth;
            }
        }
    }

    private void SetSelectedPhone(string phone)
    {
        _selectedPhone = phone;

        foreach (var kvp in _conversationControls)
        {
            kvp.Value.IsSelected = kvp.Key == phone;
        }
    }

    private async Task SearchConversationsAsync()
    {
        var query = _txtSearch.Text.Trim();
        if (query == "Buscar por teléfono...") query = string.Empty;

        await _controller.LoadConversationsAsync(query);
        _controller.RefreshList();
        RefreshConversationsList();
    }

    private void PickConversation(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        ConversationPicked?.Invoke(phone);
        Close();
    }

    private static void AttachDoubleClick(Control control, Action onDoubleClick)
    {
        control.DoubleClick += (s, e) => onDoubleClick();
        foreach (Control child in control.Controls)
        {
            child.DoubleClick += (s, e) => onDoubleClick();
        }
    }
}
