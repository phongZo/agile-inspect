using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AgileInspect.Code
{
    public class Permission
    {
        #region Singleton
        private static readonly Permission _instance = new Permission();

        public static Permission Instance => _instance;

        private Permission()
        {
        }
        #endregion

        public void ResetPermissionOfFile(string fullFilename)
        {
            try
            {
                ResetPermissionFolder(Path.GetDirectoryName(fullFilename)!);

                if (!File.Exists(fullFilename)) return;

                var fileInfo = new FileInfo(fullFilename);
                var fSecurity = fileInfo.GetAccessControl();

                var authRules = fSecurity.GetAccessRules(true, true, typeof(NTAccount));

                foreach (FileSystemAccessRule rule in authRules)
                {
                    if (rule.AccessControlType == AccessControlType.Deny)
                    {
                        fSecurity.RemoveAccessRule(rule);
                    }
                }

                fSecurity.AddAccessRule(new FileSystemAccessRule(
                    Environment.UserName,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow
                ));

                fileInfo.SetAccessControl(fSecurity);
                fileInfo.Attributes = FileAttributes.Normal;
            }
            catch (Exception)
            {

            }
        }

        public void ResetPermissionRoamingDirectory()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FileChangePlugin"
            );

            ResetPermissionFolder(folder);
        }

        public void ResetPermissionFolder(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var dirInfo = new DirectoryInfo(folder);
                var dirSecurity = dirInfo.GetAccessControl();
                var authRules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));

                foreach (FileSystemAccessRule rule in authRules)
                {
                    if (rule.AccessControlType == AccessControlType.Deny)
                    {
                        dirSecurity.RemoveAccessRule(rule);
                    }
                }

                dirSecurity.AddAccessRule(new FileSystemAccessRule(
                    Environment.UserName,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow
                ));

                dirInfo.SetAccessControl(dirSecurity);
            }
            catch (Exception)
            {

            }
        }

        public bool HasReadWritePermissions(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSecurity = fileInfo.GetAccessControl();
                var currentUser = WindowsIdentity.GetCurrent();
                var rules = fileSecurity.GetAccessRules(true, true, typeof(NTAccount));

                var userGroups = GetUserGroups(currentUser);

                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.IdentityReference.Value.Equals(currentUser!.Name, StringComparison.OrdinalIgnoreCase) ||
                        userGroups.Contains(rule.IdentityReference.Value))
                    {
                        if ((rule.FileSystemRights & FileSystemRights.Read) == FileSystemRights.Read &&
                            (rule.FileSystemRights & FileSystemRights.Write) == FileSystemRights.Write)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Optional: log if needed
            }

            return false;
        }

        private List<string> GetUserGroups(WindowsIdentity currentUser)
        {
            List<string> groups = new List<string>();

            foreach (var group in currentUser.Groups!)
            {
                try
                {
                    var groupName = group.Translate(typeof(NTAccount)).ToString();
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        groups.Add(groupName);
                    }
                }
                catch
                {

                }
            }

            return groups;
        }
    }
}
