namespace NotifierDesktop;

public partial class SettingsForm : Form
{
    private readonly TextBox _txtOperatorName;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly AppSettings _settings;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings currentSettings)
    {
        _settings = currentSettings;
        Settings = new AppSettings
        {
            OperatorName = currentSettings.OperatorName
        };

        Text = "Configuración";
        Size = new Size(400, 150);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var lblOperator = new Label
        {
            Text = "Nombre del operador:",
            Location = new Point(12, 20),
            Size = new Size(120, 23),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _txtOperatorName = new TextBox
        {
            Text = Settings.OperatorName,
            Location = new Point(140, 17),
            Size = new Size(230, 23)
        };

        _btnSave = new Button
        {
            Text = "Guardar",
            DialogResult = DialogResult.OK,
            Location = new Point(196, 60),
            Size = new Size(85, 30)
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Location = new Point(285, 60),
            Size = new Size(85, 30)
        };

        Controls.AddRange(new Control[] {
            lblOperator, _txtOperatorName,
            _btnSave, _btnCancel
        });

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var operatorName = _txtOperatorName.Text.Trim();
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            MessageBox.Show("El nombre del operador es requerido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
            return;
        }

        Settings.OperatorName = operatorName;
    }
}
