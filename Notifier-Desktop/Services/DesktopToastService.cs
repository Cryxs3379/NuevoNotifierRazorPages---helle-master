using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NotifierDesktop.UI;

namespace NotifierDesktop.Services;

public class DesktopToastService
{
    private readonly List<ToastNotificationForm> _activeToasts = new();
    private readonly object _lock = new();

    private const int ToastWidth = 380;
    private const int Margin = 16;
    private const int Spacing = 10;

    public void ShowToast(
        string title,
        string body,
        Control? uiThreadControl = null,
        int? customHeight = null,
        Color? accentColor = null,
        ToastVisualStyle style = ToastVisualStyle.Neutral)
    {
        if (uiThreadControl != null && uiThreadControl.InvokeRequired)
        {
            uiThreadControl.BeginInvoke(new Action(() =>
                ShowToast(title, body, uiThreadControl, customHeight, accentColor, style)));
            return;
        }

        if (uiThreadControl == null)
        {
            var mainForm = Application.OpenForms.Cast<Form>().FirstOrDefault();
            if (mainForm != null && mainForm.InvokeRequired)
            {
                mainForm.BeginInvoke(new Action(() =>
                    ShowToast(title, body, mainForm, customHeight, accentColor, style)));
                return;
            }
        }

        lock (_lock)
        {
            CleanupDisposed();

            var toast = new ToastNotificationForm(
                title: title,
                body: body,
                width: ToastWidth,
                customHeight: customHeight,
                accentColor: accentColor,
                style: style);

            var screen = GetTargetScreen();
            var wa = screen.WorkingArea;

            // base bottom-right
            int x = wa.Right - ToastWidth - Margin;
            int y = wa.Bottom - Margin - toast.Height;

            // stacking
            foreach (var t in _activeToasts)
                y -= (t.Height + Spacing);

            toast.Location = new Point(x, y);

            _activeToasts.Add(toast);
            toast.FormClosed += (_, __) => RemoveToast(toast);
            toast.ShowToast();
        }
    }

    private void RemoveToast(ToastNotificationForm toast)
    {
        lock (_lock)
        {
            _activeToasts.Remove(toast);
            CleanupDisposed();
            Reflow();
        }
    }

    private void Reflow()
    {
        if (_activeToasts.Count == 0) return;

        var screen = GetTargetScreen();
        var wa = screen.WorkingArea;

        int x = wa.Right - ToastWidth - Margin;
        int y = wa.Bottom - Margin;

        foreach (var t in _activeToasts)
        {
            y -= t.Height;
            t.Location = new Point(x, y);
            y -= Spacing;
        }
    }

    private void CleanupDisposed()
    {
        _activeToasts.RemoveAll(t => t == null || t.IsDisposed);
    }

    private static Screen GetTargetScreen()
    {
        // Preferimos donde está el usuario (cursor). Si no, primary.
        var cursorPos = Cursor.Position;
        return Screen.FromPoint(cursorPos) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }
}

public enum ToastVisualStyle
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}
