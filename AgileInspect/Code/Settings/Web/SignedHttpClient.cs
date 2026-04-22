using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgileInspect.Code.Settings;

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

        public async Task<HttpResponseMessage> SendSignedRequestAsync(HttpMethod method, string url, string plainBodyJson = null, TimeSpan? timeout = null)
        {
            var uri = new Uri(url);
            bool debug = string.Equals(Environment.GetEnvironmentVariable("AGI_HTTP_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);

            // 1. Extract query string from URL, remove leading '?'
            string plainQueryString = uri.Query;
            if (!string.IsNullOrEmpty(plainQueryString) && plainQueryString.StartsWith("?", StringComparison.Ordinal))
                plainQueryString = plainQueryString.Substring(1);
            else
                plainQueryString = "";

            // 2. Load clientId, secretKey for HMAC and secretAESKey for AES encryption from settings
            string clientId = StoreCfgJson.Instance.customerID;
            string secretKey = GlobalSettings.KeyStringLicense;
            string secretAESKey = GlobalSettings.KeyString;

            // 3. Ensure body is not null, use empty string if null
            plainBodyJson ??= "";

            // 4. Encrypt body and query parameters if they exist
            string encryptedBody = plainBodyJson.Length > 0 ? Aes.Encrypt(plainBodyJson, secretAESKey) : "";
            string encryptedParam = plainQueryString.Length > 0 ? Aes.Encrypt(plainQueryString, secretAESKey) : "";

            // 5. Compute SHA256 hash of encrypted body and param
            string bodyHash = encryptedBody.Length > 0
                ? ComputeSha256Hash(Convert.FromBase64String(encryptedBody))
                : ComputeSha256Hash(Array.Empty<byte>());

            string paramHash = encryptedParam.Length > 0
                ? ComputeSha256Hash(Convert.FromBase64String(encryptedParam))
                : ComputeSha256Hash(Array.Empty<byte>());

            // 6. Generate timestamps in RFC2822 and ISO8601 formats
            var now = DateTimeOffset.UtcNow;
            string timestampRfc2822 = now.ToString(GlobalSettings.FORMAT_RFC2822, CultureInfo.InvariantCulture);
            string timestampIso8601 = now.ToString(GlobalSettings.FORMAT_ISO8601, CultureInfo.InvariantCulture);

            // 7. Build the canonical string to sign
            // Format: HTTP_METHOD#REQUEST_PATH#TIMESTAMP_RFC2822#BODY_HASH#PARAM_HASH
            string canonicalString = $"{method.Method.ToUpperInvariant()}#{uri.AbsolutePath}#{timestampRfc2822}#{bodyHash}#{paramHash}";

            // 8. Generate HMAC SHA256 signature using key = secretKey + clientId
            string key = secretKey + clientId;
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] messageBytes = Encoding.UTF8.GetBytes(canonicalString);

            string signature;
            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(messageBytes);
                signature = Convert.ToBase64String(hashBytes);
            }

            // 9. Construct final URL by replacing original query with encrypted `data` param
            string finalUrl = encryptedParam.Length > 0
                ? $"{uri.GetLeftPart(UriPartial.Path)}?data={Uri.EscapeDataString(encryptedParam)}"
                : uri.GetLeftPart(UriPartial.Path);

            if (debug)
            {
                try
                {
                    DebugLog.WriteLine("[SignedHttpClient] url=" + url);
                    DebugLog.WriteLine("[SignedHttpClient] method=" + method.Method.ToUpperInvariant());
                    DebugLog.WriteLine("[SignedHttpClient] path=" + uri.AbsolutePath);
                    DebugLog.WriteLine("[SignedHttpClient] plainQuery=" + plainQueryString);
                    DebugLog.WriteLine("[SignedHttpClient] xDate=" + timestampIso8601);
                    DebugLog.WriteLine("[SignedHttpClient] rfc2822=" + timestampRfc2822);
                    DebugLog.WriteLine("[SignedHttpClient] bodyHash=" + bodyHash);
                    DebugLog.WriteLine("[SignedHttpClient] paramHash=" + paramHash);
                    DebugLog.WriteLine("[SignedHttpClient] canonical=" + canonicalString);
                    DebugLog.WriteLine("[SignedHttpClient] finalUrl=" + finalUrl);
                    DebugLog.WriteLine("[SignedHttpClient] authClientId=" + clientId);
                    DebugLog.WriteLine("[SignedHttpClient] signature=" + signature);
                    DebugLog.WriteLine("[SignedHttpClient] encParam.len=" + encryptedParam.Length);
                    DebugLog.WriteLine("[SignedHttpClient] encBody.len=" + encryptedBody.Length);
                }
                catch { /* ignore */ }
            }

            // 10. Create HttpRequestMessage with method and final URL
            var request = new HttpRequestMessage(method, finalUrl);

            // 11. Add encrypted body content if method supports body and body is present
            if (encryptedBody.Length > 0 && method != HttpMethod.Get && method != HttpMethod.Head)
            {
                request.Content = new StringContent(encryptedBody, Encoding.UTF8, "text/plain");
            }

            // 12. Add required headers: timestamp, authorization and user-agent
            request.Headers.Add("X-Date", timestampIso8601);
            request.Headers.Authorization = new AuthenticationHeaderValue("AgileInspect", $"Signature {clientId}:{signature}");
            var productVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "0";
            request.Headers.UserAgent.ParseAdd($"AgileMark Agent {productVersion}");

            // 13. Send the HTTP request with optional timeout
            if (timeout.HasValue)
            {
                using var cts = new CancellationTokenSource(timeout.Value);
                return await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            }

            return await _httpClient.SendAsync(request).ConfigureAwait(false);
        }

        private static string ComputeSha256Hash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
