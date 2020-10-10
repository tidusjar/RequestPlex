#region Copyright
// /************************************************************************
//    Copyright (c) 2017 Jamie Rees
//    File: OmbiUserManager.cs
//    Created By: Jamie Rees
//   
//    Permission is hereby granted, free of charge, to any person obtaining
//    a copy of this software and associated documentation files (the
//    "Software"), to deal in the Software without restriction, including
//    without limitation the rights to use, copy, modify, merge, publish,
//    distribute, sublicense, and/or sell copies of the Software, and to
//    permit persons to whom the Software is furnished to do so, subject to
//    the following conditions:
//   
//    The above copyright notice and this permission notice shall be
//    included in all copies or substantial portions of the Software.
//   
//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//  ************************************************************************/
#endregion

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Novell.Directory.Ldap;
using Ombi.Api.Emby;
using Ombi.Api.Plex;
using Ombi.Api.Plex.Models;
using Ombi.Core.Settings;
using Ombi.Core.Settings.Models.External;
using Ombi.Helpers;
using Ombi.Settings.Settings.Models;
using Ombi.Store.Entities;

namespace Ombi.Core.Authentication
{
    public class OmbiUserManager : UserManager<OmbiUser>
    {
        public OmbiUserManager(IUserStore<OmbiUser> store, IOptions<IdentityOptions> optionsAccessor,
            IPasswordHasher<OmbiUser> passwordHasher, IEnumerable<IUserValidator<OmbiUser>> userValidators,
            IEnumerable<IPasswordValidator<OmbiUser>> passwordValidators, ILookupNormalizer keyNormalizer,
            IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<OmbiUser>> logger, IPlexApi plexApi,
            IEmbyApiFactory embyApi, ISettingsService<EmbySettings> embySettings, ISettingsService<AuthenticationSettings> auth,
            ILdapUserManager ldapUserManager, ISettingsService<UserManagementSettings> userManagementSettings)
            : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
        {
            _plexApi = plexApi;
            _embyApi = embyApi;
            _embySettings = embySettings;
            _authSettings = auth;
            _ldapUserManager = ldapUserManager;
            _userManagementSettings = userManagementSettings;
        }

        private readonly IPlexApi _plexApi;
        private readonly IEmbyApiFactory _embyApi;
        private readonly ILdapUserManager _ldapUserManager;
        private readonly ISettingsService<EmbySettings> _embySettings;
        private readonly ISettingsService<AuthenticationSettings> _authSettings;
        private readonly ISettingsService<UserManagementSettings> _userManagementSettings;

        public async Task<OmbiUser> FindUser(string userName)
        {
            var user = await FindByNameAsync(userName);
            if (user != null)
            {
                return user;
            }
            user = await FindByEmailAsync(userName);
            if (user != null)
            {
                user.EmailLogin = true;
                return user;
            }

            var ldapSettings = await _ldapUserManager.GetSettings();
            if (!(ldapSettings.IsEnabled && ldapSettings.CreateUsersAtLogin))
            {
                return null;
            }

            var ldapUser = await _ldapUserManager.LocateLdapUser(userName);
            if (ldapUser == null)
            {
                return null;
            }
            return await CreateOmbiUserFromLdapEntry(ldapUser);
        }

        public override async Task<bool> CheckPasswordAsync(OmbiUser user, string password)
        {
            var requiresPassword = await RequiresPassword(user);
            if (!requiresPassword)
            {
                // Let them through!
                return true;
            }
            if (user.UserType == UserType.LocalUser)
            {
                return await base.CheckPasswordAsync(user, password);
            }
            if (user.UserType == UserType.PlexUser)
            {
                return await CheckPlexPasswordAsync(user, password);
            }
            if (user.UserType == UserType.EmbyUser || user.UserType == UserType.EmbyConnectUser)
            {
                return await CheckEmbyPasswordAsync(user, password);
            }
            if (user.UserType == UserType.LdapUser)
            {
                return await CheckLdapPasswordAsync(user, password);
            }
            return false;
        }

        public async Task<bool> RequiresPassword(OmbiUser user)
        {
            var authSettings = await _authSettings.GetSettingsAsync();
            if (authSettings.AllowNoPassword)
            {
                var roles = await GetRolesAsync(user);
                if (roles.Contains(OmbiRoles.Admin) || roles.Contains(OmbiRoles.PowerUser))
                {
                    // We require a password
                    return true;
                }
                return false;
            }
            return true;
        }

        public async Task<OmbiUser> GetOmbiUserFromPlexToken(string plexToken)
        {
            var plexAccount = await _plexApi.GetAccount(plexToken);

            // Check for a ombi user
            if (plexAccount?.user != null)
            {
                var potentialOmbiUser = await Users.FirstOrDefaultAsync(x =>
                    x.ProviderUserId == plexAccount.user.id);
                return potentialOmbiUser;
            }

            return null;
        }
        

        /// <summary>
        /// Sign the user into plex and make sure we can get the authentication token.
        /// <remarks>We do not check if the user is in the owners "friends" since they must have a local user account to get this far</remarks>
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private async Task<bool> CheckPlexPasswordAsync(OmbiUser user, string password)
        {
            var login = user.EmailLogin ? user.Email : user.UserName;
            var result = await _plexApi.SignIn(new UserRequest { password = password, login = login });
            if (result.user?.authentication_token != null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sign the user into Emby
        /// <remarks>We do not check if the user is in the owners "friends" since they must have a local user account to get this far.
        /// We also have to try and authenticate them with every server, the first server that work we just say it was a success</remarks>
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private async Task<bool> CheckEmbyPasswordAsync(OmbiUser user, string password)
        {
            var embySettings = await _embySettings.GetSettingsAsync();
            var client = _embyApi.CreateClient(embySettings);

            if (user.IsEmbyConnect)
            {
                var result = await client.LoginConnectUser(user.UserName, password);
                if (result.AccessToken.HasValue())
                {
                    // We cannot update the email address in the user importer due to there is no way 
                    // To get this info from Emby Connect without the username and password.
                    // So we do it here!
                    var email = user.Email ?? string.Empty;
                    if (!email.Equals(result.User?.Email))
                    {
                        user.Email = result.User?.Email;
                        await UpdateAsync(user);
                    }

                    return true;
                }
            }

            foreach (var server in embySettings.Servers)
            {
                try
                {
                    var result = await client.LogIn(user.UserName, password, server.ApiKey, server.FullUri);
                    if (result != null)
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Emby Login Failed");
                }
            }
            return false;
        }

        private async Task<bool> CheckLdapPasswordAsync(OmbiUser user, string password)
        {
            var ldapSettings = await _ldapUserManager.GetSettings();
            if (!ldapSettings.IsEnabled)
            {
                return false;
            }

            return await _ldapUserManager.Authenticate(user, password);
        }

        public async Task<OmbiUser> CreateOmbiUserFromLdapEntry(LdapEntry entry)
        {
            var newUser = await _ldapUserManager.LdapEntryToOmbiUser(entry);
            var userManagementSettings = await _userManagementSettings.GetSettingsAsync();

            var result = await CreateAsync(newUser);
            if (!result.Succeeded)
            {
                foreach (var identityError in result.Errors)
                {
                    Logger.LogError(LoggingEvents.Authentication, identityError.Description);
                }
                return null;
            }

            if (userManagementSettings.DefaultRoles.Any())
            {
                foreach (var defaultRole in userManagementSettings.DefaultRoles)
                {
                    await AddToRoleAsync(newUser, defaultRole);
                }
            }

            return newUser;
        }
    }
}