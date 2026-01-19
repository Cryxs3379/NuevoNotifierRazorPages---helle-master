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
        
        // SIEMPRE pedir el nombre del operador al iniciar (obligatorio)
        using var operatorDialog = new OperatorNameDialog(settings.OperatorName);
        if (operatorDialog.ShowDialog() != DialogResult.OK)
        {
            // Si cancela, salir de la aplicación
            return;
        }
        
        // Actualizar el nombre del operador y guardarlo
        settings.OperatorName = operatorDialog.OperatorName;
        settings.Save();

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