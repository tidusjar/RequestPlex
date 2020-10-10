using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using Ombi.Core.Settings;
using Ombi.Settings.Settings.Models;
using Ombi.Store.Entities;

namespace Ombi.Core.Authentication
{
    /// <summary>
    /// Ldap Authentication Provider
    /// </summary>
    public class LdapUserManager : ILdapUserManager
    {
        public LdapUserManager(ILogger<LdapUserManager> logger, ISettingsService<LdapSettings> ldapSettings)
        {
            _ldapSettingsService = ldapSettings;
            _logger = logger;
        }

        private readonly ISettingsService<LdapSettings> _ldapSettingsService;
        private readonly ILogger<LdapUserManager> _logger;

        public async Task<LdapSettings> GetSettings()
        {
            return await _ldapSettingsService.GetSettingsAsync();
        }

        public async Task<OmbiUser> LdapEntryToOmbiUser(LdapEntry entry)
        {
            var settings = await GetSettings();
            var userName = GetLdapAttribute(entry, settings.UsernameAttribute).StringValue;

            return new OmbiUser
            {
                UserType = UserType.LdapUser,
                ProviderUserId = entry.Dn,
                UserName = userName
            };
        }

        private LdapAttribute GetLdapAttribute(LdapEntry userEntry, string attr)
        {
            try
            {
                return userEntry.GetAttribute(attr);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<LdapConnection> BindLdapConnection(string username, string password)
        {
            var settings = await GetSettings();

            var ldapClient = new LdapConnection { SecureSocketLayer = settings.UseSsl };
            try
            {
                if (settings.SkipSslVerify)
                {
                    ldapClient.UserDefinedServerCertValidationDelegate += LdapClient_UserDefinedServerCertValidationDelegate;
                }

                ldapClient.Connect(settings.Hostname, settings.Port);
                if (settings.UseStartTls)
                {
                    ldapClient.StartTls();
                }

                ldapClient.Bind(username, password);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to Connect or Bind to server");
                throw e;
            }
            finally
            {
                ldapClient.UserDefinedServerCertValidationDelegate -= LdapClient_UserDefinedServerCertValidationDelegate;
            }

            if (!ldapClient.Bound)
            {
                ldapClient.Dispose();
                return null;
            }

            return ldapClient;
        }

        private async Task<ILdapSearchResults> SearchLdapUsers(LdapConnection ldapClient)
        {
            var settings = await GetSettings();

            string[] searchAttributes = { settings.UsernameAttribute };

            _logger.LogDebug("Search: {1} {2} @ {3}", settings.BaseDn, settings.SearchFilter, settings.Hostname);
            return ldapClient.Search(settings.BaseDn, LdapConnection.ScopeSub, settings.SearchFilter, searchAttributes, false);
        }

        public async Task<ILdapSearchResults> GetLdapUsers()
        {
            var settings = await GetSettings();

            using var ldapClient = await BindLdapConnection(settings.BindUserDn, settings.BindUserPassword);

            return await SearchLdapUsers(ldapClient);
        }

        public async Task<LdapEntry> LocateLdapUser(string username)
        {
            var settings = await GetSettings();

            using var ldapClient = await BindLdapConnection(settings.BindUserDn, settings.BindUserPassword);
            var ldapUsers = await SearchLdapUsers(ldapClient);
            if (ldapUsers == null)
            {
                return null;
            }

            while (ldapUsers.HasMore())
            {
                var currentUser = ldapUsers.Next();
                var foundUsername = GetLdapAttribute(currentUser, settings.UsernameAttribute)?.StringValue;
                if (foundUsername == username)
                {
                    return currentUser;
                }
            }

            return null;
        }

        /// <summary>
        /// Authenticate user against the ldap server.
        /// </summary>
        /// <param name="user">Username to authenticate.</param>
        /// <param name="password">Password to authenticate.</param>
        public async Task<bool> Authenticate(OmbiUser user, string password)
        {
            var ldapUser = await LocateLdapUser(user.UserName);

            if (ldapUser == null)
            {
                return false;
            }

            try
            {
                using var ldapClient = await BindLdapConnection(ldapUser.Dn, password);
                return (bool)ldapClient?.Bound;
            } catch (Exception)
            {
                return false;
            }
        }

        private static bool LdapClient_UserDefinedServerCertValidationDelegate(
            object sender,
            System.Security.Cryptography.X509Certificates.X509Certificate certificate,
            System.Security.Cryptography.X509Certificates.X509Chain chain,
            System.Net.Security.SslPolicyErrors sslPolicyErrors)
            => true;
    }
}