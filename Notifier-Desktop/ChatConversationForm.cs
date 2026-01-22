using System;
using System.Linq;
using System.Threading.Tasks;
using NotifierDesktop.Controls;
using NotifierDesktop.Controllers;
using NotifierDesktop.Helpers;
using NotifierDesktop.Models;
using NotifierDesktop.Services;
using NotifierDesktop.UI;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop;

public partial class ChatConversationForm : Form
{
    private readonly ApiClient _apiClient;
    private readonly ChatController _chatController;
    private readonly ConversationsController _conversationsController;
    private readonly AppSettings _settings;

    private readonly Label _lblChatPhone;
    private readonly Label _lblAssignedTo;
    private readonly Label _lblLoading;
    private readonly FlowLayoutPanel _flowChat;
    private readonly TextBox _txtMessage;
    private readonly Button _btnSend;
    private readonly Label _lblEmpty;

    private string? _currentPhoneOriginal;
    private string? _currentPhoneNormalized;
    private bool _isWindowFocused = true;

    public string? CurrentPhoneNormalized => _currentPhoneNormalized;

    public ChatConversationForm(
        ApiClient apiClient,
        ChatController chatController,
        ConversationsController conversationsController,
        AppSettings settings,
        string phone)
    {
        _apiClient = apiClient;
        _chatController = chatController;
        _conversationsController = conversationsController;
        _settings = settings;
        _currentPhoneOriginal = phone;

        Text = "Chat";
        Size = new Size(900, 720);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Theme.Background;
        KeyPreview = true;

        Theme.EnableDoubleBuffer(this);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
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

        var headerLeft = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        Theme.EnableDoubleBuffer(headerLeft);

        _lblChatPhone = new Label
        {
            Text = "Conversación con:",
            Font = Theme.Title,
            Location = new Point(0, 0),
            AutoSize = true,
            ForeColor = Theme.TextPrimary
        };

        _lblAssignedTo = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.Success,
            Location = new Point(0, 30),
            AutoSize = true,
            Visible = false
        };

        headerLeft.Controls.Add(_lblChatPhone);
        headerLeft.Controls.Add(_lblAssignedTo);

        _lblLoading = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = false
        };

        headerLayout.Controls.Add(headerLeft, 0, 0);
        headerLayout.Controls.Add(_lblLoading, 1, 0);
        headerPanel.Controls.Add(headerLayout);

        _flowChat = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background,
            Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(_flowChat);
        _flowChat.SizeChanged += (s, e) => AdjustBubbleWidths();

        _lblEmpty = new Label
        {
            Text = "Sin mensajes",
            Font = Theme.Body,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(Theme.Spacing12, Theme.Spacing12, Theme.Spacing12, Theme.Spacing12)
        };

        var composerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(composerPanel);

        _txtMessage = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,
            Margin = new Padding(0, 0, Theme.Spacing8, 0),
            Padding = new Padding(Theme.Spacing8)
        };
        _txtMessage.KeyDown += TxtMessage_KeyDown;
        _txtMessage.TextChanged += (s, e) => UpdateSendState();

        _btnSend = new Button
        {
            Text = "Enviar",
            Dock = DockStyle.Right,
            Width = 110,
            Height = 56,
            Enabled = false,
            BackColor = Theme.TextTertiary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.BodyBold,
            Cursor = Cursors.Hand
        };
        _btnSend.FlatAppearance.BorderSize = 0;
        _btnSend.FlatAppearance.MouseOverBackColor = Theme.AccentBlueHover;
        _btnSend.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 82, 215);
        _btnSend.Click += async (s, e) => await BtnSend_Click();
        Theme.EnableDoubleBuffer(_btnSend);

        composerPanel.Controls.Add(_txtMessage);
        composerPanel.Controls.Add(_btnSend);

        Controls.Add(_flowChat);
        Controls.Add(composerPanel);
        Controls.Add(headerPanel);

        Activated += (s, e) => _isWindowFocused = true;
        Deactivate += (s, e) => _isWindowFocused = false;
        Shown += async (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_currentPhoneOriginal))
            {
                await OpenConversationAsync(_currentPhoneOriginal, _isWindowFocused);
            }
            _txtMessage.Focus();
        };
    }

    public async Task OpenConversationAsync(string phone, bool isWindowFocused)
    {
        _isWindowFocused = isWindowFocused;
        _currentPhoneOriginal = phone;

        var phoneNormalized = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrEmpty(phoneNormalized))
        {
            MessageBox.Show($"Número telefónico inválido: {phone}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _currentPhoneNormalized = phoneNormalized;
        UpdateChatHeader(phoneNormalized);
        Text = $"Chat - {phoneNormalized}";

        ShowLoading(true);
        _flowChat.Controls.Clear();
        _flowChat.Controls.Add(_lblEmpty);

        await _chatController.LoadChatAsync(phoneNormalized);
        RefreshChat();

        if (_isWindowFocused)
        {
            await _apiClient.MarkConversationReadAsync(phoneNormalized);
        }

        _txtMessage.Enabled = true;
        UpdateSendState();
        _txtMessage.Focus();
    }

    public void AppendMessageFromSignalR(MessageDto dto, bool inbound)
    {
        if (string.IsNullOrWhiteSpace(_currentPhoneNormalized)) return;

        if (_chatController.Messages.Any(m => m.Id == dto.Id)) return;

        var msgVm = new MessageVm
        {
            Id = dto.Id,
            Direction = inbound ? MessageDirection.Inbound : MessageDirection.Outbound,
            At = dto.MessageAt,
            Text = dto.Body,
            From = dto.Originator,
            To = dto.Recipient,
            SentBy = dto.SentBy
        };
        _chatController.AddMessage(msgVm);
        RefreshChat();

        if (inbound && _isWindowFocused)
        {
            _ = _apiClient.MarkConversationReadAsync(_currentPhoneNormalized);
        }
    }

    private void RefreshChat()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(RefreshChat));
            return;
        }

        _flowChat.SuspendLayout();
        _flowChat.Controls.Clear();

        if (_chatController.Messages.Count == 0)
        {
            _flowChat.Controls.Add(_lblEmpty);
            ShowLoading(false);
            _flowChat.ResumeLayout(true);
            return;
        }

        foreach (var msg in _chatController.Messages)
        {
            var bubble = new MessageBubbleControl { Message = msg };
            _flowChat.Controls.Add(bubble);
        }

        AdjustBubbleWidths();
        ShowLoading(false);

        if (_flowChat.Controls.Count > 0)
        {
            _flowChat.ScrollControlIntoView(_flowChat.Controls[_flowChat.Controls.Count - 1]);
        }

        _flowChat.ResumeLayout(true);
    }

    private void AdjustBubbleWidths()
    {
        if (_flowChat.Controls.Count == 0) return;
        var maxWidth = _flowChat.Width - 30;
        if (maxWidth <= 0) return;

        foreach (Control ctrl in _flowChat.Controls)
        {
            if (ctrl is MessageBubbleControl bubble && bubble.Width > maxWidth)
            {
                bubble.Width = maxWidth;
            }
        }
    }

    private void UpdateChatHeader(string phone)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(UpdateChatHeader), phone);
            return;
        }

        _lblChatPhone.Text = $"Conversación con: {phone}";

        var conv = _conversationsController.GetSelected(phone);
        if (conv != null && conv.AssignedTo != null &&
            conv.AssignedUntil.HasValue && conv.AssignedUntil > DateTime.UtcNow)
        {
            _lblAssignedTo.Text = $"Atendiendo: {conv.AssignedTo}";
            _lblAssignedTo.Visible = true;
        }
        else
        {
            _lblAssignedTo.Visible = false;
        }
    }

    private async Task BtnSend_Click()
    {
        if (string.IsNullOrWhiteSpace(_currentPhoneNormalized))
        {
            MessageBox.Show("Seleccione una conversación primero.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var messageText = _txtMessage.Text.Trim();
        if (string.IsNullOrWhiteSpace(messageText))
        {
            MessageBox.Show("Escriba un mensaje.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnSend.Enabled = false;
        _btnSend.Text = "Enviando...";
        _btnSend.BackColor = Theme.TextTertiary;

        try
        {
            var operatorName = !string.IsNullOrWhiteSpace(_settings.OperatorName)
                ? _settings.OperatorName
                : Environment.UserName;

            var response = await _apiClient.SendMessageAsync(_currentPhoneNormalized, messageText, operatorName);
            if (response?.Success == true)
            {
                _txtMessage.Clear();
                try
                {
                    await _apiClient.ClaimConversationAsync(_currentPhoneNormalized, operatorName, 5);
                    UpdateChatHeader(_currentPhoneNormalized);
                }
                catch (Exception)
                {
                    // Ignorar claim fallido para no romper el envío
                }
            }
            else
            {
                MessageBox.Show($"Error al enviar: {response?.Error ?? "Error desconocido"}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al enviar mensaje: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSend.Text = "Enviar";
            UpdateSendState();
        }
    }

    private void TxtMessage_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = BtnSend_Click();
        }
    }

    private void UpdateSendState()
    {
        var hasPhone = !string.IsNullOrWhiteSpace(_currentPhoneNormalized);
        var hasText = !string.IsNullOrWhiteSpace(_txtMessage.Text);
        _btnSend.Enabled = hasPhone && hasText;
        _btnSend.BackColor = _btnSend.Enabled ? Theme.AccentBlue : Theme.TextTertiary;
    }

    private void ShowLoading(bool show)
    {
        _lblLoading.Text = show ? "Cargando..." : "";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
