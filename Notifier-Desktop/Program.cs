namespace NotifierDesktop;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // Cargar configuración
        var settings = AppSettings.Load();
        
        // Mostrar formulario de configuración si la URL no está configurada
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl) || 
            !Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out _))
        {
            using var settingsForm = new SettingsForm(settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settingsForm.Settings.Save();
                settings = settingsForm.Settings;
            }
            else
            {
                return; // Salir si canceló
            }
        }

        // Crear y mostrar formulario principal
        var mainForm = new FormMain(settings);
        
        // Añadir menú para configuración
        var menuStrip = new MenuStrip();
        var settingsMenuItem = new ToolStripMenuItem("Configuración");
        settingsMenuItem.Click += (s, e) =>
        {
            using var settingsForm = new SettingsForm(settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settingsForm.Settings.Save();
                settings.ApiBaseUrl = settingsForm.Settings.ApiBaseUrl;
                settings.OperatorName = settingsForm.Settings.OperatorName;
                MessageBox.Show("La configuración se aplicará al reiniciar la aplicación.", 
                    "Configuración", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        menuStrip.Items.Add(settingsMenuItem);
        mainForm.MainMenuStrip = menuStrip;
        mainForm.Controls.Add(menuStrip);
        menuStrip.Dock = DockStyle.Top;

        Application.Run(mainForm);
    }    
}