using Microsoft.Identity.Client;
using Microsoft.InformationProtection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ClipboardMonitorPlugin.Code.MIP
{
    public class BackgroundAuthDelegate : IAuthDelegate
    {
        private readonly IConfidentialClientApplication _app;

        public BackgroundAuthDelegate(string clientId, string tenantId, string clientSecret)
        {
            // MSAL keep token in RAM (In-memory cache)
            _app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .Build();
        }

        public string AcquireToken(Identity identity, string authority, string resource, string claims)
        {
            return AcquireTokenAsync(authority, resource, claims).GetAwaiter().GetResult();
        }

        private async Task<string> AcquireTokenAsync(string authority, string resource, string claims)
        {
            string resourceUrl = resource.TrimEnd('/');
            string[] scopes = new[] { $"{resourceUrl}/.default" };

            try
            {
                // Try to get the token from the RAM cache first, if expired it will automatically refresh
                var result = await _app.AcquireTokenForClient(scopes).ExecuteAsync();
                return result.AccessToken;
            }
            catch (Exception ex)
            {
                PluginContext.Log("BackgroundAuth", $"Error acquiring background token: {ex.Message}");
                return null;
            }
        }
    }
}
