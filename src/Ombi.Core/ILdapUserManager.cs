using System.Threading.Tasks;
using Novell.Directory.Ldap;
using Ombi.Settings.Settings.Models;
using Ombi.Store.Entities;

namespace Ombi.Core.Authentication
{
    public interface ILdapUserManager
    {
        Task<LdapSettings> GetSettings();

        Task<bool> Authenticate(OmbiUser user, string password);

        Task<ILdapSearchResults> GetLdapUsers();

        Task<LdapEntry> LocateLdapUser(string username);

        Task<OmbiUser> LdapEntryToOmbiUser(LdapEntry entry);
    }
}