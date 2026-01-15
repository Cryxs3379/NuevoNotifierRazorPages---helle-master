using System.Runtime.InteropServices;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop.Controls;

public partial class MessageBubbleControl : UserControl
{
    private MessageVm? _message;
    private readonly Label _lblText;
    private readonly Label _lblTime;
    private readonly Panel _bubblePanel;

    public MessageVm? Message
    {
        get => _message;
        set
        {
            _message = value;
            UpdateUI();
        }
    }

    public MessageBubbleControl()
    {
        AutoSize = true;
        Padding = new Padding(5, 3, 5, 3);
        MaximumSize = new Size(600, 0);  // Valor fijo razonable

        _bubblePanel = new Panel
        {
            Dock = DockStyle.None,  // Cambiar de Fill a None
            Padding = new Padding(10, 8, 10, 8),
            AutoSize = false,  // Cambiar a false para control manual
            Location = new Point(0, 0),  // Establecer location inicial
            Size = new Size(200, 50)  // Tamaño inicial mínimo
        };

        _lblText = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 9),
            ForeColor = Color.Black,
            AutoSize = true,
            MaximumSize = new Size(550, 0),  // Valor fijo en lugar de calcular desde MaximumSize
            Location = new Point(10, 8)
        };

        _lblTime = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 7),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(10, 30)
        };

        _bubblePanel.Controls.Add(_lblText);
        _bubblePanel.Controls.Add(_lblTime);

        Controls.Add(_bubblePanel);
    }

    private void UpdateUI()
    {
        if (_message == null)
        {
            System.Diagnostics.Debug.WriteLine("[MessageBubbleControl] UpdateUI called with null message");
            _lblText.Text = "";
            _lblTime.Text = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(_message.Text))
        {
            System.Diagnostics.Debug.WriteLine($"[MessageBubbleControl] WARNING: Message text is empty. Id={_message.Id}, Direction={_message.Direction}");
            _lblText.Text = "(mensaje vacío)";
        }
        else
        {
            _lblText.Text = _message.Text;
        }
        
        _lblTime.Text = FormatTime(_message.At);

        var isOutbound = _message.Direction == MessageDirection.Outbound;

        // Colores tipo WhatsApp
        if (isOutbound)
        {
            _bubblePanel.BackColor = Color.FromArgb(220, 248, 198); // Verde claro
            _lblText.ForeColor = Color.Black;
            _lblTime.ForeColor = Color.FromArgb(100, 100, 100);
            Anchor = AnchorStyles.Top | AnchorStyles.Right;
            // Remover Anchor de _bubblePanel - establecer posición manualmente
        }
        else
        {
            _bubblePanel.BackColor = Color.White;
            _lblText.ForeColor = Color.Black;
            _lblTime.ForeColor = Color.FromArgb(100, 100, 100);
            Anchor = AnchorStyles.Top | AnchorStyles.Left;
            // Remover Anchor de _bubblePanel - establecer posición manualmente
        }

        // Ajustar tamaño del panel según contenido
        var textSize = TextRenderer.MeasureText(_message.Text, _lblText.Font, 
            new Size(_lblText.MaximumSize.Width, int.MaxValue), 
            TextFormatFlags.WordBreak);
        
        var bubbleHeight = Math.Max(textSize.Height + 25, 40);
        var bubbleWidth = Math.Min(textSize.Width + 40, MaximumSize.Width - 20);
        
        // Establecer Location y Size explícitamente (necesario con Dock=None)
        _bubblePanel.Location = new Point(Padding.Left, Padding.Top);
        _bubblePanel.Size = new Size(
            Math.Max(bubbleWidth, 100),  // Tamaño mínimo visible
            Math.Max(bubbleHeight, 40)
        );
        
        _lblTime.Location = new Point(
            _bubblePanel.Width - _lblTime.Width - 10,
            _bubblePanel.Height - _lblTime.Height - 5);

        // Asegurar que el UserControl tenga un tamaño mínimo visible
        Height = Math.Max(_bubblePanel.Height + Padding.Top + Padding.Bottom, 50);
        Width = Math.Max(_bubblePanel.Width + Padding.Left + Padding.Right, 100);

        // Redondear esquinas (simulado con bordes)
        _bubblePanel.Region = System.Drawing.Region.FromHrgn(
            CreateRoundRectRgn(0, 0, _bubblePanel.Width, _bubblePanel.Height, 10, 10));
        
        System.Diagnostics.Debug.WriteLine($"[MessageBubbleControl] UpdateUI completed: UserControl Size={Size}, BubblePanel Size={_bubblePanel.Size}, Visible={Visible}");
    }

    private string FormatTime(DateTime dateTime)
    {
        var local = dateTime.ToLocalTime();
        return local.ToString("HH:mm");
    }

    [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
        int nWidthEllipse, int nHeightEllipse);
}
