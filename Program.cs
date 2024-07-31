using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using OuraRing;
using System.Drawing.Text;
using System.Timers;
using Timers = System.Timers;

namespace TrayHeartRate
{
    internal class Program
    {
        static readonly IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        static readonly NotifyIcon trayIcon = new()
        {
            BalloonTipIcon = ToolTipIcon.Info,
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };

        static readonly Font font = new("Arial Narrow", 9);
        static readonly Font fontNarrow = new("Arial Narrow", 7);
        static readonly SolidBrush brush = new(Color.Black);
        static readonly SolidBrush brushAlert = new(Color.DarkRed);
        static readonly SolidBrush backBrush = new(Color.White);

        static OuraRingClient? OuraRingClient;

        static DateTimeOffset lastHeartRateTime = DateTimeOffset.MinValue;

        /* replaced with .Dispose()
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);*/

        static void DrawHeartRate(HeartRate measurement)
        {
            const int iconSize = 16; // maximum possible icon size
            var bmp = new Bitmap(iconSize, iconSize);
            using var img = Graphics.FromImage(bmp);

            img.FillRectangle(backBrush, 0, 0, iconSize, iconSize);

            img.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int heartRate = measurement.Bpm;

            bool alert = IsBpmOverThreshold(heartRate);

            img.DrawString(
                $"{heartRate}",
                heartRate > 99 ? fontNarrow : font,
                alert ? brushAlert : brush,
                new PointF(0.0F, 0.0F));

            var icon = Icon.FromHandle(bmp.GetHicon());

            // older alternative way to dispose icon, might work better
            /*if (trayIcon.Icon != null)
            {
                DestroyIcon(trayIcon.Icon.Handle);
            }*/

            // new untested way to dispose icon
            trayIcon.Icon?.Dispose();

            trayIcon.Icon = icon;

            trayIcon.Text = $"{measurement.Source} heart rate at {DateTimeToTimeString(measurement.Timestamp)}: {heartRate}";
        }

        private const string BpmAlertThresholdConfigKey = "bpm_alert_threshold";

        static async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            await RereshIcon();
        }

        static async Task RereshIcon()
        {
            var start = lastHeartRateTime == DateTimeOffset.MinValue ? 
                DateTimeOffset.UtcNow.AddMinutes(-120) :
                lastHeartRateTime.AddSeconds(1);

            HeartRate? measurement = await OuraRingClient!.GetHeartRateAsync(start);

            if (measurement == null)
            {
                return;
            }

            int bpm = measurement.Bpm;
            lastHeartRateTime = measurement.Timestamp;
            DrawHeartRate(measurement);

            if (IsBpmOverThreshold(bpm))
            {
                trayIcon.ShowBalloonTip(10000, $"{measurement.Source} heart rate at {DateTimeToTimeString(lastHeartRateTime)}:", $"{bpm}", ToolTipIcon.Warning);
            }
        }

        static string DateTimeToTimeString(DateTimeOffset dateTime) => dateTime.ToLocalTime().ToString("H:mm:ss");

        static bool IsBpmOverThreshold(int bpm) => bpm > int.Parse(configuration[BpmAlertThresholdConfigKey]);

        static void Main()
        {
            string oura_personal_token = configuration["oura_personal_token"];

            if (string.IsNullOrEmpty(oura_personal_token))
            {
                var userSecretsPath = PathHelper.GetSecretsPathFromSecretsId(oura_personal_token);
                Console.WriteLine($"Set oura_personal_token in {userSecretsPath} file.");
                return;
            }

            var tmp = configuration[BpmAlertThresholdConfigKey];
            if (string.IsNullOrEmpty(tmp))
            {
                throw new InvalidOperationException($"Set {BpmAlertThresholdConfigKey} in appsettings.json file.");
            }

            OuraRingClient = new OuraRingClient(oura_personal_token);

            trayIcon.ContextMenuStrip!.Items.Add("Quit", null, (s, e) => Application.Exit());

            var t = RereshIcon();

            var refreshInterval = TimeSpan.FromMinutes(1);
            var timer = new Timers.Timer(refreshInterval);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            Application.Run();

            trayIcon.Dispose();
        }
    }
}
