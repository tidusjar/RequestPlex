using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ombi.Api.Emby;
using Ombi.Api.Github;
using Ombi.Attributes;
using Ombi.Core.Engine;
using Ombi.Core.Models.UI;
using Ombi.Core.Settings;
using Ombi.Core.Settings.Models;
using Ombi.Core.Settings.Models.External;
using Ombi.Helpers;
using Ombi.Models;
using Ombi.Schedule.Jobs.Radarr;
using Ombi.Settings.Settings.Models;
using Ombi.Settings.Settings.Models.External;
using Ombi.Settings.Settings.Models.Notifications;
using Ombi.Store.Entities;
using Ombi.Store.Repository;
using Ombi.Extensions;
using Quartz;
using Ombi.Schedule.Jobs;
using Ombi.Schedule.Jobs.Emby;
using Ombi.Schedule.Jobs.Sonarr;
using Ombi.Schedule.Jobs.Lidarr;

namespace Ombi.Controllers.V1
{
    /// <inheritdoc />
    /// <summary>
    /// The Settings Controller
    /// </summary>
    [Admin]
    [ApiV1]
    [Produces("application/json")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        public SettingsController(ISettingsResolver resolver,
            IMapper mapper,
            INotificationTemplatesRepository templateRepo,
            IEmbyApiFactory embyApi,
            ICacheService memCache,
            IGithubApi githubApi,
            IRecentlyAddedEngine engine)
        {
            SettingsResolver = resolver;
            Mapper = mapper;
            TemplateRepository = templateRepo;
            _embyApi = embyApi;
            _cache = memCache;
            _githubApi = githubApi;
            _recentlyAdded = engine;
        }

        private ISettingsResolver SettingsResolver { get; }
        private IMapper Mapper { get; }
        private INotificationTemplatesRepository TemplateRepository { get; }
        private readonly IEmbyApiFactory _embyApi;
        private readonly ICacheService _cache;
        private readonly IGithubApi _githubApi;
        private readonly IRecentlyAddedEngine _recentlyAdded;

        /// <summary>
        /// Gets the Ombi settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("ombi")]
        public async Task<OmbiSettings> OmbiSettings()
        {
            return await Get<OmbiSettings>();
        }

        /// <summary>
        /// Gets the base url.
        /// </summary>
        /// <returns></returns>
        [HttpGet("baseurl")]
        [AllowAnonymous]
        public async Task<string> GetBaseUrl()
        {
            var s = await Get<OmbiSettings>();
            var baseUrl = s?.BaseUrl ?? string.Empty;
            if (!baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.TrimEnd('/');
            }
            if (baseUrl.StartsWith("/"))
            {
                baseUrl = baseUrl.TrimStart('/');
            }
            return baseUrl;
        }

        /// <summary>
        /// Save the Ombi settings.
        /// </summary>
        /// <param name="ombi">The ombi.</param>
        /// <returns></returns>
        [HttpPost("ombi")]
        public async Task<bool> OmbiSettings([FromBody]OmbiSettings ombi)
        {
            ombi.Wizard = true;
            _cache.Remove(CacheKeys.OmbiSettings);
            return await Save(ombi);
        }

        [HttpGet("about")]
        public AboutViewModel About()
        {
            var dbConfiguration = DatabaseExtensions.GetDatabaseConfiguration();
            var storage = StoragePathSingleton.Instance;
            var model = new AboutViewModel
            {
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                OsDescription = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                ApplicationBasePath = Directory.GetCurrentDirectory(),
                ExternalDatabaseType = dbConfiguration.ExternalDatabase.Type,
                OmbiDatabaseType = dbConfiguration.OmbiDatabase.Type,
                SettingsDatabaseType = dbConfiguration.SettingsDatabase.Type,
                StoragePath = storage.StoragePath.HasValue() ? storage.StoragePath : "None Specified",
                NotSupported = Directory.GetCurrentDirectory().Contains("qpkg")
            };


            var version = AssemblyHelper.GetRuntimeVersion();
            var productArray = version.Split('-');
            model.Version = productArray[0];
            //model.Branch = productArray[1];
            return model;
        }

        [HttpPost("ombi/resetApi")]
        public async Task<string> ResetApiKey()
        {
            var currentSettings = await Get<OmbiSettings>();
            currentSettings.ApiKey = Guid.NewGuid().ToString("N");
            await Save(currentSettings);

            return currentSettings.ApiKey;
        }

        /// <summary>
        /// Gets the Plex Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("plex")]
        public async Task<PlexSettings> PlexSettings()
        {
            var s = await Get<PlexSettings>();
            return s;
        }

        [HttpGet("clientid")]
        [AllowAnonymous]
        public async Task<string> GetClientId()
        {
            var s = await Get<PlexSettings>();
            if (s.InstallId == Guid.Empty)
            {
                s.InstallId = Guid.NewGuid();
                // Save it
                await PlexSettings(s);
            }
            return s.InstallId.ToString("N");
        }

        /// <summary>
        /// Save the Plex settings.
        /// </summary>
        /// <param name="plex">The plex.</param>
        /// <returns></returns>
        [HttpPost("plex")]
        public async Task<bool> PlexSettings([FromBody]PlexSettings plex)
        {
            if (plex.InstallId == null || plex.InstallId == Guid.Empty)
            {
                plex.InstallId = Guid.NewGuid();
            }
            var result = await Save(plex);

            if (result)
            {
                // Kick off the plex sync
                await OmbiQuartz.TriggerJob(nameof(IPlexContentSync), "Plex");
            }

            return result;
        }

        /// <summary>
        /// Gets the Emby Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("emby")]
        public async Task<EmbySettings> EmbySettings()
        {
            return await Get<EmbySettings>();
        }

        /// <summary>
        /// Save the Emby settings.
        /// </summary>
        /// <param name="emby">The emby.</param>
        /// <returns></returns>
        [HttpPost("emby")]
        public async Task<bool> EmbySettings([FromBody]EmbySettings emby)
        {
            if (emby.Enable)
            {
                var client = await _embyApi.CreateClient();
                foreach (var server in emby.Servers)
                {
                    var users = await client.GetUsers(server.FullUri, server.ApiKey);
                    var admin = users.FirstOrDefault(x => x.Policy.IsAdministrator);
                    server.AdministratorId = admin?.Id;
                }
            }
            var result = await Save(emby);
            if (result)
            {
                await OmbiQuartz.TriggerJob(nameof(IEmbyContentSync), "Emby");
            }
            return result;
        }

        /// <summary>
        /// Gets the Landing Page Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("landingpage")]
        [AllowAnonymous]
        public async Task<LandingPageSettings> LandingPageSettings()
        {
            return await Get<LandingPageSettings>();
        }

        /// <summary>
        /// Save the Landing Page settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("landingpage")]
        public async Task<bool> LandingPageSettings([FromBody]LandingPageSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the Customization Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("customization")]
        [AllowAnonymous]
        public async Task<CustomizationSettings> CustomizationSettings()
        {
            return await Get<CustomizationSettings>();
        }


        /// <summary>
        /// Gets the default language set in Ombi
        /// </summary>
        /// <returns></returns>
        [HttpGet("defaultlanguage")]
        [AllowAnonymous]
        public async Task<string> GetDefaultLanguage()
        {
            var s = await Get<OmbiSettings>();
            return s.DefaultLanguageCode;
        }

        /// <summary>
        /// Save the Customization settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("customization")]
        public async Task<bool> CustomizationSettings([FromBody]CustomizationSettings settings)
        {
            return await Save(settings);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("customization/urlverify")]
        public bool VerifyUrl([FromBody]UrlVerifyModel url)
        {
            return Uri.TryCreate(url.Url, UriKind.Absolute, out var __);
        }

        /// <summary>
        /// Get's the preset themes available
        /// </summary>
        /// <returns></returns>
        [HttpGet("themes")]
        public async Task<IEnumerable<PresetThemeViewModel>> GetThemes()
        {
            var themes = await _githubApi.GetCakeThemes();
            var cssThemes = themes.Where(x => x.name.Contains(".css", CompareOptions.IgnoreCase)
                                              && x.type.Equals("file", StringComparison.CurrentCultureIgnoreCase));

            // 001-theBlur-leram84-1.0.css
            // Number-Name-Author-Version.css
            var model = new List<PresetThemeViewModel>();
            foreach (var theme in cssThemes)
            {
                var parts = theme.name.Split("-");
                model.Add(new PresetThemeViewModel
                {
                    DisplayName = parts[1],
                    FullName = theme.name,
                    Version = parts[3].Replace(".css", string.Empty, StringComparison.CurrentCultureIgnoreCase),
                    Url = theme.download_url
                });
            }

            // Display on UI - Current Theme = theBlur 1.0
            // In dropdown display as "theBlur 1.1"

            return model;
        }

        /// <summary>
        /// Gets the Sonarr Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("sonarr")]
        public async Task<SonarrSettings> SonarrSettings()
        {
            return await Get<SonarrSettings>();
        }

        /// <summary>
        /// Save the Sonarr settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("sonarr")]
        public async Task<bool> SonarrSettings([FromBody]SonarrSettings settings)
        {
            var result = await Save(settings);
            if (result)
            {
                await OmbiQuartz.TriggerJob(nameof(ISonarrSync), "DVR");
            }
            return result;
        }

        /// <summary>
        /// Gets the Radarr Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("radarr")]
        public async Task<RadarrSettings> RadarrSettings()
        {
            return await Get<RadarrSettings>();
        }

        /// <summary>
        /// Gets the Lidarr Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("lidarr")]
        public async Task<LidarrSettings> LidarrSettings()
        {
            return await Get<LidarrSettings>();
        }

        /// <summary>
        /// Gets the Lidarr Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("lidarrenabled")]
        [AllowAnonymous]
        public async Task<bool> LidarrEnabled()
        {
            var settings = await Get<LidarrSettings>();
            return settings.Enabled;
        }

        /// <summary>
        /// Save the Lidarr settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("lidarr")]
        public async Task<bool> LidarrSettings([FromBody]LidarrSettings settings)
        {
            var lidarr = await Save(settings);
            if (lidarr)
            {
                await OmbiQuartz.TriggerJob(nameof(ILidarrArtistSync), "DVR");
            }
            return lidarr;
        }

        /// <summary>
        /// Save the Authentication settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("authentication")]
        public async Task<bool> AuthenticationsSettings([FromBody]AuthenticationSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the Authentication Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("authentication")]
        [AllowAnonymous]
        public async Task<AuthenticationSettings> AuthenticationsSettings()
        {
            return await Get<AuthenticationSettings>();
        }

        /// <summary>
        /// Save the LDAP settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("ldap")]
        public async Task<bool> LdapSettings([FromBody] LdapSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the LDAP Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("ldap")]
        [AllowAnonymous]
        public async Task<LdapSettings> LdapSettings()
        {
            return await Get<LdapSettings>();
        }

        /// <summary>
        /// Save the Radarr settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("radarr")]
        public async Task<bool> RadarrSettings([FromBody]RadarrSettings settings)
        {
            var result = await Save(settings);
            if (result)
            {
                _cache.Remove(CacheKeys.RadarrRootProfiles);
                _cache.Remove(CacheKeys.RadarrQualityProfiles);

                await OmbiQuartz.TriggerJob(nameof(IRadarrSync), "DVR");
            }
            return result;
        }

        /// <summary>
        /// Save the Update settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("Update")]
        public async Task<bool> UpdateSettings([FromBody]UpdateSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the UserManagement Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("UserManagement")]
        public async Task<UserManagementSettings> UserManagementSettings()
        {
            return await Get<UserManagementSettings>();
        }

        /// <summary>
        /// Save the UserManagement settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("UserManagement")]
        public async Task<bool> UserManagementSettings([FromBody]UserManagementSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the Update Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Update")]
        public async Task<UpdateSettings> UpdateSettings()
        {
            var settings = await Get<UpdateSettings>();

            return Mapper.Map<UpdateSettingsViewModel>(settings);
        }

        /// <summary>
        /// Gets the CouchPotatoSettings Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("CouchPotato")]
        public async Task<CouchPotatoSettings> CouchPotatoSettings()
        {
            return await Get<CouchPotatoSettings>();
        }

        /// <summary>
        /// Save the CouchPotatoSettings settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("CouchPotato")]
        public async Task<bool> CouchPotatoSettings([FromBody]CouchPotatoSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the DogNzbSettings Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("DogNzb")]
        public async Task<DogNzbSettings> DogNzbSettings()
        {
            return await Get<DogNzbSettings>();
        }

        /// <summary>
        /// Save the DogNzbSettings settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("DogNzb")]
        public async Task<bool> DogNzbSettings([FromBody]DogNzbSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Save the SickRage settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("SickRage")]
        public async Task<bool> SickRageSettings([FromBody]SickRageSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the SickRage Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SickRage")]
        public async Task<SickRageSettings> SickRageSettings()
        {
            return await Get<SickRageSettings>();
        }

        /// <summary>
        /// Gets the JobSettings Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("jobs")]
        public async Task<JobSettings> JobSettings()
        {
            var j = await Get<JobSettings>();
            // Get the defaults if the jobs are not set
            j.RadarrSync = j.RadarrSync.HasValue() ? j.RadarrSync : JobSettingsHelper.Radarr(j);
            j.SonarrSync = j.SonarrSync.HasValue() ? j.SonarrSync : JobSettingsHelper.Sonarr(j);
            j.AutomaticUpdater = j.AutomaticUpdater.HasValue() ? j.AutomaticUpdater : JobSettingsHelper.Updater(j);
            j.CouchPotatoSync = j.CouchPotatoSync.HasValue() ? j.CouchPotatoSync : JobSettingsHelper.CouchPotato(j);
            j.EmbyContentSync = j.EmbyContentSync.HasValue() ? j.EmbyContentSync : JobSettingsHelper.EmbyContent(j);
            j.PlexContentSync = j.PlexContentSync.HasValue() ? j.PlexContentSync : JobSettingsHelper.PlexContent(j);
            j.UserImporter = j.UserImporter.HasValue() ? j.UserImporter : JobSettingsHelper.UserImporter(j);
            j.SickRageSync = j.SickRageSync.HasValue() ? j.SickRageSync : JobSettingsHelper.SickRageSync(j);
            j.PlexRecentlyAddedSync = j.PlexRecentlyAddedSync.HasValue() ? j.PlexRecentlyAddedSync : JobSettingsHelper.PlexRecentlyAdded(j);
            j.Newsletter = j.Newsletter.HasValue() ? j.Newsletter : JobSettingsHelper.Newsletter(j);
            j.LidarrArtistSync = j.LidarrArtistSync.HasValue() ? j.LidarrArtistSync : JobSettingsHelper.LidarrArtistSync(j);
            j.IssuesPurge = j.IssuesPurge.HasValue() ? j.IssuesPurge : JobSettingsHelper.IssuePurge(j);
            j.RetryRequests = j.RetryRequests.HasValue() ? j.RetryRequests : JobSettingsHelper.ResendFailedRequests(j);
            j.MediaDatabaseRefresh = j.MediaDatabaseRefresh.HasValue() ? j.MediaDatabaseRefresh : JobSettingsHelper.MediaDatabaseRefresh(j);
            j.AutoDeleteRequests = j.AutoDeleteRequests.HasValue() ? j.AutoDeleteRequests : JobSettingsHelper.AutoDeleteRequests(j);

            return j;
        }

        /// <summary>
        /// Save the JobSettings settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("jobs")]
        public async Task<JobSettingsViewModel> JobSettings([FromBody]JobSettings settings)
        {
            // Verify that we have correct CRON's
            foreach (var propertyInfo in settings.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (propertyInfo.Name.Equals("Id", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                var expression = (string)propertyInfo.GetValue(settings, null);

                try
                {
                    var isValid = CronExpression.IsValidExpression(expression);
                    if (!isValid)
                    {
                        return new JobSettingsViewModel
                        {
                            Message = $"{propertyInfo.Name} does not have a valid CRON Expression"
                        };
                    }
                }
                catch (Exception)
                {
                    return new JobSettingsViewModel
                    {
                        Message = $"{propertyInfo.Name} does not have a valid CRON Expression"
                    };
                }
            }
            var result = await Save(settings);

            return new JobSettingsViewModel
            {
                Result = result
            };
        }

        [HttpPost("testcron")]
        public CronTestModel TestCron([FromBody] CronViewModelBody body)
        {
            var model = new CronTestModel();
            try
            {
                var isValid = CronExpression.IsValidExpression(body.Expression);
                if (!isValid)
                {
                    return new CronTestModel
                    {
                        Message = $"CRON Expression {body.Expression} is not valid"
                    };
                }

                model.Success = true;
                return model;
            }
            catch (Exception)
            {
                return new CronTestModel
                {
                    Message = $"CRON Expression {body.Expression} is not valid"
                };
            }
        }


        /// <summary>
        /// Save the Vote settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("Issues")]
        public async Task<bool> IssueSettings([FromBody]IssueSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the Issues Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("Issues")]
        [AllowAnonymous]
        public async Task<IssueSettings> IssueSettings()
        {
            return await Get<IssueSettings>();
        }

        [AllowAnonymous]
        [HttpGet("issuesenabled")]
        public async Task<bool> IssuesEnabled()
        {
            var issues = await Get<IssueSettings>();
            return issues.Enabled;
        }

        /// <summary>
        /// Save the Vote settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        [HttpPost("vote")]
        public async Task<bool> VoteSettings([FromBody]VoteSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Gets the Vote Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("vote")]
        public async Task<VoteSettings> VoteSettings()
        {
            return await Get<VoteSettings>();
        }

        [AllowAnonymous]
        [HttpGet("voteenabled")]
        public async Task<bool> VoteEnabled()
        {
            var vote = await Get<VoteSettings>();
            return vote.Enabled;
        }

        /// <summary>
        /// Save The Movie DB settings.
        /// </summary>
        /// <param name="settings">The settings.</param>
        [HttpPost("themoviedb")]
        public async Task<bool> TheMovieDbSettings([FromBody]TheMovieDbSettings settings)
        {
            return await Save(settings);
        }

        /// <summary>
        /// Get The Movie DB settings.
        /// </summary>
        [HttpGet("themoviedb")]
        public async Task<TheMovieDbSettings> TheMovieDbSettings()
        {
            return await Get<TheMovieDbSettings>();
        }

        /// <summary>
        /// Saves the email notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/email")]
        public async Task<bool> EmailNotificationSettings([FromBody] EmailNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<EmailNotificationSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the Email Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/email")]
        public async Task<EmailNotificationsViewModel> EmailNotificationSettings()
        {
            var emailSettings = await Get<EmailNotificationSettings>();
            var model = Mapper.Map<EmailNotificationsViewModel>(emailSettings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Email);

            return model;
        }


        /// <summary>
        /// Gets the Email Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/email/enabled")]
        [AllowAnonymous]
        public async Task<bool> EmailNotificationSettingsEnabled()
        {
            var emailSettings = await Get<EmailNotificationSettings>();
            return emailSettings.Enabled;
        }

        /// <summary>
        /// Saves the discord notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/discord")]
        public async Task<bool> DiscordNotificationSettings([FromBody] DiscordNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<DiscordNotificationSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the discord Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/discord")]
        public async Task<DiscordNotificationsViewModel> DiscordNotificationSettings()
        {
            var emailSettings = await Get<DiscordNotificationSettings>();
            var model = Mapper.Map<DiscordNotificationsViewModel>(emailSettings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Discord);

            return model;
        }


        /// <summary>
        /// Saves the telegram notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/telegram")]
        public async Task<bool> TelegramNotificationSettings([FromBody] TelegramNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<TelegramSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the telegram Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/telegram")]
        public async Task<TelegramNotificationsViewModel> TelegramNotificationSettings()
        {
            var emailSettings = await Get<TelegramSettings>();
            var model = Mapper.Map<TelegramNotificationsViewModel>(emailSettings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Telegram);

            return model;
        }

        /// <summary>
        /// Saves the pushbullet notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/pushbullet")]
        public async Task<bool> PushbulletNotificationSettings([FromBody] PushbulletNotificationViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<PushbulletSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the pushbullet Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/pushbullet")]
        public async Task<PushbulletNotificationViewModel> PushbulletNotificationSettings()
        {
            var settings = await Get<PushbulletSettings>();
            var model = Mapper.Map<PushbulletNotificationViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Pushbullet);

            return model;
        }

        /// <summary>
        /// Saves the pushover notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/pushover")]
        public async Task<bool> PushoverNotificationSettings([FromBody] PushoverNotificationViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<PushoverSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the pushover Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/pushover")]
        public async Task<PushoverNotificationViewModel> PushoverNotificationSettings()
        {
            var settings = await Get<PushoverSettings>();
            var model = Mapper.Map<PushoverNotificationViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Pushover);

            return model;
        }


        /// <summary>
        /// Saves the slack notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/slack")]
        public async Task<bool> SlacktNotificationSettings([FromBody] SlackNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<SlackNotificationSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the slack Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/slack")]
        public async Task<SlackNotificationsViewModel> SlackNotificationSettings()
        {
            var settings = await Get<SlackNotificationSettings>();
            var model = Mapper.Map<SlackNotificationsViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Slack);

            return model;
        }

        /// <summary>
        /// Saves the Mattermost notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/mattermost")]
        public async Task<bool> MattermostNotificationSettings([FromBody] MattermostNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<MattermostNotificationSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the Mattermost Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/mattermost")]
        public async Task<MattermostNotificationsViewModel> MattermostNotificationSettings()
        {
            var settings = await Get<MattermostNotificationSettings>();
            var model = Mapper.Map<MattermostNotificationsViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Mattermost);

            return model;
        }

        /// <summary>
        /// Gets the Twilio Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/twilio")]
        public async Task<TwilioSettingsViewModel> TwilioNotificationSettings()
        {
            var settings = await Get<TwilioSettings>();
            var model = Mapper.Map<TwilioSettingsViewModel>(settings);

            // Lookup to see if we have any templates saved
            if (model.WhatsAppSettings == null)
            {
                model.WhatsAppSettings = new WhatsAppSettingsViewModel();
            }
            model.WhatsAppSettings.NotificationTemplates = BuildTemplates(NotificationAgent.WhatsApp);

            return model;
        }

        /// <summary>
        /// Saves the Mattermost notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/twilio")]
        public async Task<bool> TwilioNotificationSettings([FromBody] TwilioSettingsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<TwilioSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.WhatsAppSettings.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Saves the Mobile notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/mobile")]
        public async Task<bool> MobileNotificationSettings([FromBody] MobileNotificationsViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<MobileNotificationSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the Mobile Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/mobile")]
        public async Task<MobileNotificationsViewModel> MobileNotificationSettings()
        {
            var settings = await Get<MobileNotificationSettings>();
            var model = Mapper.Map<MobileNotificationsViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Mobile);

            return model;
        }

        /// <summary>
        /// Saves the gotify notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/gotify")]
        public async Task<bool> GotifyNotificationSettings([FromBody] GotifyNotificationViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<GotifySettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.UpdateRange(model.NotificationTemplates);

            return result;
        }

        /// <summary>
        /// Gets the gotify Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/gotify")]
        public async Task<GotifyNotificationViewModel> GotifyNotificationSettings()
        {
            var settings = await Get<GotifySettings>();
            var model = Mapper.Map<GotifyNotificationViewModel>(settings);

            // Lookup to see if we have any templates saved
            model.NotificationTemplates = BuildTemplates(NotificationAgent.Gotify);

            return model;
        }

        /// <summary>
        /// Saves the webhook notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/webhook")]
        public async Task<bool> WebhookNotificationSettings([FromBody] WebhookNotificationViewModel model)
        {
            var settings = Mapper.Map<WebhookSettings>(model);
            var result = await Save(settings);

            return result;
        }

        /// <summary>
        /// Gets the webhook notification settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/webhook")]
        public async Task<WebhookNotificationViewModel> WebhookNotificationSettings()
        {
            var settings = await Get<WebhookSettings>();
            var model = Mapper.Map<WebhookNotificationViewModel>(settings);

            return model;
        }

        /// <summary>
        /// Saves the Newsletter notification settings.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        [HttpPost("notifications/newsletter")]
        public async Task<bool> NewsletterSettings([FromBody] NewsletterNotificationViewModel model)
        {
            // Save the email settings
            var settings = Mapper.Map<NewsletterSettings>(model);
            var result = await Save(settings);

            // Save the templates
            await TemplateRepository.Update(model.NotificationTemplate);

            return result;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("notifications/newsletterdatabase")]
        public async Task<bool> UpdateNewsletterDatabase()
        {
            return await _recentlyAdded.UpdateRecentlyAddedDatabase();
        }

        /// <summary>
        /// Gets the Newsletter Notification Settings.
        /// </summary>
        /// <returns></returns>
        [HttpGet("notifications/newsletter")]
        public async Task<NewsletterNotificationViewModel> NewsletterSettings()
        {
            var settings = await Get<NewsletterSettings>();
            var model = Mapper.Map<NewsletterNotificationViewModel>(settings);

            // Lookup to see if we have any templates saved
            var templates = BuildTemplates(NotificationAgent.Email, true);
            model.NotificationTemplate = templates.FirstOrDefault(x => x.NotificationType == NotificationType.Newsletter);
            return model;
        }

        private List<NotificationTemplates> BuildTemplates(NotificationAgent agent, bool showNewsletter = false)
        {
            var templates = TemplateRepository.GetAllTemplates(agent);
            if (!showNewsletter)
            {
                // Make sure we do not display the newsletter
                templates = templates.Where(x => x.NotificationType != NotificationType.Newsletter);
            }
            if (agent != NotificationAgent.Email)
            {
                templates = templates.Where(x => x.NotificationType != NotificationType.WelcomeEmail);
            }

            var tem = templates.ToList();
            return tem.OrderBy(x => x.NotificationType.ToString()).ToList();
        }

        private async Task<T> Get<T>()
        {
            var settings = SettingsResolver.Resolve<T>();
            return await settings.GetSettingsAsync();
        }

        private async Task<bool> Save<T>(T settingsModel)
        {
            var settings = SettingsResolver.Resolve<T>();
            return await settings.SaveSettingsAsync(settingsModel);
        }
    }
}
