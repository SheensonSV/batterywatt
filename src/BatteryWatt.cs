using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("BatteryWatt")]
[assembly: AssemblyDescription("Live battery power draw in the Windows tray")]
[assembly: AssemblyProduct("BatteryWatt")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace BatteryWatt
{
    internal static class Program
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "BatteryWatt";
        private const string MutexName = "BatteryWatt_Tray_SingleInstance";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        private static NotifyIcon tray;
        private static System.Windows.Forms.Timer timer;
        private static Icon currentIcon;

        private static bool hasBattery;
        private static bool onAc;
        private static bool charging;
        private static double watts;
        private static double remainingWh;
        private static string lastError = "";
        private static DateTime lastRead = DateTime.MinValue;

        [STAThread]
        private static void Main(string[] args)
        {
            bool once = false;
            string autostart = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--once", StringComparison.OrdinalIgnoreCase)) once = true;
                if (string.Equals(args[i], "--autostart", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    autostart = args[i + 1];
                    i++;
                }
            }

            if (autostart != null)
            {
                bool enable = string.Equals(autostart, "on", StringComparison.OrdinalIgnoreCase);
                SetAutostart(enable);
                if (!once) return;
            }

            ReadState();

            if (once)
            {
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss}; AC={1}; charging={2}; watts={3:0.00}; remainingWh={4:0.00}; autostart={5}; error={6}",
                    DateTime.Now, onAc, charging, watts, remainingWh, IsAutostartEnabled(), lastError);
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "once.txt"), line);
                return;
            }

            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                if (!created) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("Подробнее...", null, new EventHandler(ShowDetails));

                ToolStripMenuItem autoItem = new ToolStripMenuItem("Запускать при входе");
                autoItem.Checked = IsAutostartEnabled();
                autoItem.CheckOnClick = true;
                autoItem.CheckedChanged += delegate(object s, EventArgs e)
                {
                    SetAutostart(autoItem.Checked);
                };
                menu.Items.Add(autoItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Выход", null, new EventHandler(ExitApp));

                tray = new NotifyIcon();
                tray.ContextMenuStrip = menu;
                tray.Visible = true;
                tray.Text = "BatteryWatt";
                tray.MouseClick += delegate(object s, MouseEventArgs e)
                {
                    if (e.Button == MouseButtons.Left) ShowDetails(s, EventArgs.Empty);
                };

                timer = new System.Windows.Forms.Timer();
                timer.Interval = 3000;
                timer.Tick += delegate(object s, EventArgs e)
                {
                    ReadState();
                    UpdateTray();
                };
                timer.Start();

                UpdateTray();
                Application.Run();
            }
        }

        private static bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(RunValueName);
                    if (value == null) return false;
                    string stored = value.ToString().Trim().Trim('"');
                    return string.Equals(stored, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void SetAutostart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    else if (key.GetValue(RunValueName) != null) key.DeleteValue(RunValueName, false);
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        private static void ExitApp(object sender, EventArgs e)
        {
            if (timer != null) timer.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            Application.Exit();
        }

        private static void ShowDetails(object sender, EventArgs e)
        {
            string state;
            if (!hasBattery) state = "Батарея не найдена";
            else if (onAc) state = charging ? "От сети, идёт зарядка" : "От сети, заряжена";
            else state = "От батареи";

            string runtime = "—";
            if (!onAc && watts > 0.2 && remainingWh > 0)
            {
                TimeSpan t = TimeSpan.FromHours(remainingWh / watts);
                runtime = string.Format(CultureInfo.InvariantCulture, "{0} ч {1:00} мин", (int)t.TotalHours, t.Minutes);
            }

            string msg = string.Format(CultureInfo.InvariantCulture,
                "Состояние: {0}\r\nПотребление: {1:0.00} Вт\r\nОстаток: {2:0.0} Wh\r\nРасчётное время: {3}\r\n" +
                "Автозапуск: {4}\r\n\r\n" +
                "Обновление: каждые 3 с.\r\nКонтроллер батареи уточняет цифру примерно раз в 10–30 с,\r\n" +
                "поэтому значение меняется ступенями — это нормально.\r\n\r\n" +
                "Последнее чтение: {5:HH:mm:ss}\r\n{6}",
                state, watts, remainingWh, runtime,
                IsAutostartEnabled() ? "включён" : "выключен",
                lastRead, lastError);

            MessageBox.Show(msg, "BatteryWatt", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ReadState()
        {
            hasBattery = false;
            onAc = false;
            charging = false;
            watts = 0;
            remainingWh = 0;
            lastError = "";

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\wmi",
                    "SELECT PowerOnline, Charging, Discharging, DischargeRate, ChargeRate, RemainingCapacity FROM BatteryStatus"))
                {
                    using (ManagementObjectCollection results = searcher.Get())
                    {
                        foreach (ManagementBaseObject item in results)
                        {
                            hasBattery = true;
                            onAc = ToBool(item["PowerOnline"]);
                            charging = ToBool(item["Charging"]);
                            uint discharge = ToUInt(item["DischargeRate"]);
                            uint charge = ToUInt(item["ChargeRate"]);
                            uint remaining = ToUInt(item["RemainingCapacity"]);
                            watts = (onAc ? charge : discharge) / 1000.0;
                            remainingWh = remaining / 1000.0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            lastRead = DateTime.Now;
        }

        private static bool ToBool(object value)
        {
            if (value == null) return false;
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return false; }
        }

        private static uint ToUInt(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToUInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private static void UpdateTray()
        {
            string iconText;
            Color color;
            string tip;

            if (!hasBattery)
            {
                iconText = "?";
                color = Color.Gray;
                tip = "BatteryWatt: батарея не найдена";
            }
            else if (onAc)
            {
                iconText = charging ? "+" + watts.ToString("0", CultureInfo.InvariantCulture) : "AC";
                color = Color.FromArgb(130, 195, 255);
                tip = charging
                    ? string.Format(CultureInfo.InvariantCulture, "Зарядка {0:0.0} Вт · {1:0.0} Wh", watts, remainingWh)
                    : string.Format(CultureInfo.InvariantCulture, "От сети · {0:0.0} Wh", remainingWh);
            }
            else
            {
                iconText = watts >= 10
                    ? watts.ToString("0", CultureInfo.InvariantCulture)
                    : watts.ToString("0.0", CultureInfo.InvariantCulture);

                if (watts <= 9) color = Color.FromArgb(115, 225, 115);
                else if (watts <= 13) color = Color.FromArgb(255, 210, 70);
                else if (watts <= 18) color = Color.FromArgb(255, 150, 60);
                else color = Color.FromArgb(255, 85, 85);

                string runtime = "";
                if (watts > 0.2 && remainingWh > 0)
                {
                    TimeSpan t = TimeSpan.FromHours(remainingWh / watts);
                    runtime = t.TotalHours >= 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0} ч {1} мин", (int)t.TotalHours, t.Minutes)
                        : string.Format(CultureInfo.InvariantCulture, "{0} мин", t.Minutes);
                }

                tip = string.Format(CultureInfo.InvariantCulture, "{0:0.0} Вт · {1} · {2:0.0} Wh", watts, runtime, remainingWh);
            }

            if (tip.Length > 63) tip = tip.Substring(0, 63);
            tray.Text = tip;

            Icon next = MakeIcon(iconText, color);
            Icon previous = currentIcon;
            tray.Icon = next;
            currentIcon = next;
            if (previous != null) previous.Dispose();
        }

        private static Icon MakeIcon(string text, Color color)
        {
            Bitmap bmp = new Bitmap(32, 32);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float px = 24f;
                Font font = null;

                while (true)
                {
                    if (font != null) font.Dispose();
                    font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
                    SizeF size = g.MeasureString(text, font, new PointF(0, 0), StringFormat.GenericTypographic);
                    if ((size.Width <= 31f && size.Height <= 31f) || px <= 8f) break;
                    px -= 1f;
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddString(text, font.FontFamily, (int)FontStyle.Bold, font.Size,
                        new PointF(0, 0), StringFormat.GenericTypographic);

                    RectangleF bounds = path.GetBounds();
                    using (Matrix m = new Matrix())
                    {
                        m.Translate((32f - bounds.Width) / 2f - bounds.X, (32f - bounds.Height) / 2f - bounds.Y);
                        path.Transform(m);
                    }

                    Pen outline = new Pen(Color.FromArgb(190, 0, 0, 0), 3f);
                    outline.LineJoin = LineJoin.Round;
                    using (outline) { g.DrawPath(outline, path); }

                    using (SolidBrush brush = new SolidBrush(color)) { g.FillPath(brush, path); }
                }

                font.Dispose();
            }

            IntPtr handle = bmp.GetHicon();
            Icon temporary = Icon.FromHandle(handle);
            Icon result = (Icon)temporary.Clone();
            DestroyIcon(handle);
            bmp.Dispose();
            return result;
        }
    }
}
