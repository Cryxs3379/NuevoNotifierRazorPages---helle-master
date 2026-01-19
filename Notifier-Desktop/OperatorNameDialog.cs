using NotifierDesktop.UI;

namespace NotifierDesktop;

public partial class OperatorNameDialog : Form
{
    private readonly TextBox _txtOperatorName;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string OperatorName => _txtOperatorName.Text.Trim();

    public OperatorNameDialog(string? currentOperatorName = null)
    {
        Text = "Identificación del Operador";
        Size = new Size(500, 200);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        BackColor = Theme.Background;
        Padding = Padding.Empty;
        
        Theme.EnableDoubleBuffer(this);

        // Panel principal con padding interno
        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing20)
        };
        Theme.EnableDoubleBuffer(mainPanel);

        // Label de instrucción
        var lblInstruction = new Label
        {
            Text = "Por favor, ingrese su nombre de operador:",
            Font = Theme.Body,
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(Theme.Spacing20, Theme.Spacing20),
            TextAlign = ContentAlignment.MiddleLeft
        };

        // TextBox para el nombre - usar Anchor para que se ajuste automáticamente
        _txtOperatorName = new TextBox
        {
            Text = currentOperatorName ?? Environment.UserName,
            Font = Theme.Body,
            MaxLength = 64,
            Height = 32,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(Theme.Spacing20, Theme.Spacing20 + 25)
        };
        
        // Panel para botones (alineados a la derecha)
        var buttonsPanel = new Panel
        {
            Height = 70,
            Dock = DockStyle.Bottom,
            BackColor = Theme.Surface,
            Padding = new Padding(Theme.Spacing20, Theme.Spacing16, Theme.Spacing20, Theme.Spacing16)
        };
        Theme.EnableDoubleBuffer(buttonsPanel);
        
        _btnOk = new Button
        {
            Text = "Aceptar",
            DialogResult = DialogResult.OK,
            Size = new Size(100, 32),
            Font = Theme.Body,
            BackColor = Theme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.FlatAppearance.MouseOverBackColor = Theme.AccentBlueHover;
        _btnOk.Click += BtnOk_Click;

        _btnCancel = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 32),
            Font = Theme.Body,
            BackColor = Theme.Surface,
            ForeColor = Theme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderSize = 1;
        _btnCancel.FlatAppearance.BorderColor = Theme.Border;
        _btnCancel.FlatAppearance.MouseOverBackColor = Theme.SurfaceHover;

        buttonsPanel.Controls.Add(_btnOk);
        buttonsPanel.Controls.Add(_btnCancel);
        
        mainPanel.Controls.Add(lblInstruction);
        mainPanel.Controls.Add(_txtOperatorName);
        Controls.Add(mainPanel);
        Controls.Add(buttonsPanel);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        
        // Ajustar posiciones después de que se cargue el formulario
        Load += (s, e) =>
        {
            // Ajustar ancho del TextBox usando el ClientSize del panel (sin el padding)
            _txtOperatorName.Width = mainPanel.ClientSize.Width - (Theme.Spacing20 * 2);
            
            // Calcular posiciones de botones usando el ClientSize del panel de botones
            var buttonY = (buttonsPanel.ClientSize.Height - 32) / 2;
            var buttonSpacing = 10;
            _btnCancel.Location = new Point(buttonsPanel.ClientSize.Width - Theme.Spacing20 - 100, buttonY);
            _btnOk.Location = new Point(_btnCancel.Left - 100 - buttonSpacing, buttonY);
            
            // Seleccionar texto y enfocar
            _txtOperatorName.SelectAll();
            _txtOperatorName.Focus();
        };
        
        // Ajustar cuando cambie el tamaño
        mainPanel.Resize += (s, e) =>
        {
            _txtOperatorName.Width = mainPanel.ClientSize.Width - (Theme.Spacing20 * 2);
        };
        
        buttonsPanel.Resize += (s, e) =>
        {
            var buttonY = (buttonsPanel.ClientSize.Height - 32) / 2;
            var buttonSpacing = 10;
            _btnCancel.Location = new Point(buttonsPanel.ClientSize.Width - Theme.Spacing20 - 100, buttonY);
            _btnOk.Location = new Point(_btnCancel.Left - 100 - buttonSpacing, buttonY);
        };
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtOperatorName.Text))
        {
            MessageBox.Show("El nombre del operador es requerido para usar el sistema.", 
                "Nombre Requerido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            _txtOperatorName.Focus();
            _txtOperatorName.SelectAll();
            return;
        }
    }
}
