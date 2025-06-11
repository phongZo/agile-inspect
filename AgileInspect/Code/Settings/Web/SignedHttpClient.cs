using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AgileInspect.Code.Settings.Web
{
    public class SignedHttpClient
    {
        public static SignedHttpClient Instance { get; } = new SignedHttpClient();

        private readonly HttpClient _httpClient;

        private SignedHttpClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpMethod method, string url, HttpContent content = null)
        {
            string clientId = StoreCfgJson.Instance.customerID;
            string secretKey = GlobalSettings.SecretKey;

            var uri = new Uri(url);
            var request = new HttpRequestMessage(method, uri);
            if (content != null)
                request.Content = content;

            // 1. Generate timestamp
            var now = DateTimeOffset.UtcNow;
            string timestampRfc2822 = now.ToString(GlobalSettings.FORMAT_RFC2822, CultureInfo.InvariantCulture);
            string timestampIso8601 = now.ToUniversalTime().ToString(GlobalSettings.FORMAT_ISO8601);

            // 2. Create canonical string
            string canonicalString = $"{method.Method}#{uri.AbsolutePath}#{timestampRfc2822}";

            // 3. Compute HMAC SHA256
            string key = secretKey + clientId;
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] messageBytes = Encoding.UTF8.GetBytes(canonicalString);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(messageBytes);
            string signature = Convert.ToBase64String(hashBytes);

            // 4. Add headers
            request.Headers.Add("X-Date", timestampIso8601);
            request.Headers.Authorization = new AuthenticationHeaderValue("AgileInspect", $"Signature {clientId}:{signature}");

            // 5. Send request
            return await _httpClient.SendAsync(request);
        }
    }
}
