using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BpmMeasurement = (System.DateTimeOffset, int)?;

namespace TrayHeartRate
{

    public class Root
    {
        [JsonPropertyName("data")]
        public required List<Data> Data { get; set; }
    }

    public class Data
    {
        [JsonPropertyName("bpm")]
        public int Bpm { get; set; }

        [JsonPropertyName("source")]
        public required string Source { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }
    }

    internal class OuraRingClient
    {
        private readonly HttpClient client = new();

        public OuraRingClient(string personalToken) =>
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {personalToken}");

        public async Task<BpmMeasurement> GetHeartRateAsync(DateTimeOffset start)
        {
            var url = new Uri($"https://api.ouraring.com/v2/usercollection/heartrate?start_datetime={start:yyyy-MM-ddThh:mm:ss}");

            var root = await client.GetFromJsonAsync<Root>(url).ConfigureAwait(false);

            var result = root?.Data.Select<Data, BpmMeasurement>(r => (r.Timestamp, r.Bpm)).LastOrDefault();

            return result;
        }
    }
}
