using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ClipboardMonitorPlugin.Code.MIP
{
    public class DetailedLabelInfo
    {
        public string LabelId { get; set; }
        public string LabelName { get; set; }
        public string Owner { get; set; }
        public string TenantId { get; set; }
        public string DetectionMethod { get; set; }
        public bool IsProtected { get; set; }
        public bool HasData => !string.IsNullOrEmpty(LabelId) || IsProtected;
    }

    public static class MetadataScanner
    {
        public static DetailedLabelInfo GetDetailedInfo(string filePath, IEnumerable<string> validIds = null)
        {
            var normalizedValidIds = validIds?.Select(id => id.Trim('{', '}').ToLower()).ToList();
            
            // 1. Try ZIP-based extraction (For modern Office files only)
            var info = ScanZipMetadata(filePath, normalizedValidIds);
            
            bool foundInZip = info.HasData && (normalizedValidIds == null || normalizedValidIds.Contains(info.LabelId.ToLower()));

            if (!foundInZip)
            {
                // 2. Fallback to Fast Binary scanning (For PDF, Images, TXT, and all other formats)
                var binaryInfo = ScanBinaryMetadata(filePath, normalizedValidIds);
                bool foundInBinary = binaryInfo.HasData && (normalizedValidIds == null || normalizedValidIds.Contains(binaryInfo.LabelId.ToLower()));
                if (foundInBinary || (!info.HasData && binaryInfo.HasData))
                {
                    info = binaryInfo;
                }
            }
            return info;
        }

        private static DetailedLabelInfo ScanZipMetadata(string filePath, List<string> normalizedValidIds)
        {
            var result = new DetailedLabelInfo();
            try
            {
                if (!File.Exists(filePath)) return result;
                string ext = Path.GetExtension(filePath).ToLower();
                
                bool isOfficeExt = (ext == ".docx" || ext == ".xlsx" || ext == ".pptx" || ext == ".docm" || ext == ".xlsm" || ext == ".pptm");
                if (!isOfficeExt) return result;

                try 
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        result.DetectionMethod = MIPHelper.METHOD_OFFLINE_UNZIP;

                        var labelInfoEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("labelInfo.xml", StringComparison.OrdinalIgnoreCase));
                        if (labelInfoEntry != null)
                        {
                            using (Stream stream = labelInfoEntry.Open())
                            {
                                XDocument xml = XDocument.Load(stream);
                                XNamespace ns = "http://schemas.microsoft.com/office/2020/mipLabelMetadata";
                                var labelElement = xml.Descendants(ns + "label").FirstOrDefault();
                                if (labelElement != null)
                                {
                                    result.LabelId = labelElement.Attribute("id")?.Value?.Trim('{', '}');
                                    result.LabelName = labelElement.Attribute("name")?.Value;
                                    result.TenantId = labelElement.Attribute("siteId")?.Value?.Trim('{', '}');
                                    if (normalizedValidIds != null && normalizedValidIds.Contains(result.LabelId.ToLower())) return result;
                                }
                            }
                        }

                        var customPropsEntry = archive.GetEntry("docProps/custom.xml");
                        if (customPropsEntry != null)
                        {
                            using (Stream stream = customPropsEntry.Open())
                            {
                                XDocument xml = XDocument.Load(stream);
                                XNamespace ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
                                foreach (var prop in xml.Root.Elements(ns + "property"))
                                {
                                    string name = prop.Attribute("name")?.Value;
                                    string value = prop.Value;
                                    if (name != null && name.StartsWith("MSIP_Label_", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string guid = "";
                                        if (name.EndsWith("_Enabled", StringComparison.OrdinalIgnoreCase)) guid = name.Substring(11, name.Length - 19);
                                        else if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase)) guid = name.Substring(11, name.Length - 16);
                                        
                                        if (normalizedValidIds != null && normalizedValidIds.Contains(guid.ToLower())) {
                                            result.LabelId = guid;
                                            if (name.EndsWith("_Name", StringComparison.OrdinalIgnoreCase)) result.LabelName = value;
                                            return result;
                                        }
                                        if (string.IsNullOrEmpty(result.LabelId)) result.LabelId = guid;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    // Not a ZIP but has Office extension. Check for OLE signature (RMS protection)
                    byte[] signature = new byte[8];
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.Read(signature, 0, 8);
                    }
                    if (signature[0] == 0xD0 && signature[1] == 0xCF && signature[2] == 0x11 && signature[3] == 0xE0)
                    {
                        result.IsProtected = true;
                        result.DetectionMethod = "OFFLINE_OLE_PROTECTED";
                    }
                }
            } catch { }
            return result;
        }

        private static DetailedLabelInfo ScanBinaryMetadata(string filePath, List<string> normalizedValidIds)
        {
            var result = new DetailedLabelInfo();
            try
            {
                if (!File.Exists(filePath)) return result;
                result.DetectionMethod = MIPHelper.METHOD_OFFLINE_BINARY;

                byte[] data;
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long size = fs.Length;
                    // Safely scan only first and last 512KB for maximum performance
                    int scanSize = 512 * 1024;
                    if (size <= scanSize * 2) { 
                        data = new byte[size]; 
                        fs.Read(data, 0, (int)size); 
                    }
                    else { 
                        data = new byte[scanSize * 2]; 
                        fs.Read(data, 0, scanSize); 
                        fs.Seek(-scanSize, SeekOrigin.End); 
                        fs.Read(data, scanSize, scanSize); 
                    }
                }
                
                string ascii = System.Text.Encoding.ASCII.GetString(data);
                string unicode = System.Text.Encoding.Unicode.GetString(data);

                // Detection for PDF encryption
                if (ascii.Contains("/Encrypt") || unicode.Contains("/Encrypt"))
                {
                    result.IsProtected = true;
                }

                result.LabelId = ExtractTag(ascii, "name=\"ID\">", "</") ?? ExtractTag(unicode, "name=\"ID\">", "</") ?? FindGuidFromRaw(data);
                if (result.LabelId != null) result.LabelId = result.LabelId.Trim('{', '}');

                if (normalizedValidIds != null && (result.LabelId == null || !normalizedValidIds.Contains(result.LabelId.ToLower())))
                {
                    foreach (var id in normalizedValidIds)
                        if (ascii.Contains(id) || unicode.Contains(id)) { result.LabelId = id; break; }
                }

                if (result.HasData)
                {
                    result.Owner = ExtractTag(ascii, "<OWNER><NAME>", "</") ?? ExtractTag(unicode, "<OWNER><NAME>", "</");
                    
                    string nameTag = "LCID 1033:NAME ";
                    string nameBlock = ExtractTag(ascii, nameTag, ";") ?? ExtractTag(unicode, nameTag, ";");
                    
                    if (nameBlock != null) 
                    {
                        if (nameBlock.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                        {
                            int descIdx = nameBlock.IndexOf("DESCRIPTION", StringComparison.OrdinalIgnoreCase);
                            nameBlock = nameBlock.Substring(0, descIdx).Trim();
                        }
                        result.LabelName = nameBlock.Split(':').Last().Trim().Trim('\"');
                    }
                }
            } catch { }
            return result;
        }

        private static string ExtractTag(string content, string start, string end)
        {
            int s = content.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (s == -1) return null;
            s += start.Length;
            int e = content.IndexOf(end, s, StringComparison.OrdinalIgnoreCase);
            if (e == -1) return null;
            return content.Substring(s, e - s).Trim();
        }

        private static string FindGuidFromRaw(byte[] data)
        {
            string ascii = System.Text.Encoding.ASCII.GetString(data);
            int idx = ascii.IndexOf("MSIP_Label_", StringComparison.OrdinalIgnoreCase);
            if (idx != -1 && idx + 47 <= ascii.Length)
            {
                string guid = ascii.Substring(idx + 11, 36);
                if (guid.Contains("-")) return guid;
            }
            return null;
        }
    }
}
