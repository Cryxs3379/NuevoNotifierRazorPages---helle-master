using System.Drawing;
using System.Windows.Forms;

namespace NotifierDesktop.UI;

public static class Theme
{
    // Color Palette
    public static readonly Color Background = Color.FromArgb(248, 249, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceHover = Color.FromArgb(245, 247, 250);
    public static readonly Color SurfaceSelected = Color.FromArgb(235, 240, 245);
    public static readonly Color Border = Color.FromArgb(230, 233, 237);
    public static readonly Color BorderLight = Color.FromArgb(240, 242, 245);
    
    public static readonly Color TextPrimary = Color.FromArgb(33, 37, 41);
    public static readonly Color TextSecondary = Color.FromArgb(108, 117, 125);
    public static readonly Color TextTertiary = Color.FromArgb(173, 181, 189);
    
    public static readonly Color AccentBlue = Color.FromArgb(0, 122, 255);
    public static readonly Color AccentBlueHover = Color.FromArgb(0, 102, 235);
    public static readonly Color AccentGreen = Color.FromArgb(40, 167, 69);
    public static readonly Color AccentOrange = Color.FromArgb(255, 152, 0);
    public static readonly Color AccentRed = Color.FromArgb(220, 53, 69);
    
    public static readonly Color Success = Color.FromArgb(40, 167, 69);
    public static readonly Color Warning = Color.FromArgb(255, 152, 0);
    public static readonly Color Danger = Color.FromArgb(220, 53, 69);
    public static readonly Color Info = Color.FromArgb(0, 122, 255);
    
    // Fonts
    private static readonly FontFamily _fontFamily = SystemFonts.DefaultFont.FontFamily;
    
    public static Font Title => new Font(_fontFamily, 12, FontStyle.Bold);
    public static Font TitleSmall => new Font(_fontFamily, 11, FontStyle.Bold);
    public static Font Body => new Font(_fontFamily, 10, FontStyle.Regular);
    public static Font BodyBold => new Font(_fontFamily, 10, FontStyle.Bold);
    public static Font Small => new Font(_fontFamily, 9, FontStyle.Regular);
    public static Font Tiny => new Font(_fontFamily, 8, FontStyle.Regular);
    
    // Spacing
    public const int Spacing4 = 4;
    public const int Spacing8 = 8;
    public const int Spacing12 = 12;
    public const int Spacing16 = 16;
    public const int Spacing20 = 20;
    
    // Helper: Enable double buffering for a control
    public static void EnableDoubleBuffer(Control control)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(control, true, null);
    }
    
    // Helper: Apply rounded region (simple rounded corners simulation)
    public static void ApplyRoundedRegion(Control control, int radius)
    {
        // For WinForms, we'll use a simple border style instead of actual rounded regions
        // as CreateRoundRectRgn requires P/Invoke and can be complex
        // This is a placeholder - actual rounded corners would need GDI+ drawing
    }
}
