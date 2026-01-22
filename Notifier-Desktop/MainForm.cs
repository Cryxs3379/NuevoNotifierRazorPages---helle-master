using System.ComponentModel;
using System.Linq;
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

    // Panel de Llamadas Perdidas
    private TabControl _tabControl;
    private TabPage _tabConversations;
    private TabPage _tabMissedCalls;
    private DataGridView _dgvMissedCalls;
    private BindingSource _missedCallsBindingSource;
    private Label _lblMissedCallsTitle;
    private Label _lblMissedCallsCount;
    private Label _lblMissedCallsLastUpdate;
    private List<MissedCallVm> _missedCalls = new();

    private string? _selectedPhone;
    private bool _isWindowFocused = true;

    // IMPORTANTE: usado para aplicar SplitterDistance una vez que hay layout real
    private bool _splitterInitialized = false;

    // Cache de controles de conversación para reutilización (optimización de performance)
    private readonly Dictionary<string, ConversationRowControl> _conversationControls = new();

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
    }

    /// <summary>
    /// Aplica SplitterDistance y MinSizes de forma segura (solo cuando el SplitContainer ya tiene tamaño real).
    /// Evita InvalidOperationException cuando el control aún no está layouted (ancho 0).
    /// </summary>
    private void ApplySplitterLayout()
    {
        if (_splitContainer == null || _splitContainer.IsDisposed) return;
        if (!_splitContainer.IsHandleCreated) return;

        var w = _splitContainer.ClientSize.Width;
        if (w <= 0) return;

        const int preferredMin1 = 300;
        const int preferredMin2 = 500;

        // Si el ancho no permite los mínimos preferidos, relajamos para no romper
        int min1 = preferredMin1;
        int min2 = preferredMin2;

        var required = preferredMin1 + preferredMin2 + _splitContainer.SplitterWidth;
        if (w < required)
        {
            min1 = 200;
            min2 = 200;
        }

        // IMPORTANTE: MinSizes SOLO aquí (cuando ya hay ancho real)
        _splitContainer.Panel1MinSize = min1;
        _splitContainer.Panel2MinSize = min2;

        var min = _splitContainer.Panel1MinSize;
        var max = w - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
        if (max < min) max = min;

        var target = (int)(w * 0.32);
        var dist = Math.Clamp(target, min, max);

        if (_splitContainer.SplitterDistance == dist) return;

        try
        {
            _splitContainer.SplitterDistance = dist;
        }
        catch (InvalidOperationException)
        {
            // Durante ciertos momentos de layout WinForms puede lanzar aunque el clamp parezca correcto.
            // Reintentamos con el mínimo permitido.
            try { _splitContainer.SplitterDistance = min; } catch { /* último recurso: ignorar */ }
        }
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
            Height = 40,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(statusPanel);

        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0)
        };
        Theme.EnableDoubleBuffer(statusFlow);

        _lblApiStatus = new Label
        {
            Text = "API: Desconectado",
            ForeColor = Color.White,
            BackColor = Theme.Danger,
            Font = Theme.Small,
            AutoSize = true,
            Padding = new Padding(Theme.Spacing8, Theme.Spacing4, Theme.Spacing8, Theme.Spacing4),
            Margin = new Padding(0, 0, Theme.Spacing8, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblSignalRStatus = new Label
        {
            Text = "SignalR: Desconectado",
            ForeColor = Color.White,
            BackColor = Theme.Danger,
            Font = Theme.Small,
            AutoSize = true,
            Padding = new Padding(Theme.Spacing8, Theme.Spacing4, Theme.Spacing8, Theme.Spacing4),
            Margin = new Padding(0, 0, Theme.Spacing8, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblError = new Label
        {
            Text = "",
            ForeColor = Theme.Danger,
            Font = Theme.Small,
            AutoSize = true,
            Margin = new Padding(Theme.Spacing8, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        statusFlow.Controls.Add(_lblApiStatus);
        statusFlow.Controls.Add(_lblSignalRStatus);
        statusFlow.Controls.Add(_lblError);
        statusPanel.Controls.Add(statusFlow);

        // TabControl
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            Font = Theme.Body,
            BackColor = Theme.Background,
            Padding = new Point(Theme.Spacing8, Theme.Spacing4)
        };
        Theme.EnableDoubleBuffer(_tabControl);

        _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabControl.DrawItem += (s, e) =>
        {
            var tabPage = _tabControl.TabPages[e.Index];
            var tabRect = _tabControl.GetTabRect(e.Index);
            var isSelected = _tabControl.SelectedIndex == e.Index;

            e.Graphics.FillRectangle(new SolidBrush(isSelected ? Theme.Background : Theme.Surface), tabRect);
            TextRenderer.DrawText(e.Graphics, tabPage.Text, Theme.Body, tabRect,
                isSelected ? Theme.TextPrimary : Theme.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        // Tab: Conversaciones
        _tabConversations = new TabPage("Conversaciones");
        var conversationsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
        conversationsPanel.BackColor = Theme.Background;
        Theme.EnableDoubleBuffer(conversationsPanel);

        var conversationsHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8)
        };
        Theme.EnableDoubleBuffer(conversationsHeader);

        var lblConversationsTitle = new Label
        {
            Text = "CONVERSACIONES (SMS)",
            Font = Theme.Title,
            ForeColor = Theme.TextPrimary,
            Location = new Point(Theme.Spacing12, Theme.Spacing8),
            AutoSize = true
        };

        var lblPendingCount = new Label
        {
            Text = "Pendientes: 0",
            Font = Theme.Body,
            ForeColor = Theme.TextSecondary,
            Location = new Point(Theme.Spacing12, 28),
            AutoSize = true,
            Name = "lblPendingCount"
        };

        conversationsHeader.Controls.Add(lblConversationsTitle);
        conversationsHeader.Controls.Add(lblPendingCount);

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "Buscar por teléfono...",
            Font = Theme.Body,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextSecondary,
            Padding = new Padding(Theme.Spacing8, Theme.Spacing8, Theme.Spacing8, Theme.Spacing8),
            Margin = new Padding(Theme.Spacing8, Theme.Spacing8, Theme.Spacing8, 0)
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

        conversationsPanel.Controls.Add(_flowReceived);
        conversationsPanel.Controls.Add(_txtSearch);
        conversationsPanel.Controls.Add(conversationsHeader);
        _tabConversations.Controls.Add(conversationsPanel);

        // Tab: Llamadas Perdidas
        _tabMissedCalls = new TabPage("Llamadas Perdidas");
        var missedCallsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };
        missedCallsPanel.BackColor = Theme.Background;
        Theme.EnableDoubleBuffer(missedCallsPanel);

        var missedCallsHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing16, Theme.Spacing12, Theme.Spacing16, Theme.Spacing12)
        };
        Theme.EnableDoubleBuffer(missedCallsHeader);

        var missedCallsHeaderLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        missedCallsHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        missedCallsHeaderLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));

        _lblMissedCallsTitle = new Label
        {
            Text = "Llamadas Perdidas",
            Font = Theme.Title,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, Theme.Spacing4)
        };

        _lblMissedCallsCount = new Label
        {
            Text = "Total: 0",
            Font = Theme.Body,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0)
        };

        _lblMissedCallsLastUpdate = new Label
        {
            Text = "",
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = false,
            Margin = new Padding(0)
        };

        var missedCallsLeftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        missedCallsLeftPanel.Controls.Add(_lblMissedCallsTitle);
        missedCallsLeftPanel.Controls.Add(_lblMissedCallsCount);

        missedCallsHeaderLayout.Controls.Add(missedCallsLeftPanel, 0, 0);
        missedCallsHeaderLayout.Controls.Add(_lblMissedCallsLastUpdate, 1, 0);
        missedCallsHeader.Controls.Add(missedCallsHeaderLayout);

        _missedCallsBindingSource = new BindingSource();
        _dgvMissedCalls = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = _missedCallsBindingSource,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoGenerateColumns = false,
            ScrollBars = ScrollBars.Both,
            BackgroundColor = Theme.Background,
            GridColor = Theme.Border,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 40,
            RowTemplate = { Height = 35 },
            EnableHeadersVisualStyles = false,
            Padding = new Padding(0)
        };
        Theme.EnableDoubleBuffer(_dgvMissedCalls);

        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.BackColor = Theme.Surface;
        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextPrimary;
        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.Font = Theme.BodyBold;
        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8);
        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        _dgvMissedCalls.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _dgvMissedCalls.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Surface;

        _dgvMissedCalls.AlternatingRowsDefaultCellStyle.BackColor = Theme.Surface;
        _dgvMissedCalls.DefaultCellStyle.BackColor = Theme.Background;
        _dgvMissedCalls.DefaultCellStyle.ForeColor = Theme.TextPrimary;
        _dgvMissedCalls.DefaultCellStyle.Font = Theme.Body;
        _dgvMissedCalls.DefaultCellStyle.Padding = new Padding(Theme.Spacing12, Theme.Spacing8, Theme.Spacing12, Theme.Spacing8);
        var softBlue = Color.FromArgb(220, 230, 255);
        _dgvMissedCalls.DefaultCellStyle.SelectionBackColor = softBlue;
        _dgvMissedCalls.DefaultCellStyle.SelectionForeColor = Theme.TextPrimary;
        _dgvMissedCalls.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        SetupMissedCallsColumns();

        missedCallsPanel.Controls.Add(_dgvMissedCalls);
        missedCallsPanel.Controls.Add(missedCallsHeader);
        _tabMissedCalls.Controls.Add(missedCallsPanel);

        _tabControl.TabPages.Add(_tabConversations);
        _tabControl.TabPages.Add(_tabMissedCalls);

        // SplitContainer principal
        // >>> CLAVE: NO establecer Panel1MinSize / Panel2MinSize aquí (puede tener ancho 0 y lanzar excepción)
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.None,
            IsSplitterFixed = false,
            SplitterWidth = 4,
            BackColor = Theme.Border
        };

        // Panel izquierdo
        _splitContainer.Panel1.Controls.Add(_tabControl);

        // Panel derecho: Chat
        var chatPanel = new Panel { Dock = DockStyle.Fill };

        _chatHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
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
            Location = new Point(Theme.Spacing16, 38),
            AutoSize = true
        };

        _chatHeader.Controls.Add(_lblChatPhone);
        _chatHeader.Controls.Add(_lblAssignedTo);

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

        // IMPORTANTE: aplicar SplitterDistance y mins cuando el form ya se ha mostrado (tamaños reales)
        Shown += (s, e) =>
        {
            if (_splitterInitialized) return;
            _splitterInitialized = true;
            ApplySplitterLayout();
        };

        // Reaplicar en resize (solo si ya inicializó)
        SizeChanged += (s, e) =>
        {
            if (!_splitterInitialized) return;
            ApplySplitterLayout();
        };

        Activated += (s, e) => _isWindowFocused = true;
        Deactivate += (s, e) => _isWindowFocused = false;
    }

    private void LoadSettings()
    {
        _apiClient = new ApiClient(AppSettings.ApiBaseUrl);
        _signalRService = new SignalRService(AppSettings.ApiBaseUrl);
        _conversationsController = new ConversationsController(_apiClient, _signalRService);
        _chatController = new ChatController(_apiClient);

        if (_signalRService != null)
        {
            _signalRService.OnNewMessage += SignalRService_OnNewMessage;
            _signalRService.OnNewSentMessage += SignalRService_OnNewSentMessage;
            _signalRService.OnDbError += SignalRService_OnDbError;
            _signalRService.OnEsendexDeleteError += SignalRService_OnEsendexDeleteError;
            _signalRService.OnMissedCallsUpdated += SignalRService_OnMissedCallsUpdated;
            _signalRService.OnConnected += SignalRService_OnConnected;
            _signalRService.OnDisconnected += SignalRService_OnDisconnected;
            _signalRService.OnReconnecting += SignalRService_OnReconnecting;
        }

        Load += (s, e) => { InitializeAsync().GetAwaiter(); };
    }

    private async Task InitializeAsync()
    {
        await CheckApiConnectionAsync();
        await LoadConversationsAsync();
        await LoadMissedCallsAsync();
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

        _flowReceived.SuspendLayout();

        var currentPhones = new HashSet<string>(_conversationsController.Conversations.Select(c => c.Phone));

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

        foreach (var conv in _conversationsController.Conversations)
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
                rowControl.ConversationSelected += async (s, e) => await ConversationRowControl_ConversationSelected(conv.Phone);

                _conversationControls[conv.Phone] = rowControl;
                _flowReceived.Controls.Add(rowControl);
            }
        }

        var conversationOrder = _conversationsController.Conversations
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

        if (_flowReceived == null || _flowReceived.Controls.Count == 0)
            return;

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

    private async Task ConversationRowControl_ConversationSelected(string phone)
    {
        if (_selectedPhone == phone) return;

        var previousPhone = _selectedPhone;
        _selectedPhone = phone;

        UpdateRowSelection(previousPhone, phone);

        var phoneNormalized = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrEmpty(phoneNormalized))
        {
            ShowError($"Número telefónico inválido: {phone}");
            return;
        }

        if (_apiClient != null && !string.IsNullOrWhiteSpace(_settings.OperatorName))
        {
            await _apiClient.ClaimConversationAsync(phoneNormalized, _settings.OperatorName, 5);
        }

        if (_chatController != null)
        {
            await _chatController.LoadChatAsync(phoneNormalized);
            RefreshChat();
        }

        if (_apiClient != null && _isWindowFocused)
        {
            await _apiClient.MarkConversationReadAsync(phoneNormalized);
        }

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

        if (_chatController == null) return;

        _flowChat.Controls.Clear();
        if (_chatController.Messages.Count == 0) return;

        foreach (var msg in _chatController.Messages)
        {
            var bubble = new MessageBubbleControl { Message = msg };

            var maxWidth = _flowChat.Width - 30;
            if (maxWidth > 0 && bubble.Width > maxWidth)
                bubble.Width = maxWidth;

            _flowChat.Controls.Add(bubble);
        }

        if (_flowChat.Controls.Count > 0)
            _flowChat.ScrollControlIntoView(_flowChat.Controls[_flowChat.Controls.Count - 1]);
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
            var operatorName = !string.IsNullOrWhiteSpace(_settings.OperatorName)
                ? _settings.OperatorName
                : Environment.UserName;

            var response = await _apiClient.SendMessageAsync(phoneNormalized, messageText, operatorName);
            if (response?.Success == true)
            {
                _txtMessage.Clear();
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
            _ = BtnSend_Click();
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
            : message.Originator;

        if (string.IsNullOrWhiteSpace(customerPhone)) return;

        var customerPhoneNormalized = Helpers.PhoneNormalizer.Normalize(customerPhone);
        if (string.IsNullOrEmpty(customerPhoneNormalized))
            customerPhoneNormalized = customerPhone;

        var selectedPhoneNormalized = !string.IsNullOrWhiteSpace(_selectedPhone)
            ? Helpers.PhoneNormalizer.Normalize(_selectedPhone)
            : null;
        if (string.IsNullOrEmpty(selectedPhoneNormalized) && !string.IsNullOrWhiteSpace(_selectedPhone))
            selectedPhoneNormalized = _selectedPhone;

        var isMatch = !string.IsNullOrWhiteSpace(selectedPhoneNormalized) &&
                      selectedPhoneNormalized == customerPhoneNormalized;

        if (_conversationsController != null)
        {
            _conversationsController.UpsertFromSignalR(message, isInbound: true);
            RefreshConversationsList();
        }

        if (isMatch && _chatController != null)
        {
            var msgVm = new MessageVm
            {
                Id = message.Id,
                Direction = MessageDirection.Inbound,
                At = message.MessageAt,
                Text = message.Body,
                From = message.Originator,
                To = message.Recipient,
                SentBy = message.SentBy
            };
            _chatController.AddMessage(msgVm);
            RefreshChat();

            if (_isWindowFocused && _apiClient != null)
                _ = _apiClient.MarkConversationReadAsync(customerPhoneNormalized);
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
            : message.Recipient;

        if (string.IsNullOrWhiteSpace(customerPhone)) return;

        var customerPhoneNormalized = Helpers.PhoneNormalizer.Normalize(customerPhone);
        if (string.IsNullOrEmpty(customerPhoneNormalized))
            customerPhoneNormalized = customerPhone;

        var selectedPhoneNormalized = !string.IsNullOrWhiteSpace(_selectedPhone)
            ? Helpers.PhoneNormalizer.Normalize(_selectedPhone)
            : null;
        if (string.IsNullOrEmpty(selectedPhoneNormalized) && !string.IsNullOrWhiteSpace(_selectedPhone))
            selectedPhoneNormalized = _selectedPhone;

        var isMatch = !string.IsNullOrWhiteSpace(selectedPhoneNormalized) &&
                      selectedPhoneNormalized == customerPhoneNormalized;

        if (_conversationsController != null)
        {
            _conversationsController.UpsertFromSignalR(message, isInbound: false);
            RefreshConversationsList();
        }

        if (isMatch && _chatController != null)
        {
            var msgVm = new MessageVm
            {
                Id = message.Id,
                Direction = MessageDirection.Outbound,
                At = message.MessageAt,
                Text = message.Body,
                From = message.Originator,
                To = message.Recipient,
                SentBy = message.SentBy
            };
            _chatController.AddMessage(msgVm);
            RefreshChat();
        }
    }

    private void SignalRService_OnDbError(string error) => ShowError($"Error de BD: {error}");
    private void SignalRService_OnEsendexDeleteError(string error) => ShowError($"Error Esendex: {error}");

    private void SignalRService_OnMissedCallsUpdated()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => SignalRService_OnMissedCallsUpdated()));
            return;
        }

        _ = Task.Run(async () => { await LoadMissedCallsAsync(); });
    }

    private async Task LoadMissedCallsAsync()
    {
        if (_apiClient == null) return;

        try
        {
            var calls = await _apiClient.GetMissedCallsFromViewAsync(limit: 200);

            if (InvokeRequired)
                Invoke(new Action(() => RefreshMissedCallsUI(calls)));
            else
                RefreshMissedCallsUI(calls);
        }
        catch
        {
            if (InvokeRequired)
                Invoke(new Action(() => ShowMissedCallsError("Error al cargar llamadas perdidas")));
            else
                ShowMissedCallsError("Error al cargar llamadas perdidas");
        }
    }

    private void SetupMissedCallsColumns()
    {
        _dgvMissedCalls.Columns.Clear();

        var colId = new DataGridViewTextBoxColumn
        {
            Name = "Id",
            DataPropertyName = "Id",
            HeaderText = "Id",
            Visible = false,
            Width = 0
        };

        var colDateAndTime = new DataGridViewTextBoxColumn
        {
            Name = "DateAndTime",
            DataPropertyName = "DateAndTime",
            HeaderText = "Fecha/Hora",
            Width = 160,
            MinimumWidth = 140,
            Resizable = DataGridViewTriState.True,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "dd/MM/yyyy HH:mm",
                Alignment = DataGridViewContentAlignment.MiddleLeft
            }
        };

        var colPhoneNumber = new DataGridViewTextBoxColumn
        {
            Name = "PhoneNumber",
            DataPropertyName = "PhoneNumber",
            HeaderText = "Teléfono",
            Width = 150,
            MinimumWidth = 120,
            Resizable = DataGridViewTriState.True,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft
            }
        };

        var colNombrePila = new DataGridViewTextBoxColumn
        {
            Name = "NombrePila",
            DataPropertyName = "NombrePila",
            HeaderText = "Nombre",
            MinimumWidth = 120,
            Resizable = DataGridViewTriState.True,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 40,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                NullValue = "",
                Alignment = DataGridViewContentAlignment.MiddleLeft
            }
        };

        var colNombreCompleto = new DataGridViewTextBoxColumn
        {
            Name = "NombreCompleto",
            DataPropertyName = "NombreCompleto",
            HeaderText = "Nombre Completo",
            MinimumWidth = 180,
            Resizable = DataGridViewTriState.True,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 60,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                NullValue = "",
                Alignment = DataGridViewContentAlignment.MiddleLeft
            }
        };

        _dgvMissedCalls.Columns.AddRange(new DataGridViewColumn[]
        {
            colId,
            colDateAndTime,
            colPhoneNumber,
            colNombrePila,
            colNombreCompleto
        });
    }

    private void RefreshMissedCallsUI(List<MissedCallVm>? calls)
    {
        if (calls == null)
        {
            ShowMissedCallsError("No se pudieron cargar las llamadas perdidas");
            return;
        }

        _missedCalls = calls;

        _lblMissedCallsCount.Text = $"Total: {calls.Count}";
        _lblMissedCallsCount.ForeColor = Theme.TextSecondary;

        _lblMissedCallsLastUpdate.Text = $"● Actualizado: {DateTime.Now:HH:mm:ss}";

        _missedCallsBindingSource.DataSource = calls;
        _missedCallsBindingSource.ResetBindings(false);
    }

    private void ShowMissedCallsError(string message)
    {
        _lblMissedCallsCount.Text = message;
        _lblMissedCallsCount.ForeColor = Theme.Danger;
        _lblMissedCallsLastUpdate.Text = "";
    }

    private void SignalRService_OnConnected() => UpdateSignalRStatus(true);
    private void SignalRService_OnDisconnected() => UpdateSignalRStatus(false);

    private void SignalRService_OnReconnecting()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                _lblSignalRStatus.Text = "SignalR: Reconectando...";
                _lblSignalRStatus.BackColor = Color.Orange;
                _lblSignalRStatus.ForeColor = Color.White;
            }));
        }
        else
        {
            _lblSignalRStatus.Text = "SignalR: Reconectando...";
            _lblSignalRStatus.BackColor = Color.Orange;
            _lblSignalRStatus.ForeColor = Color.White;
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
        _lblApiStatus.BackColor = connected ? Theme.Success : Theme.Danger;
        _lblApiStatus.ForeColor = Color.White;
    }

    private void UpdateSignalRStatus(bool connected)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(UpdateSignalRStatus), connected);
            return;
        }

        _lblSignalRStatus.Text = connected ? "SignalR: Conectado" : "SignalR: Desconectado";
        _lblSignalRStatus.BackColor = connected ? Theme.Success : Theme.Danger;
        _lblSignalRStatus.ForeColor = Color.White;
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
