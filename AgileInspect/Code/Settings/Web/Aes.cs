using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AgileInspect.Code.Settings.Web
{
    static class Aes
    {
        private static byte[] PrepareKey(string keyString)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(keyString);
            byte[] finalKey;
            if (keyBytes.Length >= 32) { finalKey = new byte[32]; Array.Copy(keyBytes, 0, finalKey, 0, 32); }
            else if (keyBytes.Length >= 24) { finalKey = new byte[24]; Array.Copy(keyBytes, 0, finalKey, 0, 24); }
            else { finalKey = new byte[16]; Array.Copy(keyBytes, 0, finalKey, 0, Math.Min(keyBytes.Length, 16)); }
            return finalKey;
        }

        public static string Encrypt(string plainText, string keyString)
        {
            byte[] cipherData;
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = PrepareKey(keyString);
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                ICryptoTransform cipher = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, cipher, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }

                cipherData = ms.ToArray();
            }

            byte[] combinedData = new byte[aes.IV.Length + cipherData.Length];
            Array.Copy(aes.IV, 0, combinedData, 0, aes.IV.Length);
            Array.Copy(cipherData, 0, combinedData, aes.IV.Length, cipherData.Length);
            return ByteArrayToString(combinedData);
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string Decrypt(string combinedString, string keyString)
        {
            string plainText;
            byte[] combinedData = StringToByteArray(combinedString);
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = PrepareKey(keyString);
                byte[] iv = new byte[aes.BlockSize / 8];
                byte[] cipherText = new byte[combinedData.Length - iv.Length];
                Array.Copy(combinedData, iv, iv.Length);
                Array.Copy(combinedData, iv.Length, cipherText, 0, cipherText.Length);
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                ICryptoTransform decipher = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decipher, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            plainText = sr.ReadToEnd();
                        }
                    }

                    return plainText;
                }
            }
        }
    }
}
