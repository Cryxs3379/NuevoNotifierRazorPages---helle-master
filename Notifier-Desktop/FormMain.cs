using NotifierDesktop.Models;
using NotifierDesktop.Services;

namespace NotifierDesktop;

public partial class FormMain : Form
{
    private readonly AppSettings _settings;
    private ApiClient? _apiClient;
    private SignalRService? _signalRService;
    private readonly Dictionary<string, ConversationInfo> _conversations = new();
    private string? _selectedPhone;

    // UI Controls
    private SplitContainer _splitContainer;
    private Panel _leftPanel;
    private Panel _rightPanel;
    private TextBox _txtSearch;
    private Button _btnSearch;
    private ListView _lstConversations;
    private Label _lblSelectedPhone;
    private ListBox _lstChat;
    private TextBox _txtMessage;
    private Button _btnSend;
    private Label _lblApiStatus;
    private Label _lblSignalRStatus;
    private Label _lblError;

    public FormMain(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "Notifier Desktop - Recepción SMS";
        Size = new Size(1200, 700);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        // Barra superior con estados
        var statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.LightGray
        };

        _lblApiStatus = new Label
        {
            Text = "API: Desconectado",
            ForeColor = Color.Red,
            Location = new Point(10, 6),
            AutoSize = true
        };

        _lblSignalRStatus = new Label
        {
            Text = "SignalR: Desconectado",
            ForeColor = Color.Red,
            Location = new Point(150, 6),
            AutoSize = true
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Color.Red,
            Location = new Point(350, 6),
            AutoSize = true
        };

        statusPanel.Controls.AddRange(new Control[] { _lblApiStatus, _lblSignalRStatus, _lblError });

        // SplitContainer principal
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 350,
            FixedPanel = FixedPanel.Panel1
        };

        // Panel izquierdo - Lista de conversaciones
        _leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40
        };

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill
        };
        _txtSearch.GotFocus += (s, e) => { if (_txtSearch.Text == "Buscar por teléfono...") _txtSearch.Clear(); };
        _txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) _txtSearch.Text = "Buscar por teléfono..."; };
        _txtSearch.Text = "Buscar por teléfono...";
        _txtSearch.ForeColor = Color.Gray;

        _btnSearch = new Button
        {
            Text = "Buscar",
            Dock = DockStyle.Right,
            Width = 80
        };
        _btnSearch.Click += BtnSearch_Click;

        searchPanel.Controls.Add(_txtSearch);
        searchPanel.Controls.Add(_btnSearch);

        _lstConversations = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };
        _lstConversations.Columns.Add("Teléfono", 150);
        _lstConversations.Columns.Add("Preview", 200);
        _lstConversations.Columns.Add("Hora", 100);
        _lstConversations.SelectedIndexChanged += LstConversations_SelectedIndexChanged;
        _lstConversations.DoubleClick += LstConversations_DoubleClick;

        _leftPanel.Controls.Add(_lstConversations);
        _leftPanel.Controls.Add(searchPanel);

        // Panel derecho - Chat
        _rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        _lblSelectedPhone = new Label
        {
            Text = "Seleccione una conversación",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lstChat = new ListBox
        {
            Dock = DockStyle.Fill,
            FormattingEnabled = true,
            DrawMode = DrawMode.OwnerDrawVariable
        };
        _lstChat.DrawItem += LstChat_DrawItem;
        _lstChat.MeasureItem += LstChat_MeasureItem;

        var sendPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 100
        };

        _txtMessage = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true
        };

        _btnSend = new Button
        {
            Text = "Enviar",
            Dock = DockStyle.Right,
            Width = 100,
            Height = 100
        };
        _btnSend.Click += BtnSend_Click;

        sendPanel.Controls.Add(_txtMessage);
        sendPanel.Controls.Add(_btnSend);

        _rightPanel.Controls.Add(_lblSelectedPhone);
        _rightPanel.Controls.Add(_lstChat);
        _rightPanel.Controls.Add(sendPanel);

        _splitContainer.Panel1.Controls.Add(_leftPanel);
        _splitContainer.Panel2.Controls.Add(_rightPanel);

        Controls.Add(_splitContainer);
        Controls.Add(statusPanel);
    }

    private void LoadSettings()
    {
        _apiClient = new ApiClient(_settings.ApiBaseUrl);
        _signalRService = new SignalRService(_settings.ApiBaseUrl);

        // Configurar eventos SignalR
        _signalRService.OnNewMessage += SignalRService_OnNewMessage;
        _signalRService.OnNewSentMessage += SignalRService_OnNewSentMessage;
        _signalRService.OnDbError += SignalRService_OnDbError;
        _signalRService.OnEsendexDeleteError += SignalRService_OnEsendexDeleteError;
        _signalRService.OnConnected += SignalRService_OnConnected;
        _signalRService.OnDisconnected += SignalRService_OnDisconnected;
        _signalRService.OnReconnecting += SignalRService_OnReconnecting;

        // Conectar SignalR y cargar datos iniciales
        Load += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Probar conexión API
        await CheckApiConnectionAsync();

        // Cargar conversaciones iniciales
        await LoadConversationsAsync();

        // Conectar SignalR
        await ConnectSignalRAsync();
    }

    private async Task CheckApiConnectionAsync()
    {
        if (_apiClient == null) return;

        try
        {
            var health = await _apiClient.GetHealthAsync();
            if (health != null)
            {
                UpdateApiStatus(true);
            }
            else
            {
                UpdateApiStatus(false);
            }
        }
        catch
        {
            UpdateApiStatus(false);
        }
    }

    private async Task LoadConversationsAsync()
    {
        if (_apiClient == null) return;

        try
        {
            var response = await _apiClient.GetMessagesAsync(direction: 0, page: 1, pageSize: 50);
            if (response?.Items != null)
            {
                _conversations.Clear();
                foreach (var msg in response.Items)
                {
                    var phone = msg.Originator;
                    if (!_conversations.ContainsKey(phone))
                    {
                        _conversations[phone] = new ConversationInfo
                        {
                            Phone = phone,
                            LastMessage = msg.Body,
                            LastMessageAt = msg.MessageAt,
                            Unread = true
                        };
                    }
                    else
                    {
                        var conv = _conversations[phone];
                        if (msg.MessageAt > conv.LastMessageAt)
                        {
                            conv.LastMessage = msg.Body;
                            conv.LastMessageAt = msg.MessageAt;
                            conv.Unread = true;
                        }
                    }
                }

                UpdateConversationsList();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error al cargar conversaciones: {ex.Message}");
        }
    }

    private void UpdateConversationsList()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(UpdateConversationsList));
            return;
        }

        _lstConversations.Items.Clear();
        foreach (var conv in _conversations.Values.OrderByDescending(c => c.LastMessageAt))
        {
            var preview = conv.LastMessage.Length > 30 
                ? conv.LastMessage.Substring(0, 30) + "..." 
                : conv.LastMessage;
            var timeStr = FormatTime(conv.LastMessageAt);
            var item = new ListViewItem(new[] { conv.Phone, preview, timeStr })
            {
                Tag = conv.Phone,
                ForeColor = conv.Unread ? Color.Blue : Color.Black,
                Font = conv.Unread ? new Font(Font, FontStyle.Bold) : Font
            };
            _lstConversations.Items.Add(item);
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

    private async void BtnSearch_Click(object? sender, EventArgs e)
    {
        var searchPhone = _txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchPhone) || searchPhone == "Buscar por teléfono...")
        {
            await LoadConversationsAsync();
        }
        else
        {
            await LoadConversationsAsync(searchPhone);
        }
    }

    private async Task LoadConversationsAsync(string? searchPhone = null)
    {
        if (_apiClient == null) return;

        try
        {
            var response = await _apiClient.GetMessagesAsync(
                direction: 0,
                page: 1,
                pageSize: 50,
                phone: searchPhone);
            
            if (response?.Items != null)
            {
                _conversations.Clear();
                foreach (var msg in response.Items)
                {
                    var phone = msg.Originator;
                    if (!_conversations.ContainsKey(phone))
                    {
                        _conversations[phone] = new ConversationInfo
                        {
                            Phone = phone,
                            LastMessage = msg.Body,
                            LastMessageAt = msg.MessageAt,
                            Unread = true
                        };
                    }
                }
                UpdateConversationsList();
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error al buscar: {ex.Message}");
        }
    }

    private async void LstConversations_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lstConversations.SelectedItems.Count == 0) return;

        var phone = _lstConversations.SelectedItems[0].Tag?.ToString();
        if (phone != null)
        {
            await LoadConversationAsync(phone);
        }
    }

    private void LstConversations_DoubleClick(object? sender, EventArgs e)
    {
        LstConversations_SelectedIndexChanged(sender, e);
    }

    private async Task LoadConversationAsync(string phone)
    {
        if (_apiClient == null) return;

        _selectedPhone = phone;

        if (InvokeRequired)
        {
            Invoke(new Action(() => _lblSelectedPhone.Text = $"Conversación: {phone}"));
        }
        else
        {
            _lblSelectedPhone.Text = $"Conversación: {phone}";
        }

        try
        {
            var messages = await _apiClient.GetConversationMessagesAsync(phone, take: 200);
            if (messages != null)
            {
                UpdateChat(messages);
                
                // Marcar como leída
                if (_conversations.ContainsKey(phone))
                {
                    _conversations[phone].Unread = false;
                    UpdateConversationsList();
                }
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error al cargar conversación: {ex.Message}");
        }
    }

    private void UpdateChat(List<MessageDto> messages)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<List<MessageDto>>(UpdateChat), messages);
            return;
        }

        _lstChat.Items.Clear();
        foreach (var msg in messages)
        {
            _lstChat.Items.Add(msg);
        }
        _lstChat.TopIndex = _lstChat.Items.Count - 1; // Scroll al final
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
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

        try
        {
            var response = await _apiClient.SendMessageAsync(_selectedPhone, messageText);
            if (response?.Success == true)
            {
                _txtMessage.Clear();
                // El mensaje se añadirá automáticamente cuando llegue por SignalR
                // Pero lo añadimos inmediatamente para mejor UX
                var sentMsg = new MessageDto
                {
                    Id = response.Id ?? 0,
                    Originator = _settings.ApiBaseUrl, // Placeholder, se actualizará con SignalR
                    Recipient = _selectedPhone,
                    Body = messageText,
                    Direction = 1,
                    MessageAt = DateTime.UtcNow
                };
                AddMessageToChat(sentMsg);
            }
            else
            {
                MessageBox.Show($"Error al enviar: {response?.Error ?? "Error desconocido"}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al enviar mensaje: {ex.Message}", 
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddMessageToChat(MessageDto message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<MessageDto>(AddMessageToChat), message);
            return;
        }

        // Verificar si ya existe (evitar duplicados de SignalR)
        foreach (MessageDto existing in _lstChat.Items)
        {
            if (existing.Id == message.Id && existing.Id != 0)
                return;
        }

        _lstChat.Items.Add(message);
        _lstChat.TopIndex = _lstChat.Items.Count - 1;
    }

    private void SignalRService_OnNewMessage(MessageDto message)
    {
        var phone = message.Originator;
        
        // Actualizar conversación
        if (_conversations.ContainsKey(phone))
        {
            _conversations[phone].LastMessage = message.Body;
            _conversations[phone].LastMessageAt = message.MessageAt;
            _conversations[phone].Unread = true;
        }
        else
        {
            _conversations[phone] = new ConversationInfo
            {
                Phone = phone,
                LastMessage = message.Body,
                LastMessageAt = message.MessageAt,
                Unread = true
            };
        }

        UpdateConversationsList();

        // Si esta conversación está abierta, añadir al chat
        if (_selectedPhone == phone)
        {
            AddMessageToChat(message);
            _conversations[phone].Unread = false;
        }
    }

    private void SignalRService_OnNewSentMessage(MessageDto message)
    {
        var phone = message.Recipient;
        
        // Actualizar conversación
        if (_conversations.ContainsKey(phone))
        {
            _conversations[phone].LastMessage = message.Body;
            _conversations[phone].LastMessageAt = message.MessageAt;
        }
        else
        {
            _conversations[phone] = new ConversationInfo
            {
                Phone = phone,
                LastMessage = message.Body,
                LastMessageAt = message.MessageAt,
                Unread = false
            };
        }

        UpdateConversationsList();

        // Si esta conversación está abierta, añadir al chat
        if (_selectedPhone == phone)
        {
            AddMessageToChat(message);
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
        _lblApiStatus.ForeColor = connected ? Color.Green : Color.Red;
    }

    private void UpdateSignalRStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(UpdateSignalRStatus), connected);
            return;
        }

        _lblSignalRStatus.Text = connected ? "SignalR: Conectado" : "SignalR: Desconectado";
        _lblSignalRStatus.ForeColor = connected ? Color.Green : Color.Red;
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

    private string FormatTime(DateTime dateTime)
    {
        var local = dateTime.ToLocalTime();
        var diff = DateTime.Now - local;
        
        if (diff.TotalMinutes < 1)
            return "Ahora";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}d";
        
        return local.ToString("dd/MM HH:mm");
    }

    private void LstChat_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || sender is not ListBox listBox) return;

        var message = listBox.Items[e.Index] as MessageDto;
        if (message == null) return;

        e.DrawBackground();

        var isOutbound = message.Direction == 1;
        var backColor = isOutbound ? Color.LightBlue : Color.LightGray;
        var textColor = Color.Black;

        using (var brush = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var text = $"[{FormatTime(message.MessageAt)}] {(isOutbound ? "Yo" : message.Originator)}: {message.Body}";
        using (var brush = new SolidBrush(textColor))
        {
            e.Graphics.DrawString(text, e.Font, brush, e.Bounds);
        }

        e.DrawFocusRectangle();
    }

    private void LstChat_MeasureItem(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index < 0 || sender is not ListBox listBox) return;

        var message = listBox.Items[e.Index] as MessageDto;
        if (message == null)
        {
            e.ItemHeight = 20;
            return;
        }

        var text = $"[{FormatTime(message.MessageAt)}] {(message.Direction == 1 ? "Yo" : message.Originator)}: {message.Body}";
        var size = TextRenderer.MeasureText(text, listBox.Font, new Size(listBox.Width - 10, int.MaxValue), TextFormatFlags.WordBreak);
        e.ItemHeight = Math.Max(20, size.Height + 4);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _signalRService?.Dispose();
        _apiClient?.Dispose();
        base.OnFormClosing(e);
    }
}

internal class ConversationInfo
{
    public string Phone { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public bool Unread { get; set; }
}
