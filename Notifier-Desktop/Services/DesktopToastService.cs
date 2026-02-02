using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NotifierDesktop.UI;

namespace NotifierDesktop.Services;

public class DesktopToastService
{
    private readonly List<ToastNotificationForm> _activeToasts = new();
    private readonly object _lock = new();
    private const int ToastWidth = 360;
    private const int Margin = 14;
    private const int Spacing = 10;

    public void ShowToast(string title, string body, Control? uiThreadControl = null, int? customHeight = null)
    {
        // Asegurar ejecución en UI thread
        if (uiThreadControl != null && uiThreadControl.InvokeRequired)
        {
            uiThreadControl.BeginInvoke(new Action(() => ShowToast(title, body, uiThreadControl, customHeight)));
            return;
        }

        // Si no hay control, intentar obtener uno de Application.OpenForms
        if (uiThreadControl == null)
        {
            var mainForm = Application.OpenForms.Cast<Form>().FirstOrDefault();
            if (mainForm != null && mainForm.InvokeRequired)
            {
                mainForm.BeginInvoke(new Action(() => ShowToast(title, body, mainForm, customHeight)));
                return;
            }
        }

        lock (_lock)
        {
            // Crear nuevo toast con altura personalizada si se proporciona
            var toast = customHeight.HasValue
                ? new ToastNotificationForm(title, body, ToastWidth, customHeight.Value)
                : new ToastNotificationForm(title, body, ToastWidth);
            
            // Posicionar abajo-derecha
            var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            var workingArea = screen.WorkingArea;
            
            int x = workingArea.Right - ToastWidth - Margin;
            int y = workingArea.Bottom - Margin - toast.Height;
            
            // Ajustar Y para stacking (apilar hacia arriba)
            foreach (var activeToast in _activeToasts.ToList())
            {
                if (activeToast != null && !activeToast.IsDisposed)
                {
                    y -= (activeToast.Height + Spacing);
                }
            }
            
            toast.Location = new System.Drawing.Point(x, y);
            
            // Añadir a lista y mostrar
            _activeToasts.Add(toast);
            toast.FormClosed += (s, e) => RemoveToast(toast);
            toast.ShowToast();
        }
    }

    private void RemoveToast(ToastNotificationForm toast)
    {
        lock (_lock)
        {
            _activeToasts.Remove(toast);
            
            // Reorganizar toasts restantes (reflow)
            var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            var workingArea = screen.WorkingArea;
            
            int baseY = workingArea.Bottom - Margin;
            
            foreach (var activeToast in _activeToasts.ToList())
            {
                if (activeToast != null && !activeToast.IsDisposed)
                {
                    baseY -= (activeToast.Height + Spacing);
                    activeToast.Location = new System.Drawing.Point(
                        workingArea.Right - ToastWidth - Margin,
                        baseY
                    );
                }
            }
        }
    }
}
