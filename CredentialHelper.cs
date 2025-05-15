using System.Security.Cryptography;
using System.Text;

namespace UnifiVideoExporter
{
    internal static class CredentialHelper
    {
        public static void SetUnencryptedCredentials(Settings SettingsObject)
        {
            if (!string.IsNullOrEmpty(SettingsObject.EncryptedPassword))
            {
                SettingsObject.Password = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(SettingsObject.EncryptedPassword), null, DataProtectionScope.CurrentUser));
            }
            if (!string.IsNullOrEmpty(SettingsObject.EncryptedUserName))
            {
                SettingsObject.UserName = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(SettingsObject.EncryptedUserName), null, DataProtectionScope.CurrentUser));
            }
        }

        public static void SetEncryptedCredentials(Settings SettingsObject)
        {
            if (!string.IsNullOrEmpty(SettingsObject.Password))
            {
                SettingsObject.EncryptedPassword = Convert.ToBase64String(ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(SettingsObject.Password), null, DataProtectionScope.CurrentUser));
            }
            if (!string.IsNullOrEmpty(SettingsObject.UserName))
            {
                SettingsObject.EncryptedUserName = Convert.ToBase64String(ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(SettingsObject.UserName), null, DataProtectionScope.CurrentUser));
            }
        }
    }
}
