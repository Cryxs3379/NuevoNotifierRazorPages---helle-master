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
        MaximumSize = new Size((int)(Screen.PrimaryScreen?.WorkingArea.Width * 0.7 ?? 500), 0);

        _bubblePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 8, 10, 8),
            AutoSize = true
        };

        _lblText = new Label
        {
            Text = "",
            Font = new Font(Font.FontFamily, 9),
            ForeColor = Color.Black,
            AutoSize = true,
            MaximumSize = new Size((int)(MaximumSize.Width * 0.9), 0),
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
            _lblText.Text = "";
            _lblTime.Text = "";
            return;
        }

        _lblText.Text = _message.Text;
        _lblTime.Text = FormatTime(_message.At);

        var isOutbound = _message.Direction == MessageDirection.Outbound;

        // Colores tipo WhatsApp
        if (isOutbound)
        {
            _bubblePanel.BackColor = Color.FromArgb(220, 248, 198); // Verde claro
            _lblText.ForeColor = Color.Black;
            _lblTime.ForeColor = Color.FromArgb(100, 100, 100);
            Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _bubblePanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }
        else
        {
            _bubblePanel.BackColor = Color.White;
            _lblText.ForeColor = Color.Black;
            _lblTime.ForeColor = Color.FromArgb(100, 100, 100);
            Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _bubblePanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        }

        // Ajustar tamaño del panel según contenido
        var textSize = TextRenderer.MeasureText(_message.Text, _lblText.Font, 
            new Size(_lblText.MaximumSize.Width, int.MaxValue), 
            TextFormatFlags.WordBreak);
        
        _bubblePanel.Height = Math.Max(textSize.Height + 25, 40);
        _bubblePanel.Width = Math.Min(textSize.Width + 40, MaximumSize.Width - 20);
        
        _lblTime.Location = new Point(
            _bubblePanel.Width - _lblTime.Width - 10,
            _bubblePanel.Height - _lblTime.Height - 5);

        Height = _bubblePanel.Height + Padding.Top + Padding.Bottom;
        Width = _bubblePanel.Width + Padding.Left + Padding.Right;

        // Redondear esquinas (simulado con bordes)
        _bubblePanel.Region = System.Drawing.Region.FromHrgn(
            CreateRoundRectRgn(0, 0, _bubblePanel.Width, _bubblePanel.Height, 10, 10));
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
