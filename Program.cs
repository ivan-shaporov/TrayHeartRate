using Microsoft.Extensions.Configuration;
using System.Drawing.Text;
using System.Timers;
using Timers = System.Timers;
using BpmMeasurement = (System.DateTimeOffset At, int Bpm);

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

        static void DrawHeartRate(BpmMeasurement measurement)
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

            /*if (trayIcon.Icon != null)
            {
                DestroyIcon(trayIcon.Icon.Handle);
            }*/
            trayIcon.Icon?.Dispose();

            trayIcon.Icon = icon;

            trayIcon.Text = $"Heart rate at {DateTimeToTimeString(measurement.At)}: {heartRate}";
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

            BpmMeasurement? measurement = await OuraRingClient!.GetHeartRateAsync(start);

            if (!measurement.HasValue)
            {
                return;
            }

            int bpm = measurement.Value.Bpm;
            lastHeartRateTime = measurement.Value.At;
            DrawHeartRate(measurement.Value);

            if (IsBpmOverThreshold(bpm))
            {
                trayIcon.ShowBalloonTip(10000, $"Heart rate at {DateTimeToTimeString(lastHeartRateTime)}:", $"{bpm}", ToolTipIcon.Warning);
            }
        }

        static string DateTimeToTimeString(DateTimeOffset dateTime) => dateTime.ToLocalTime().ToString("H:mm:ss");

        static bool IsBpmOverThreshold(int bpm) => bpm > int.Parse(configuration["BpmAlertThreshold"]);

        static void Main()
        {
            /*var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();*/

            OuraRingClient = new OuraRingClient(configuration["oura_personal_token"]);

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
