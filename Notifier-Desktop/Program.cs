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

        // Forzar SIEMPRE el operador en memoria al usuario de Windows
        settings.OperatorName = Environment.UserName;

        // Crear y mostrar formulario principal
        var mainForm = new MainForm(settings);
        
        // Añadir menú para configuración
        var menuStrip = new MenuStrip();
        var settingsMenuItem = new ToolStripMenuItem("Configuración");
        settingsMenuItem.Click += (s, e) =>
        {
            using var settingsForm = new SettingsForm(settings);
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                settingsForm.Settings.Save();
                settings.OperatorName = settingsForm.Settings.OperatorName;
                // El MainForm usa _settings directamente, así que se actualiza automáticamente
                MessageBox.Show($"El nombre del operador se ha actualizado a: {settings.OperatorName}\nSe aplicará al enviar nuevos mensajes.", 
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