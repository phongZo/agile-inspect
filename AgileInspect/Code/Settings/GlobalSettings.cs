namespace AgileInspect.Code.Settings
{
    public static class GlobalSettings
    {
        // RFC 2822 –  Tue, 10 Jun 2025 12:34:56 ZZ
        public const string FORMAT_RFC2822 = "ddd, dd MMM yyyy HH:mm:ss ZZ";

        // ISO 8601 –   2025-06-10T12:34:56Z
        public const string FORMAT_ISO8601 = "yyyy-MM-ddTHH:mm:ssZ";

        /// <summary>HMAC key segment for signed requests (same as license secret unless you split them).</summary>
        public const string KeyStringLicense = "AgileN@W";

        /// <summary>AES key material for encrypting query/body; must match server.</summary>
        public const string KeyString = "zSPk37gZRhuA8ynCkqv3JES9dmkaC3Ol";
    }
}
