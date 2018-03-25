﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ombi.Api.Discord;
using Ombi.Api.Discord.Models;
using Ombi.Core.Settings;
using Ombi.Helpers;
using Ombi.Notifications.Interfaces;
using Ombi.Notifications.Models;
using Ombi.Settings.Settings.Models;
using Ombi.Settings.Settings.Models.Notifications;
using Ombi.Store.Entities;
using Ombi.Store.Repository;
using Ombi.Store.Repository.Requests;
using Ombi.Store.Entities.Requests;

namespace Ombi.Notifications.Agents
{
    public class DiscordNotification : BaseNotification<DiscordNotificationSettings>, IDiscordNotification
    {
        public DiscordNotification(IDiscordApi api, ISettingsService<DiscordNotificationSettings> sn,
                                   ILogger<DiscordNotification> log, INotificationTemplatesRepository r,
                                   IMovieRequestRepository m, ITvRequestRepository t, ISettingsService<CustomizationSettings> s)
            : base(sn, r, m, t, s, log)
        {
            Api = api;
            Logger = log;
            ShowCompactEmbed = new Dictionary<NotificationType, bool>();
            // Temporary defaults
            ShowCompactEmbed.Add(NotificationType.NewRequest, true);
            ShowCompactEmbed.Add(NotificationType.RequestApproved, true);
            ShowCompactEmbed.Add(NotificationType.RequestDeclined, true);

            MentionAlias = new Dictionary<NotificationType, bool>();
            // Temporary defaults
            MentionAlias.Add(NotificationType.RequestAvailable, true);
            MentionAlias.Add(NotificationType.RequestApproved, true);
            MentionAlias.Add(NotificationType.RequestDeclined, true);
        }

        // constants I needed but could not find
        public const string IMDB_BASE_URL = "http://www.imdb.com/title/";
        public const string TVDB_BASE_URL = "https://www.thetvdb.com/?tab=series&id=";

        // Whether or not to show a compact embed notification (thumbnail + no description)
        public Dictionary<NotificationType, bool> ShowCompactEmbed;
        // Whether or not to post the alias to discord (for each notification type) which trigger an @mention if alias is set up as <@id>.  
        public Dictionary<NotificationType, bool> MentionAlias;
        // Whether to use alias or username when referencing a user
        public bool UsingAliasAsMention = true;

        public override string NotificationName => "DiscordNotification";

        private IDiscordApi Api { get; }
        private ILogger<DiscordNotification> Logger { get; }

        protected override bool ValidateConfiguration(DiscordNotificationSettings settings)
        {
            if (!settings.Enabled)
            {
                return false;
            }
            if (string.IsNullOrEmpty(settings.WebhookUrl))
            {
                return false;
            }
            try
            {
                var a = settings.Token;
                var b = settings.WebHookId;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
            return true;
        }

        protected override async Task NewRequest(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.NewRequest, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.NewRequest} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };

            string authorUrl = null;
            if (Customization.ApplicationUrl.HasValue())
                authorUrl = $"{Customization.ApplicationUrl}requests";

            ShowCompactEmbed.TryGetValue(NotificationType.NewRequest, out var compactEmbed);
            MentionAlias.TryGetValue(NotificationType.NewRequest, out var mentionUser);

            DiscordAuthor author = new DiscordAuthor
            {
                name = "New Request!",
                url = authorUrl,
                icon_url = "https://i.imgur.com/EPuxVav.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = createDiscordEmbed(author, MovieRequest, parsed.Image, compactEmbed, mentionUser);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = createDiscordEmbed(author, TvRequest, parsed.Image, compactEmbed, mentionUser);
            }
            await Send(notification, settings, embed);
        }

        protected override async Task NewIssue(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.Issue, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.Issue} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };
            notification.Other.Add("image", parsed.Image);
            await Send(notification, settings);
        }

        protected override async Task IssueComment(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.IssueComment, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.IssueComment} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };
            notification.Other.Add("image", parsed.Image);
            await Send(notification, settings);
        }

        protected override async Task IssueResolved(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.IssueResolved, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.IssueResolved} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };
            notification.Other.Add("image", parsed.Image);
            await Send(notification, settings);
        }

        protected override async Task AddedToRequestQueue(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var user = string.Empty;
            var title = string.Empty;
            var image = string.Empty;
            if (model.RequestType == RequestType.Movie)
            {
                user = MovieRequest.RequestedUser.UserAlias;
                title = MovieRequest.Title;
                image = MovieRequest.PosterPath;
            }
            else
            {
                user = TvRequest.RequestedUser.UserAlias;
                title = TvRequest.ParentRequest.Title;
                image = TvRequest.ParentRequest.PosterPath;
            }
            var message = $"Hello! The user '{user}' has requested {title} but it could not be added. This has been added into the requests queue and will keep retrying";
            var notification = new NotificationMessage
            {
                Message = message
            };
            notification.Other.Add("image", image);
            await Send(notification, settings);
        }

        protected override async Task RequestDeclined(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.RequestDeclined, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.RequestDeclined} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };

            ShowCompactEmbed.TryGetValue(NotificationType.RequestDeclined, out var compactEmbed);
            MentionAlias.TryGetValue(NotificationType.RequestDeclined, out var mentionUser);

            DiscordAuthor author = new DiscordAuthor
            {
                name = "Request Declined!",
                icon_url = "https://i.imgur.com/i1X39I2.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = createDiscordEmbed(author, MovieRequest, parsed.Image, compactEmbed, mentionUser);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = createDiscordEmbed(author, TvRequest, parsed.Image, compactEmbed, mentionUser);
            }
            await Send(notification, settings, embed);
        }

        protected override async Task RequestApproved(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.RequestApproved, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.RequestApproved} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };

            ShowCompactEmbed.TryGetValue(NotificationType.RequestApproved, out var compactEmbed);
            MentionAlias.TryGetValue(NotificationType.RequestApproved, out var mentionUser);

            DiscordAuthor author = new DiscordAuthor
            {
                name = "Request Approved!",
                icon_url = "https://i.imgur.com/sodXDGW.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = createDiscordEmbed(author, MovieRequest, parsed.Image, compactEmbed, mentionUser);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = createDiscordEmbed(author, TvRequest, parsed.Image, compactEmbed, mentionUser);
            }
            await Send(notification, settings, embed);
        }

        protected override async Task AvailableRequest(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var parsed = await LoadTemplate(NotificationAgent.Discord, NotificationType.RequestAvailable, model);
            if (parsed.Disabled)
            {
                Logger.LogInformation($"Template {NotificationType.RequestAvailable} is disabled for {NotificationAgent.Discord}");
                return;
            }
            var notification = new NotificationMessage
            {
                Message = parsed.Message,
            };

            // TODO implement plex / emby url
            string authorUrl = null;
            /*
            if (Customization.ApplicationUrl.HasValue())
                authorUrl = $"{Customization.ApplicationUrl}requests";
            */

            ShowCompactEmbed.TryGetValue(NotificationType.RequestAvailable, out var compact);
            MentionAlias.TryGetValue(NotificationType.RequestAvailable, out var mentionUser);

            DiscordAuthor author = new DiscordAuthor
            {
                name = "Request Now Available!",
                url = authorUrl,
                icon_url = "https://i.imgur.com/k4bX9KM.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = createDiscordEmbed(author, MovieRequest, parsed.Image, compact, mentionUser);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = createDiscordEmbed(author, TvRequest, parsed.Image, compact, mentionUser);
            }

            await Send(notification, settings, embed);
        }

        protected async Task Send(NotificationMessage model, DiscordNotificationSettings settings, DiscordEmbeds embed)
        {
            try
            {
                var discordBody = new DiscordWebhookBody
                {
                    username = settings.Username,
                };
                discordBody.embeds = new List<DiscordEmbeds>
                {
                    embed
                };
                await Api.SendMessage(discordBody, settings.WebHookId, settings.Token);
            }
            catch (Exception e)
            {
                Logger.LogError(LoggingEvents.DiscordNotification, e, "Failed to send Discord Notification");
            }
        }

        protected override async Task Send(NotificationMessage model, DiscordNotificationSettings settings)
        {
            try
            {
                var discordBody = new DiscordWebhookBody
                {
                    content = model.Message,
                    username = settings.Username,
                };

                model.Other.TryGetValue("image", out var image);
                discordBody.embeds = new List<DiscordEmbeds>

                {
                    new DiscordEmbeds
                    {
                        image = new DiscordImage
                        {
                            url = image
                        }
                    }
                };

                await Api.SendMessage(discordBody, settings.WebHookId, settings.Token);
            }
            catch (Exception e)
            {
                Logger.LogError(LoggingEvents.DiscordNotification, e, "Failed to send Discord Notification");
            }
        }

        protected override async Task Test(NotificationOptions model, DiscordNotificationSettings settings)
        {
            var message = $"This is a test from Ombi, if you can see this then we have successfully pushed a notification!";
            var notification = new NotificationMessage
            {
                Message = message,
            };
            await Send(notification, settings);
        }

        private DiscordEmbeds createDiscordEmbed(DiscordAuthor author, BaseRequest req, string imageUrl, bool compactEmbed, bool mentionUser)
        {
            // Grab info specific to TV / Movie requests
            string description = null;
            int releaseYear = 0;
            string titleUrl = null;
            if (req is ChildRequests)
            {
                ChildRequests tvReq = req as ChildRequests;
                description = tvReq.ParentRequest.Overview;
                releaseYear = tvReq.ParentRequest.ReleaseDate.Year;
                titleUrl = $"{TVDB_BASE_URL}{tvReq.ParentRequest.TvDbId}";
            }
            else if (req is MovieRequests)
            {
                MovieRequests movieReq = req as MovieRequests;
                description = movieReq.Overview;
                releaseYear = movieReq.ReleaseDate.Year;
                titleUrl = $"{IMDB_BASE_URL}{movieReq.ImdbId}";
            }

            // Compact embed
            DiscordImage image = null;
            DiscordImage thumbnail = null;
            if (compactEmbed)
            {
                thumbnail = new DiscordImage { url = imageUrl };
                description = null;
            }
            else
            {
                image = new DiscordImage { url = imageUrl };
            }
            
            // Fields
            List<DiscordField> fields = new List<DiscordField>();
            // Field : mention user
            string alias = req.RequestedUser.Alias;
            if (UsingAliasAsMention && mentionUser)
            {
                fields.Add
                (
                    new DiscordField
                    {
                        name = "Honourable Mentions",
                        value = alias
                    }
                );
            }

            // Use appropriate user reference
            if (UsingAliasAsMention || !alias.HasValue())
                alias = req.RequestedUser.UserName;

            DiscordFooter footer = new DiscordFooter
            {
                text = $"Requested by {alias}  on {req.RequestedDate.ToLongDateString()}"
            };

            DiscordEmbeds embed = new DiscordEmbeds
            {
                author = author,
                title = $"{req.Title} ({releaseYear})",
                url = titleUrl,
                thumbnail = thumbnail,
                image = image,
                description = description,
                footer = footer,
                fields = fields
            };
            return embed;
        }
    }
}
