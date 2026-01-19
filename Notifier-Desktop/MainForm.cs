using NotifierDesktop.Controllers;
using NotifierDesktop.Controls;
using NotifierDesktop.Helpers;
using NotifierDesktop.Models;
using NotifierDesktop.Services;
using NotifierDesktop.UI;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop;

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private ApiClient? _apiClient;
    private SignalRService? _signalRService;
    private ConversationsController? _conversationsController;
    private ChatController? _chatController;

    // UI Controls
    private TextBox _txtSearch;
    private FlowLayoutPanel _flowReceived;
    private SplitContainer _splitContainer;
    private Panel _chatHeader;
    private Label _lblChatPhone;
    private Label _lblAssignedTo;
    private FlowLayoutPanel _flowChat;
    private TextBox _txtMessage;
    private Button _btnSend;
    private Label _lblApiStatus;
    private Label _lblSignalRStatus;
    private Label _lblError;

    private string? _selectedPhone;
    private bool _isWindowFocused = true;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "Notifier Desktop - Recepción SMS";
        Size = new Size(1400, 800);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Theme.Background;
        
        Theme.EnableDoubleBuffer(this);

        // Barra superior con estados
        var statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(statusPanel);

        _lblApiStatus = new Label
        {
            Text = "API: Desconectado",
            ForeColor = Theme.Danger,
            Font = Theme.Small,
            Location = new Point(Theme.Spacing12, 10),
            AutoSize = true
        };

        _lblSignalRStatus = new Label
        {
            Text = "SignalR: Desconectado",
            ForeColor = Theme.Danger,
            Font = Theme.Small,
            Location = new Point(150, 10),
            AutoSize = true
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Theme.Danger,
            Font = Theme.Small,
            Location = new Point(350, 10),
            AutoSize = true
        };

        statusPanel.Controls.AddRange(new Control[] { _lblApiStatus, _lblSignalRStatus, _lblError });

        // Panel izquierdo: Lista de conversaciones
        var conversationsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.Spacing8) };
        conversationsPanel.BackColor = Theme.Background;
        Theme.EnableDoubleBuffer(conversationsPanel);
        
        _txtSearch = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = "Buscar por teléfono...",
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface
        };
        _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "Buscar por teléfono...") { _txtSearch.Clear(); _txtSearch.ForeColor = Theme.TextPrimary; } };
        _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text = "Buscar por teléfono..."; _txtSearch.ForeColor = Theme.TextSecondary; } };
        _txtSearch.ForeColor = Theme.TextSecondary;
        _txtSearch.TextChanged += async (s, e) => await SearchConversationsAsync();
        
        _flowReceived = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background
        };
        Theme.EnableDoubleBuffer(_flowReceived);
        conversationsPanel.Controls.Add(_flowReceived);
        conversationsPanel.Controls.Add(_txtSearch);

        // SplitContainer principal (fixed width, no resizable)
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = true,
            SplitterWidth = 1,
            Panel1MinSize = 400
        };
        
        // Set splitter distance and panel2 minsize after form has size
        Load += (s, e) =>
        {
            _splitContainer.Panel2MinSize = 600;
            _splitContainer.SplitterDistance = 400;
        };
        
        _splitContainer.SplitterMoved += (s, e) =>
        {
            // Prevent splitter movement by resetting distance
            if (_splitContainer.SplitterDistance != 400)
            {
                _splitContainer.SplitterDistance = 400;
            }
        };

        // Panel izquierdo: Lista de conversaciones
        _splitContainer.Panel1.Controls.Add(conversationsPanel);

        // Panel derecho: Chat
        var chatPanel = new Panel { Dock = DockStyle.Fill };

        // Header del chat
        _chatHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing16, Theme.Spacing12, Theme.Spacing16, Theme.Spacing12)
        };
        Theme.EnableDoubleBuffer(_chatHeader);

        _lblChatPhone = new Label
        {
            Text = "Seleccione una conversación",
            Font = Theme.Title,
            Location = new Point(Theme.Spacing16, Theme.Spacing12),
            AutoSize = true,
            ForeColor = Theme.TextPrimary
        };

        _lblAssignedTo = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.Success,
            Location = new Point(Theme.Spacing16, 36),
            AutoSize = true
        };

        _chatHeader.Controls.Add(_lblChatPhone);
        _chatHeader.Controls.Add(_lblAssignedTo);

        // Panel de chat con scroll
        _flowChat = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Background,
            Padding = new Padding(Theme.Spacing12)
        };
        Theme.EnableDoubleBuffer(_flowChat);

        // Panel composer
        var composerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing12)
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
            Margin = new Padding(0, 0, Theme.Spacing8, 0)
        };
        _txtMessage.KeyDown += TxtMessage_KeyDown;

        _btnSend = new Button
        {
            Text = "Enviar",
            Dock = DockStyle.Right,
            Width = 110,
            Height = 56,
            Enabled = false,
            BackColor = Theme.AccentBlue,
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

        chatPanel.Controls.Add(_flowChat);
        chatPanel.Controls.Add(composerPanel);
        chatPanel.Controls.Add(_chatHeader);

        _splitContainer.Panel2.Controls.Add(chatPanel);

        Controls.Add(_splitContainer);
        Controls.Add(statusPanel);

        Activated += (s, e) => _isWindowFocused = true;
        Deactivate += (s, e) => _isWindowFocused = false;
    }

    private void LoadSettings()
    {
        _apiClient = new ApiClient(_settings.ApiBaseUrl);
        _signalRService = new SignalRService(_settings.ApiBaseUrl);
        _conversationsController = new ConversationsController(_apiClient, _signalRService);
        _chatController = new ChatController(_apiClient);

        // Configurar eventos SignalR
        if (_signalRService != null)
        {
            _signalRService.OnNewMessage += SignalRService_OnNewMessage;
            _signalRService.OnNewSentMessage += SignalRService_OnNewSentMessage;
            _signalRService.OnDbError += SignalRService_OnDbError;
            _signalRService.OnEsendexDeleteError += SignalRService_OnEsendexDeleteError;
            _signalRService.OnConnected += SignalRService_OnConnected;
            _signalRService.OnDisconnected += SignalRService_OnDisconnected;
            _signalRService.OnReconnecting += SignalRService_OnReconnecting;
        }

        Load += (s, e) =>
        {
            // Set splitter distance after form has size
            _splitContainer.Panel2MinSize = 600;
            _splitContainer.SplitterDistance = 400;
            InitializeAsync().GetAwaiter();
        };
    }

    private async Task InitializeAsync()
    {
        await CheckApiConnectionAsync();
        await LoadConversationsAsync();
        await ConnectSignalRAsync();
    }

    private async Task CheckApiConnectionAsync()
    {
        if (_apiClient == null) return;

        try
        {
            var health = await _apiClient.GetHealthAsync();
            UpdateApiStatus(health != null);
        }
        catch
        {
            UpdateApiStatus(false);
        }
    }

    private async Task LoadConversationsAsync()
    {
        if (_conversationsController == null) return;

        try
        {
            await _conversationsController.LoadConversationsAsync();
            _conversationsController.RefreshList();
            RefreshConversationsList();
        }
        catch (Exception ex)
        {
            ShowError($"Error al cargar conversaciones: {ex.Message}");
        }
    }

    private void RefreshConversationsList()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(RefreshConversationsList));
            return;
        }

        if (_conversationsController == null) return;

        // Suspend layout for performance
        _flowReceived.SuspendLayout();

        // Limpiar lista
        _flowReceived.Controls.Clear();

        foreach (var conv in _conversationsController.Conversations)
        {
            var rowControl = new ConversationRowControl
            {
                Conversation = conv,
                Width = Math.Max(200, _flowReceived.Width - 25),
                Margin = new Padding(0, 0, 0, Theme.Spacing4)
            };
            rowControl.IsSelected = _selectedPhone == conv.Phone;
            rowControl.ConversationSelected += async (s, e) => await ConversationRowControl_ConversationSelected(conv.Phone);

            _flowReceived.Controls.Add(rowControl);
        }
        
        // Resume layout
        _flowReceived.ResumeLayout(true);
    }

    private async Task ConversationRowControl_ConversationSelected(string phone)
    {
        System.Diagnostics.Debug.WriteLine($"[MainForm] ConversationRowControl_ConversationSelected called with phone: '{phone}'");
        
        if (_selectedPhone == phone) return;

        // Update selection state
        var previousPhone = _selectedPhone;
        _selectedPhone = phone;
        
        // Update row selection states
        UpdateRowSelection(previousPhone, phone);

        // Normalizar phone para API (pero mantener original para display)
        var phoneNormalized = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrEmpty(phoneNormalized))
        {
            System.Diagnostics.Debug.WriteLine($"[MainForm] ERROR: Could not normalize phone '{phone}'");
            ShowError($"Número telefónico inválido: {phone}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[MainForm] Phone normalized: '{phone}' -> '{phoneNormalized}'");

        // Claim conversación (usar phone normalizado)
        if (_apiClient != null && !string.IsNullOrWhiteSpace(_settings.OperatorName))
        {
            await _apiClient.ClaimConversationAsync(phoneNormalized, _settings.OperatorName, 5);
        }

        // Cargar chat (usar phone normalizado para API, pero el endpoint ya es tolerante)
        if (_chatController != null)
        {
            System.Diagnostics.Debug.WriteLine($"[MainForm] Calling LoadChatAsync with normalized phone: '{phoneNormalized}'");
            await _chatController.LoadChatAsync(phoneNormalized);
            System.Diagnostics.Debug.WriteLine($"[MainForm] LoadChatAsync completed. ChatController has {_chatController.Messages.Count} messages");
            RefreshChat();
        }

        // Marcar como leído (usar phone normalizado)
        if (_apiClient != null && _isWindowFocused)
        {
            await _apiClient.MarkConversationReadAsync(phoneNormalized);
        }

        // Actualizar header (mostrar número normalizado)
        UpdateChatHeader(phoneNormalized);
        _btnSend.Enabled = true;
        _btnSend.BackColor = Theme.AccentBlue;
        _txtMessage.Enabled = true;
    }
    
    private void UpdateRowSelection(string? previousPhone, string newPhone)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string?, string>(UpdateRowSelection), previousPhone, newPhone);
            return;
        }
        
        foreach (Control ctrl in _flowReceived.Controls)
        {
            if (ctrl is ConversationRowControl row)
            {
                row.IsSelected = row.Conversation?.Phone == newPhone;
            }
        }
    }

    private void RefreshChat()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(RefreshChat));
            return;
        }

        if (_chatController == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainForm] RefreshChat: _chatController is null");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[MainForm] RefreshChat: _chatController.Messages.Count = {_chatController.Messages.Count}");

        _flowChat.Controls.Clear();

        if (_chatController.Messages.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[MainForm] RefreshChat: No messages to display");
            return; // No hacer scroll si no hay mensajes
        }

        foreach (var msg in _chatController.Messages)
        {
            System.Diagnostics.Debug.WriteLine($"[MainForm] Adding message bubble: Id={msg.Id}, Direction={msg.Direction}, Text='{msg.Text?.Substring(0, Math.Min(30, msg.Text?.Length ?? 0))}...'");
            
            var bubble = new MessageBubbleControl
            {
                Message = msg  // Asignar Message primero (esto llama UpdateUI y calcula tamaño)
            };
            
            // Ajustar ancho máximo después de que UpdateUI haya calculado el tamaño
            var maxWidth = _flowChat.Width - 30;
            if (maxWidth > 0 && bubble.Width > maxWidth)
            {
                bubble.Width = maxWidth;
            }
            
            _flowChat.Controls.Add(bubble);
            
            System.Diagnostics.Debug.WriteLine($"[MainForm] Bubble added: Size={bubble.Size}, Visible={bubble.Visible}, Location={bubble.Location}");
        }

        System.Diagnostics.Debug.WriteLine($"[MainForm] RefreshChat: Added {_flowChat.Controls.Count} bubbles to _flowChat");

        // Auto-scroll al final solo si hay controles
        if (_flowChat.Controls.Count > 0)
        {
            _flowChat.ScrollControlIntoView(_flowChat.Controls[_flowChat.Controls.Count - 1]);
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

        var conv = _conversationsController?.GetSelected(phone);
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
        if (_apiClient == null || string.IsNullOrWhiteSpace(_selectedPhone))
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

        // Normalizar número telefónico antes de enviar
        var phoneNormalized = PhoneNormalizer.Normalize(_selectedPhone);
        if (string.IsNullOrEmpty(phoneNormalized))
        {
            ShowError($"Número telefónico inválido: {_selectedPhone}. No se puede enviar el mensaje.");
            return;
        }

        _btnSend.Enabled = false;
        _btnSend.Text = "Enviando...";
        _btnSend.BackColor = Theme.TextTertiary;

        try
        {
            // Enviar con número normalizado
            var response = await _apiClient.SendMessageAsync(phoneNormalized, messageText);
            if (response?.Success == true)
            {
                _txtMessage.Clear();
                // El mensaje se añadirá automáticamente cuando llegue por SignalR
            }
            else
            {
                ShowError($"Error al enviar: {response?.Error ?? "Error desconocido"}");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error al enviar mensaje: {ex.Message}");
        }
        finally
        {
            _btnSend.Enabled = !string.IsNullOrWhiteSpace(_selectedPhone);
            _btnSend.Text = "Enviar";
            _btnSend.BackColor = _btnSend.Enabled ? Theme.AccentBlue : Theme.TextTertiary;
        }
    }

    private void TxtMessage_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            BtnSend_Click().GetAwaiter().GetResult();
        }
    }

    private async Task SearchConversationsAsync()
    {
        var query = _txtSearch.Text.Trim();
        if (query == "Buscar por teléfono...") query = string.Empty;
        if (_conversationsController != null)
        {
            await _conversationsController.LoadConversationsAsync(query);
            _conversationsController.RefreshList();
            RefreshConversationsList();
        }
    }

    private async Task ConnectSignalRAsync()
    {
        if (_signalRService == null) return;

        try
        {
            await _signalRService.StartAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error al conectar SignalR: {ex.Message}");
        }
    }

    private void SignalRService_OnNewMessage(MessageDto message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<MessageDto>(SignalRService_OnNewMessage), message);
            return;
        }

        var customerPhone = !string.IsNullOrWhiteSpace(message.CustomerPhone) 
            ? message.CustomerPhone 
            : message.Originator; // Fallback para compatibilidad

        // Log de warning si CustomerPhone sigue vacío después del fix
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            System.Diagnostics.Debug.WriteLine($"[MainForm] WARNING: CustomerPhone is empty for NewMessage. " +
                $"Originator='{message.Originator}', Recipient='{message.Recipient}', Direction={message.Direction}");
            return; // No procesar si no hay customerPhone
        }

        // Normalizar customerPhone para comparación (puede venir con/sin '+')
        var customerPhoneNormalized = Helpers.PhoneNormalizer.Normalize(customerPhone);
        if (string.IsNullOrEmpty(customerPhoneNormalized))
            customerPhoneNormalized = customerPhone; // Usar original si no se puede normalizar

        // Actualizar o crear conversación en lista (tiempo real)
        if (_conversationsController != null)
        {
            _conversationsController.UpsertFromSignalR(message, isInbound: true);
            RefreshConversationsList();
        }

        // Si esta conversación está abierta, añadir al chat
        // Comparar con _selectedPhone normalizado también
        var selectedPhoneNormalized = !string.IsNullOrWhiteSpace(_selectedPhone)
            ? Helpers.PhoneNormalizer.Normalize(_selectedPhone)
            : _selectedPhone;
        if (string.IsNullOrEmpty(selectedPhoneNormalized))
            selectedPhoneNormalized = _selectedPhone;

        if (selectedPhoneNormalized == customerPhoneNormalized && _chatController != null)
        {
            var msgVm = new MessageVm
            {
                Id = message.Id,
                Direction = MessageDirection.Inbound,
                At = message.MessageAt,
                Text = message.Body,
                From = message.Originator,
                To = message.Recipient
            };
            _chatController.AddMessage(msgVm);
            RefreshChat();

            // Marcar como leído solo si la ventana está enfocada
            if (_isWindowFocused && _apiClient != null)
            {
                _apiClient.MarkConversationReadAsync(customerPhoneNormalized).GetAwaiter();
            }
        }
    }

    private void SignalRService_OnNewSentMessage(MessageDto message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<MessageDto>(SignalRService_OnNewSentMessage), message);
            return;
        }

        var customerPhone = !string.IsNullOrWhiteSpace(message.CustomerPhone) 
            ? message.CustomerPhone 
            : message.Recipient; // Fallback para compatibilidad

        // Log de warning si CustomerPhone sigue vacío después del fix
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            System.Diagnostics.Debug.WriteLine($"[MainForm] WARNING: CustomerPhone is empty for NewSentMessage. " +
                $"Originator='{message.Originator}', Recipient='{message.Recipient}', Direction={message.Direction}");
            return; // No procesar si no hay customerPhone
        }

        // Normalizar customerPhone para comparación (puede venir con/sin '+')
        var customerPhoneNormalized = Helpers.PhoneNormalizer.Normalize(customerPhone);
        if (string.IsNullOrEmpty(customerPhoneNormalized))
            customerPhoneNormalized = customerPhone; // Usar original si no se puede normalizar

        // Actualizar o crear conversación en lista (tiempo real)
        if (_conversationsController != null)
        {
            _conversationsController.UpsertFromSignalR(message, isInbound: false);
            RefreshConversationsList();
        }

        // Si esta conversación está abierta, añadir al chat
        // Comparar con _selectedPhone normalizado también
        var selectedPhoneNormalized = !string.IsNullOrWhiteSpace(_selectedPhone)
            ? Helpers.PhoneNormalizer.Normalize(_selectedPhone)
            : _selectedPhone;
        if (string.IsNullOrEmpty(selectedPhoneNormalized))
            selectedPhoneNormalized = _selectedPhone;

        if (selectedPhoneNormalized == customerPhoneNormalized && _chatController != null)
        {
            var msgVm = new MessageVm
            {
                Id = message.Id,
                Direction = MessageDirection.Outbound,
                At = message.MessageAt,
                Text = message.Body,
                From = message.Originator,
                To = message.Recipient
            };
            _chatController.AddMessage(msgVm);
            RefreshChat();
        }
    }

    private void SignalRService_OnDbError(string error)
    {
        ShowError($"Error de BD: {error}");
    }

    private void SignalRService_OnEsendexDeleteError(string error)
    {
        ShowError($"Error Esendex: {error}");
    }

    private void SignalRService_OnConnected()
    {
        UpdateSignalRStatus(true);
    }

    private void SignalRService_OnDisconnected()
    {
        UpdateSignalRStatus(false);
    }

    private void SignalRService_OnReconnecting()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => _lblSignalRStatus.Text = "SignalR: Reconectando..."));
            Invoke(new Action(() => _lblSignalRStatus.ForeColor = Color.Orange));
        }
        else
        {
            _lblSignalRStatus.Text = "SignalR: Reconectando...";
            _lblSignalRStatus.ForeColor = Color.Orange;
        }
    }

    private void UpdateApiStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(UpdateApiStatus), connected);
            return;
        }

        _lblApiStatus.Text = connected ? "API: Conectado" : "API: Desconectado";
        _lblApiStatus.ForeColor = connected ? Theme.Success : Theme.Danger;
    }

    private void UpdateSignalRStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(UpdateSignalRStatus), connected);
            return;
        }

        _lblSignalRStatus.Text = connected ? "SignalR: Conectado" : "SignalR: Desconectado";
        _lblSignalRStatus.ForeColor = connected ? Theme.Success : Theme.Danger;
    }

    private void ShowError(string error)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(ShowError), error);
            return;
        }

        _lblError.Text = error;
        Task.Delay(5000).ContinueWith(_ =>
        {
            if (InvokeRequired)
                Invoke(new Action(() => _lblError.Text = ""));
            else
                _lblError.Text = "";
        });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _signalRService?.Dispose();
        _apiClient?.Dispose();
        base.OnFormClosing(e);
    }
}
