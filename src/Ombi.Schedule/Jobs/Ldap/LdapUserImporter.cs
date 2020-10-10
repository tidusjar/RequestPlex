using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ombi.Core.Authentication;
using Ombi.Core.Settings;
using Ombi.Hubs;
using Ombi.Settings.Settings.Models;
using Ombi.Store.Entities;
using Quartz;

namespace Ombi.Schedule.Jobs.Ldap
{
    public class LdapUserImporter : ILdapUserImporter
    {
        public LdapUserImporter(OmbiUserManager userManager, ILdapUserManager ldapUserManager,
            ISettingsService<LdapSettings> ldapSettings, ISettingsService<UserManagementSettings> ums, IHubContext<NotificationHub> notification)
        {
            _userManager = userManager;
            _ldapUserManager = ldapUserManager;
            _ldapSettings = ldapSettings;
            _userManagementSettings = ums;
            _notification = notification;
        }

        private readonly OmbiUserManager _userManager;
        private readonly ILdapUserManager _ldapUserManager;
        private readonly ISettingsService<LdapSettings> _ldapSettings;
        private readonly ISettingsService<UserManagementSettings> _userManagementSettings;
        private readonly IHubContext<NotificationHub> _notification;

        public async Task Execute(IJobExecutionContext job)
        {
            var userManagementSettings = await _userManagementSettings.GetSettingsAsync();
            if (!userManagementSettings.ImportLdapUsers)
            {
                return;
            }
            var settings = await _ldapSettings.GetSettingsAsync();
            if (!settings.IsEnabled)
            {
                return;
            }

            await _notification.Clients.Clients(NotificationHub.AdminConnectionIds)
                .SendAsync(NotificationHub.NotificationEvent, "LDAP User Importer Started");
            var allUsers = await _userManager.Users.Where(x => x.UserType == UserType.LdapUser).ToListAsync();

            var allLdapUsers = await _ldapUserManager.GetLdapUsers();

            while (allLdapUsers.HasMore())
            {
                var currentUser = allLdapUsers.Next();
                var existingEmbyUser = allUsers.FirstOrDefault(x => x.ProviderUserId == currentUser.Dn);
                if (existingEmbyUser != null)
                {
                    continue;
                }

                await _userManager.CreateOmbiUserFromLdapEntry(currentUser);
            }

            await _notification.Clients.Clients(NotificationHub.AdminConnectionIds)
                .SendAsync(NotificationHub.NotificationEvent, "LDAP User Importer Finished");
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _userManager?.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}