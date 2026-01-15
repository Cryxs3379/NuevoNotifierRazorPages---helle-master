namespace NotifierDesktop;

public partial class SettingsForm : Form
{
    private readonly TextBox _txtApiBaseUrl;
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
            ApiBaseUrl = currentSettings.ApiBaseUrl,
            OperatorName = currentSettings.OperatorName
        };

        Text = "Configuración";
        Size = new Size(500, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var lblApiUrl = new Label
        {
            Text = "URL de la API:",
            Location = new Point(12, 15),
            Size = new Size(120, 23),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _txtApiBaseUrl = new TextBox
        {
            Text = Settings.ApiBaseUrl,
            Location = new Point(140, 12),
            Size = new Size(330, 23)
        };

        var lblOperator = new Label
        {
            Text = "Nombre del operador:",
            Location = new Point(12, 50),
            Size = new Size(120, 23),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _txtOperatorName = new TextBox
        {
            Text = Settings.OperatorName,
            Location = new Point(140, 47),
            Size = new Size(330, 23)
        };

        _btnSave = new Button
        {
            Text = "Guardar",
            DialogResult = DialogResult.OK,
            Location = new Point(296, 90),
            Size = new Size(85, 30)
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Location = new Point(385, 90),
            Size = new Size(85, 30)
        };

        Controls.AddRange(new Control[] {
            lblApiUrl, _txtApiBaseUrl,
            lblOperator, _txtOperatorName,
            _btnSave, _btnCancel
        });

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtApiBaseUrl.Text))
        {
            MessageBox.Show("La URL de la API es requerida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
            return;
        }

        if (!Uri.TryCreate(_txtApiBaseUrl.Text, UriKind.Absolute, out _))
        {
            MessageBox.Show("La URL de la API no es válida.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
            return;
        }

        Settings.ApiBaseUrl = _txtApiBaseUrl.Text.Trim();
        Settings.OperatorName = _txtOperatorName.Text.Trim();
        if (string.IsNullOrWhiteSpace(Settings.OperatorName))
        {
            Settings.OperatorName = Environment.UserName;
        }
    }
}
