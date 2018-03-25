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

            DiscordAuthor author = new DiscordAuthor
            {
                name = parsed.Subject,
                icon_url = "https://i.imgur.com/EPuxVav.png"
            };
            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = CreateDiscordEmbed(author, MovieRequest, NotificationType.NewRequest, parsed.Image, parsed.Message);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = CreateDiscordEmbed(author, TvRequest, NotificationType.NewRequest, parsed.Image, parsed.Message);
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

            DiscordAuthor author = new DiscordAuthor
            {
                name = parsed.Subject,
                icon_url = "https://i.imgur.com/i1X39I2.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = CreateDiscordEmbed(author, MovieRequest, NotificationType.RequestDeclined, parsed.Image, parsed.Message);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = CreateDiscordEmbed(author, TvRequest, NotificationType.RequestDeclined, parsed.Image, parsed.Message);
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

            DiscordAuthor author = new DiscordAuthor
            {
                name = parsed.Subject,
                icon_url = "https://i.imgur.com/sodXDGW.png"
            };

            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = CreateDiscordEmbed(author, MovieRequest, NotificationType.RequestApproved, parsed.Image, parsed.Message);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = CreateDiscordEmbed(author, TvRequest, NotificationType.RequestApproved, parsed.Image, parsed.Message);
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

            // TODO: implement plex / emby url
            string authorUrl = null;
            /*
            if (Customization.ApplicationUrl.HasValue())
                authorUrl = $"{Customization.ApplicationUrl}requests";
            */
            

            DiscordAuthor author = new DiscordAuthor
            {
                name = parsed.Subject,
                url = authorUrl,
                icon_url = "https://i.imgur.com/k4bX9KM.png"
            };
            
            DiscordEmbeds embed = null;
            if (model.RequestType == RequestType.Movie)
            {
                embed = CreateDiscordEmbed(author, MovieRequest, NotificationType.RequestAvailable, parsed.Image, parsed.Message);
            }
            else if (model.RequestType == RequestType.TvShow)
            {
                embed = CreateDiscordEmbed(author, TvRequest, NotificationType.RequestAvailable, parsed.Image, parsed.Message);
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

        private DiscordEmbeds CreateDiscordEmbed(DiscordAuthor author, BaseRequest req, NotificationType type, string imageUrl, string description)
        {
            MentionAlias.TryGetValue(type, out var mentionUser);
            ShowCompactEmbed.TryGetValue(type, out var compactEmbed);
            // Grab info specific to TV / Movie requests
            string overview = null;
            int releaseYear = 0;
            string titleUrl = null;
            if (req is ChildRequests)
            {
                ChildRequests tvReq = req as ChildRequests;
                overview = tvReq.ParentRequest.Overview;
                releaseYear = tvReq.ParentRequest.ReleaseDate.Year;
                titleUrl = $"{TVDB_BASE_URL}{tvReq.ParentRequest.TvDbId}";
            }
            else if (req is MovieRequests)
            {
                MovieRequests movieReq = req as MovieRequests;
                overview = movieReq.Overview;
                releaseYear = movieReq.ReleaseDate.Year;
                titleUrl = $"{IMDB_BASE_URL}{movieReq.ImdbId}";
            }

            // Compact embed
            // TODO: check imageUrl is valid - if malformed discord will throw error and not post at all
            DiscordImage image = new DiscordImage { url = imageUrl };
            DiscordImage thumbnail = null;
            if (compactEmbed)
            {
                image = null;
                thumbnail = new DiscordImage { url = imageUrl };
                overview = null;
                description = null;
            }            

            // Fields
            List<DiscordField> fields = new List<DiscordField>();
            if (overview.HasValue())
            {
                fields.Add
                (
                    new DiscordField
                    {
                        name = "Overview",
                        value = overview
                    }
                );
            }
            // Field : mention user
            string alias = req.RequestedUser.Alias;
            if (UsingAliasAsMention && mentionUser && alias.HasValue())
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
