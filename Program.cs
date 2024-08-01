using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using OuraRing;
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

        static readonly Font font = new("Arial Narrow", 27, FontStyle.Bold);
        static readonly Font fontNarrow = new("Arial Narrow", 19);

        static readonly Brush textBrush = Brushes.White;
        static readonly Brush textOutlineBrush = Brushes.Black;
        static readonly Brush alertTextBrush = Brushes.Red;

        static readonly Font backFont = new("Arial", 24);
        static readonly Brush backBrush = Brushes.Pink;

        static OuraRingClient? OuraRingClient;

        static DateTimeOffset lastHeartRateTime = DateTimeOffset.MinValue;

        private const string BpmAlertThresholdConfigKey = "bpm_alert_threshold";

        static readonly NotifyIcon trayIcon = new()
        {
            BalloonTipIcon = ToolTipIcon.Info,
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip(),
        };

        static void DrawHeartRate(HeartRate? measurement)
        {
            const int iconSize = 32;
            var bmp = new Bitmap(iconSize, iconSize);
            using var img = Graphics.FromImage(bmp);

            if (measurement != null)
            {
                int heartRate = measurement.Bpm;

                bool alert = IsBpmOverThreshold(heartRate);
                DrawStringOutlined(img,
                    $"{heartRate}",
                    heartRate > 99 ? fontNarrow : font,
                    alert ? alertTextBrush : textBrush,
                    heartRate > 99 ? new PointF(-6, -2) : new PointF(-6, -4));

                trayIcon.Text = $"{measurement.Source} heart rate at {DateTimeToTimeString(measurement.Timestamp)}: {heartRate}";
            }
            else
            {
                img.DrawString("❤", backFont, backBrush, new PointF(-6, 0));
            }

            var icon = Icon.FromHandle(bmp.GetHicon());

            trayIcon.Icon?.Dispose();

            trayIcon.Icon = icon;
        }

        private static void DrawStringOutlined(Graphics img, string s, Font font, Brush brush, PointF point)
        {
            img.DrawString(s, font, textOutlineBrush, point.X - 1, point.Y - 1);
            img.DrawString(s, font, textOutlineBrush, point.X - 1, point.Y + 1);
            img.DrawString(s, font, textOutlineBrush, point.X + 1, point.Y - 1);
            img.DrawString(s, font, textOutlineBrush, point.X + 1, point.Y + 1);
            img.DrawString(s, font, brush, point);
        }

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
            DrawHeartRate(measurement);

            if (measurement == null)
            {
                return;
            }

            int bpm = measurement.Bpm;
            lastHeartRateTime = measurement.Timestamp;

            if (IsBpmOverThreshold(bpm))
            {
                trayIcon.ShowBalloonTip(10000, $"{measurement.Source} heart rate at {DateTimeToTimeString(lastHeartRateTime)}:", $"{bpm}", ToolTipIcon.Warning);
            }
        }

        static string DateTimeToTimeString(DateTimeOffset dateTime) => dateTime.ToLocalTime().ToString("H:mm:ss");

        static string BpmThresholdConfig => configuration[BpmAlertThresholdConfigKey];

        static bool IsBpmOverThreshold(int bpm) => bpm > int.Parse(BpmThresholdConfig ??
            throw new InvalidOperationException($"Set {BpmAlertThresholdConfigKey} in appsettings.json file."));

        static void Main()
        {
            string oura_personal_token = configuration["oura_personal_token"];

            if (string.IsNullOrEmpty(oura_personal_token))
            {
                var userSecretsPath = PathHelper.GetSecretsPathFromSecretsId(oura_personal_token);
                Console.WriteLine($"Set oura_personal_token in {userSecretsPath} file.");
                return;
            }

            if (string.IsNullOrEmpty(BpmThresholdConfig))
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
