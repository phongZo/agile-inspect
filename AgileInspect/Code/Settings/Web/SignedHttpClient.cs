using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgileInspect.Code.Settings.Web
{
    public class SignedHttpClient
    {
        public static SignedHttpClient Instance { get; } = new SignedHttpClient();

        private readonly HttpClient _httpClient;

        private SignedHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            _httpClient = new HttpClient(handler);
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

            // try log
            TryLogRequest(uri, content);

            // 5. Send request
            return await _httpClient.SendAsync(request);
        }
        private async void TryLogRequest(Uri uri, HttpContent content = null)
        {
            try
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var logDetails = new List<string>();

                foreach (string key in queryParams.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    string value = queryParams[key] ?? "null";
                    AddMaskedOrRaw(key, key, value, logDetails);
                }

                // Body
                if (content != null)
                {
                    var json = await content.ReadAsStringAsync();

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            var bodyDetails = new List<string>();
                            FlattenJsonObject(root, "", bodyDetails);
                            logDetails.AddRange(bodyDetails);
                        }
                        else
                        {
                            logDetails.Add("body=<non-object>");
                        }
                    }
                    catch (JsonException)
                    {
                        logDetails.Add("body=<invalid-json>");
                    }
                }

                string logMessage = $"[SignedHttpClient]: {uri.Scheme}://{uri.Host} " +
                    $"({string.Join(", ", logDetails)})";

                DebugLog.WriteLine(logMessage);
            }
            catch (Exception logEx)
            {
                DebugLog.WriteLine($"[SignedHttpClient] Failed to generate log: {logEx.Message}");
            }
        }

        private void FlattenJsonObject(JsonElement element, string prefix, List<string> output)
        {
            foreach (var prop in element.EnumerateObject())
            {
                string key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.Object:
                        FlattenJsonObject(prop.Value, key, output);
                        break;

                    case JsonValueKind.Array:
                        output.Add($"{key}=[array]");
                        break;

                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                        AddMaskedOrRaw(prop.Name, key, prop.Value.ToString(), output);
                        break;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        string boolStr = prop.Value.GetBoolean().ToString().ToLower();
                        AddMaskedOrRaw(prop.Name, key, boolStr, output);
                        break;

                    case JsonValueKind.Null:
                        output.Add($"{key}=null");
                        break;

                    default:
                        output.Add($"{key}=<unknown>");
                        break;
                }
            }
        }

        private void AddMaskedOrRaw(string propName, string fullKey, string value, List<string> output)
        {
            if (propName.Equals("customerId", StringComparison.OrdinalIgnoreCase))
            {
                string masked = !string.IsNullOrWhiteSpace(value) && value.Length > 6
                    ? "xxx" + value[^6..]
                    : value ?? "null";

                output.Add($"{fullKey}={masked}");
            }
            else
            {
                output.Add($"{fullKey}={value}");
            }
        }

    }
}
