using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;

class TempTrayApp : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool DestroyIcon(IntPtr handle);

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    static extern int nvmlInit();
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    static extern int nvmlHandle(uint index, out IntPtr device);
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
    static extern int nvmlTemp(IntPtr device, int sensor, out uint temp);
    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
    static extern int nvmlShutdown();

    NotifyIcon tray;
    ContextMenuStrip menu;
    Computer computer;
    System.Windows.Forms.Timer timer;
    TimerInfo settings = new TimerInfo();
    Icon currentIcon;
    float cpuTemp = -1f;
    float gpuTemp = -1f;
    int gpuTick = 0;
    const int gpuEveryTicks = 3;
    IntPtr nvmlDev = IntPtr.Zero;
    bool nvmlOk = false;
    string lastError = "";
    static string dumpPathStatic = "";
    string logPath;
    string statePath;

    class TimerInfo
    {
        public int seconds = 10;
        public int mode = 0; // 0 both, 1 cpu only, 2 gpu only
    }

    [STAThread]
    static void Main(string[] args)
    {
        foreach (string a in args)
        {
            if (a.StartsWith("--dump=")) dumpPathStatic = a.Substring(7);
        }
        bool createdNew;
        using (Mutex mtx = new Mutex(true, "Global\\TempTraySingleInstance", out createdNew))
        {
            if (!createdNew) return;
            Application.EnableVisualStyles();
            Application.Run(new TempTrayApp());
        }
    }

    public TempTrayApp()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempTray");
        Directory.CreateDirectory(dir);
        logPath = Path.Combine(dir, "log.txt");
        statePath = Path.Combine(dir, "settings.txt");
        LoadSettings();

        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        FormBorderStyle = FormBorderStyle.None;
        Load += delegate { BeginInit(); };

        BuildMenu();
        tray = new NotifyIcon();
        tray.ContextMenuStrip = menu;
        tray.Visible = true;
        tray.Text = "TempTray";
        tray.DoubleClick += delegate { ReadSensors(true); };
        UpdateIcon();
    }

    void BeginInit()
    {
        try
        {
            computer = new Computer();
            computer.IsCpuEnabled = true;
            computer.IsGpuEnabled = false;
            computer.Open();

            try
            {
                if (nvmlInit() == 0 && nvmlHandle(0, out nvmlDev) == 0) nvmlOk = true;
            }
            catch { nvmlOk = false; }
            Log("nvml=" + nvmlOk);
        }
        catch (Exception e)
        {
            lastError = e.GetType().Name + ": " + e.Message;
            Log("open failed: " + lastError);
        }

        timer = new System.Windows.Forms.Timer();
        timer.Interval = settings.seconds * 1000;
        timer.Tick += delegate { ReadSensors(false); };
        timer.Start();

        ReadSensors(true);
        Log("started, elevated=" + IsAdmin());
    }

    static bool IsAdmin()
    {
        try
        {
            System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal p = new System.Security.Principal.WindowsPrincipal(id);
            return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    void BuildMenu()
    {
        menu = new ContextMenuStrip();
        ToolStripMenuItem refresh = new ToolStripMenuItem("Refresh now");
        refresh.Click += delegate { ReadSensors(true); };
        menu.Items.Add(refresh);

        ToolStripMenuItem interval = new ToolStripMenuItem("Refresh interval");
        int[] opts = new int[] { 1, 2, 3, 5, 10 };
        for (int i = 0; i < opts.Length; i++)
        {
            int val = opts[i];
            ToolStripMenuItem mi = new ToolStripMenuItem(val.ToString() + " s");
            mi.Checked = (val == settings.seconds);
            mi.Click += delegate
            {
                settings.seconds = val;
                timer.Interval = val * 1000;
                SaveSettings();
                BuildMenu();
                tray.ContextMenuStrip = menu;
            };
            interval.DropDownItems.Add(mi);
        }
        menu.Items.Add(interval);

        ToolStripMenuItem mode = new ToolStripMenuItem("Show");
        string[] labels = new string[] { "CPU and GPU", "CPU only", "GPU only" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            ToolStripMenuItem mi = new ToolStripMenuItem(labels[i]);
            mi.Checked = (settings.mode == i);
            mi.Click += delegate { settings.mode = idx; SaveSettings(); BuildMenu(); tray.ContextMenuStrip = menu; UpdateIcon(); };
            mode.DropDownItems.Add(mi);
        }
        menu.Items.Add(mode);

        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem startWith = new ToolStripMenuItem("Run at login");
        startWith.Click += delegate { ToggleAutostart(); };
        menu.Items.Add(startWith);

        ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
        exit.Click += delegate { tray.Visible = false; Application.Exit(); };
        menu.Items.Add(exit);
    }

    void ToggleAutostart()
    {
        string taskName = "TempTrayAutostart";
        string exe = Application.ExecutablePath;
        try
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = "schtasks.exe";
            psi.Arguments = "/Query /TN " + taskName;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            System.Diagnostics.Process q = System.Diagnostics.Process.Start(psi);
            q.WaitForExit();
            bool exists = (q.ExitCode == 0);

            psi.Arguments = exists
                ? "/Delete /TN " + taskName + " /F"
                : "/Create /TN " + taskName + " /SC ONLOGON /RL HIGHEST /TR \"\\\"" + exe + "\\\"\" /F";
            System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi);
            p.WaitForExit();
            MessageBox.Show(exists ? "Autostart disabled." : "Autostart enabled.", "TempTray");
        }
        catch (Exception e)
        {
            Log("autostart failed: " + e.Message);
            MessageBox.Show("Failed to change autostart: " + e.Message, "TempTray");
        }
    }

    void LoadSettings()
    {
        try
        {
            if (File.Exists(statePath))
            {
                string[] parts = File.ReadAllText(statePath).Split(',');
                if (parts.Length >= 2)
                {
                    settings.seconds = int.Parse(parts[0]);
                    settings.mode = int.Parse(parts[1]);
                }
            }
        }
        catch { }
    }

    void SaveSettings()
    {
        try { File.WriteAllText(statePath, settings.seconds.ToString() + "," + settings.mode.ToString()); }
        catch { }
    }

    void ReadSensors(bool force)
    {
        if (computer == null) return;
        try
        {
            float cpu = -1f;
            float gpu = gpuTemp;
            gpuTick++;
            if (nvmlOk && (gpuTick % gpuEveryTicks) == 0)
            {
                try
                {
                    uint t;
                    if (nvmlTemp(nvmlDev, 0, out t) == 0) gpu = t;
                }
                catch { }
            }
            foreach (IHardware hw in computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                hw.Update();
                {
                    float best = -1f;
                    float max = -1f;
                    float avg = -1f;
                    foreach (ISensor s in hw.Sensors)
                    {
                        if (s.SensorType != SensorType.Temperature || !s.Value.HasValue) continue;
                        if (s.Name == "CPU Package") best = s.Value.Value;
                        else if (s.Name == "Core Max") max = s.Value.Value;
                        else if (s.Name == "Core Average") avg = s.Value.Value;
                    }
                    float c = best;
                    if (c < 0) c = max;
                    if (c < 0) c = avg;
                    if (c >= 0) cpu = c;
                }
            }
            cpuTemp = cpu;
            gpuTemp = gpu;
            UpdateIcon();
        }
        catch (Exception e)
        {
            lastError = e.GetType().Name + ": " + e.Message;
            Log("read failed: " + lastError);
        }
    }

    void UpdateIcon()
    {
        string cpuText = cpuTemp >= 0 ? Math.Round(cpuTemp).ToString() : "--";
        string gpuText = gpuTemp >= 0 ? Math.Round(gpuTemp).ToString() : "--";
        Icon newIcon = BuildIcon(cpuText, gpuText, settings.mode);
        if (newIcon != null)
        {
            Icon old = currentIcon;
            currentIcon = newIcon;
            tray.Icon = currentIcon;
            if (old != null) { old.Dispose(); }
        }
        string tip = "CPU " + (cpuTemp >= 0 ? Math.Round(cpuTemp) + " C" : "n/a") +
                     "  GPU " + (gpuTemp >= 0 ? Math.Round(gpuTemp) + " C" : "n/a");
        if (!IsAdmin()) tip += "  (needs admin)";
        if (tip.Length > 63) tip = tip.Substring(0, 63);
        tray.Text = tip;
    }

    static Color TempColor(float t)
    {
        if (t < 0) return Color.Gainsboro;
        if (t < 65) return Color.FromArgb(120, 230, 140);
        if (t < 80) return Color.FromArgb(255, 200, 70);
        return Color.FromArgb(255, 110, 100);
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        GraphicsPath p = new GraphicsPath();
        float d = radius * 2f;
        if (d > r.Width) d = r.Width;
        if (d > r.Height) d = r.Height;
        p.AddArc(r.X, r.Y, d, d, 180f, 90f);
        p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
        p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
        p.CloseFigure();
        return p;
    }

    Icon BuildIcon(string cpuText, string gpuText, int mode)
    {
        Bitmap bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            RectangleF panel = new RectangleF(0.5f, 0.5f, 31f, 31f);
            using (GraphicsPath path = RoundedRect(panel, 7f))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(240, 22, 22, 26)))
            using (Pen border = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            using (Font f = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;

                if (mode == 0)
                {
                    RectangleF top = new RectangleF(panel.X, 2.5f, panel.Width, 14f);
                    RectangleF bottom = new RectangleF(panel.X, 17.5f, panel.Width, 14f);
                    using (SolidBrush b1 = new SolidBrush(TempColor(cpuTemp)))
                        g.DrawString(cpuText, f, b1, top, sf);
                    using (SolidBrush b2 = new SolidBrush(TempColor(gpuTemp)))
                        g.DrawString(gpuText, f, b2, bottom, sf);
                }
                else if (mode == 1)
                {
                    using (SolidBrush b1 = new SolidBrush(TempColor(cpuTemp)))
                        g.DrawString(cpuText, f, b1, panel, sf);
                }
                else
                {
                    using (SolidBrush b2 = new SolidBrush(TempColor(gpuTemp)))
                        g.DrawString(gpuText, f, b2, panel, sf);
                }
            }
        }
        string dump = dumpPathStatic;
        if (!string.IsNullOrEmpty(dump))
        {
            try { bmp.Save(dump, System.Drawing.Imaging.ImageFormat.Png); } catch { }
        }
        IntPtr h = bmp.GetHicon();
        Icon result = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        bmp.Dispose();
        return result;
    }

    void Log(string msg)
    {
        try
        {
            File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg + Environment.NewLine);
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (tray != null) tray.Visible = false;
        if (computer != null) { try { computer.Close(); } catch { } }
        base.OnFormClosing(e);
    }
}
