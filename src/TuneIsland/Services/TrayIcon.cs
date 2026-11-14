using System;
using System.Drawing;
using System.Runtime.InteropServices;
using WinForms = System.Windows.Forms;

namespace TuneIsland.Services;

/// <summary>Ícone da bandeja com menu. Usa o NotifyIcon nativo do .NET (sem pacote de terceiros).</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _autostartItem;
    private readonly IntPtr _iconHandle;
    private bool _disposed;

    public event EventHandler? OpenSettings;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? AutostartChanged;

    public TrayIcon()
    {
        var bitmap = CreateBitmap();
        _iconHandle = bitmap.GetHicon();
        var icon = Icon.FromHandle(_iconHandle);

        var menu = new WinForms.ContextMenuStrip();
        var settingsItem = new WinForms.ToolStripMenuItem("Configurações");
        settingsItem.Click += (_, _) => OpenSettings?.Invoke(this, EventArgs.Empty);

        _autostartItem = new WinForms.ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = true };
        _autostartItem.CheckedChanged += (_, _) => AutostartChanged?.Invoke(this, _autostartItem.Checked);

        var exitItem = new WinForms.ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(settingsItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => SetAutostartCheckedSilently(Autostart.IsEnabled());

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = icon,
            Text = "TuneIsland",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings?.Invoke(this, EventArgs.Empty);
    }

    private void SetAutostartCheckedSilently(bool enabled)
    {
        if (_autostartItem.Checked == enabled)
        {
            return;
        }

        // Atualiza sem disparar AutostartChanged novamente.
        _autostartItem.CheckOnClick = false;
        _autostartItem.Checked = enabled;
        _autostartItem.CheckOnClick = true;
    }

    private static Bitmap CreateBitmap()
    {
        var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var body = new SolidBrush(Color.Black);
        g.FillEllipse(body, 1, 6, 30, 20);
        using var bar = new SolidBrush(Color.FromArgb(0x3A, 0xB0, 0xFF));
        g.FillRectangle(bar, 11, 11, 3, 10);
        g.FillRectangle(bar, 18, 9, 3, 12);
        return bitmap;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        DestroyIcon(_iconHandle);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
