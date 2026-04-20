using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using AgileInspect.Code.Settings.Web;

namespace AgileInspect.Code.Settings
{
    public class License
    {
        public static LicenseJson CurrentLicense { get; set; }
        public static bool IsLicenseValid { get; set; } = false;

        public class LicenseJson
        {
            public string CustomerId { get; set; } = "";
            public string ExpiryDate = ""; // If null or empty, never expires
            public MipCredentials JsonMip { get; set; } = new MipCredentials();
        }

        public class MipCredentials
        {
            public string MipClientId { get; set; } = "";
            public string MipTenantId { get; set; } = "";
            public string MipClientSecret { get; set; } = "";
        }

        public static int CheckLicense(string licenseHash)
        {
            if (string.IsNullOrWhiteSpace(licenseHash))
            {
                IsLicenseValid = false;
                return -2;
            }

            try
            {
                string key = StoreCfgJson.Instance.customerID + GlobalSettings.KeyStringLicense;
                string dataString = Aes.Decrypt(licenseHash, key);
                
                var jsonSerializerSettings = new JsonSerializerSettings()
                {
                    Error = (sender, errorEventArgs) =>
                    {
                        var error = errorEventArgs.ErrorContext.Error;
                        DebugLog.WriteLine($"License Decrypt JSON Error: {error.Message}");
                        errorEventArgs.ErrorContext.Handled = true;
                    },
                };

                var licenseJSON = JsonConvert.DeserializeObject<LicenseJson>(dataString, jsonSerializerSettings);
                if (licenseJSON == null) return -1;

                CurrentLicense = licenseJSON;

                // Check license expired date
                if (string.IsNullOrEmpty(licenseJSON.ExpiryDate))
                {
                    IsLicenseValid = true;
                    return 1; // No expiry date means valid forever
                }

                DateTime expiryDate;
                bool success = DateTime.TryParseExact(licenseJSON.ExpiryDate, @"yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out expiryDate);
                if (success)
                {
                    if ((DateTime.UtcNow.Date - expiryDate).TotalDays <= 0)
                    {
                        IsLicenseValid = true;
                        return 1;
                    }
                    IsLicenseValid = false;
                    return 0;
                }
            }
            catch (Exception ex)
            {
                DebugLog.WriteLine("\nFAILED TO DESERIALIZE JSON in License!!! " + $": {ex.Message}");
            }

            IsLicenseValid = false;
            return -1;
        }

        public static void CheckLicenseAgentLoad(string licenseHash)
        {
            int result = CheckLicense(licenseHash);
            if (result == 1)
            {
                DebugLog.WriteLine("License valid.");
            }
            else if (result == -2)
            {
                DebugLog.WriteLine("License settings missing");
            }
            else if (result == -1)
            {
                DebugLog.WriteLine("License Key corrupted, please contact AgileMark");
            }
            else if (result == 0)
            {
                DebugLog.WriteLine("License expired, please contact AgileMark");
            }
        }

        public static string GetSecret(string key)
        {
            if (!IsLicenseValid || CurrentLicense == null) return "";

            try
            {
                if (CurrentLicense.JsonMip != null)
                {
                    var prop = CurrentLicense.JsonMip.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                    if (prop != null) return prop.GetValue(CurrentLicense.JsonMip)?.ToString() ?? "";
                }
                
                var topProp = CurrentLicense.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (topProp != null) return topProp.GetValue(CurrentLicense)?.ToString() ?? "";
            }
            catch { }

            return "";
        }
    }
}
