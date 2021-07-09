using Ombi.Api.TheMovieDb;
using Ombi.Core.Models.Requests;
using Ombi.Helpers;
using Ombi.Store.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ombi.Api.TheMovieDb.Models;
using Ombi.Core.Authentication;
using Ombi.Core.Engine.Interfaces;
using Ombi.Core.Models.UI;
using Ombi.Core.Rule.Interfaces;
using Ombi.Core.Settings;
using Ombi.Settings.Settings.Models;
using Ombi.Store.Entities.Requests;
using Ombi.Store.Repository;
using Ombi.Core.Models;
using System.Threading;

namespace Ombi.Core.Engine
{
    public class MovieRequestEngine : BaseMediaEngine, IMovieRequestEngine
    {
        public MovieRequestEngine(IMovieDbApi movieApi, IRequestServiceMain requestService, IPrincipal user,
            INotificationHelper helper, IRuleEvaluator r, IMovieSender sender, ILogger<MovieRequestEngine> log,
            OmbiUserManager manager, IRepository<RequestLog> rl, ICacheService cache,
            ISettingsService<OmbiSettings> ombiSettings, IRepository<RequestSubscription> sub, IMediaCacheService mediaCacheService)
            : base(user, requestService, r, manager, cache, ombiSettings, sub)
        {
            MovieApi = movieApi;
            NotificationHelper = helper;
            Sender = sender;
            Logger = log;
            _requestLog = rl;
            _mediaCacheService = mediaCacheService;
        }

        private IMovieDbApi MovieApi { get; }
        private INotificationHelper NotificationHelper { get; }
        private IMovieSender Sender { get; }
        private ILogger<MovieRequestEngine> Logger { get; }
        private readonly IRepository<RequestLog> _requestLog;
        private readonly IMediaCacheService _mediaCacheService;

        /// <summary>
        /// Requests the movie.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public async Task<RequestEngineResult> RequestMovie(MovieRequestViewModel model)
        {
            var movieInfo = await MovieApi.GetMovieInformationWithExtraInfo(model.TheMovieDbId, model.LanguageCode);
            if (movieInfo == null || movieInfo.Id == 0)
            {
                return new RequestEngineResult
                {
                    Result = false,
                    Message = "There was an issue adding this movie!",
                    ErrorMessage = $"Please try again later"
                };
            }

            var fullMovieName =
                $"{movieInfo.Title}{(!string.IsNullOrEmpty(movieInfo.ReleaseDate) ? $" ({DateTime.Parse(movieInfo.ReleaseDate).Year})" : string.Empty)}";

            var userDetails = await GetUser();
            var canRequestOnBehalf = model.RequestOnBehalf.HasValue();

            var isAdmin = await UserManager.IsInRoleAsync(userDetails, OmbiRoles.PowerUser) || await UserManager.IsInRoleAsync(userDetails, OmbiRoles.Admin);
            if (canRequestOnBehalf && !isAdmin)
            {
                return new RequestEngineResult
                {
                    Result = false,
                    Message = "You do not have the correct permissions to request on behalf of users!",
                    ErrorMessage = $"You do not have the correct permissions to request on behalf of users!"
                };
            }

            if ((model.RootFolderOverride.HasValue || model.QualityPathOverride.HasValue) && !isAdmin)
            {
                return new RequestEngineResult
                {
                    Result = false,
                    Message = "You do not have the correct permissions!",
                    ErrorMessage = $"You do not have the correct permissions!"
                };
            }

            var requestModel = new MovieRequests
            {
                TheMovieDbId = movieInfo.Id,
                RequestType = RequestType.Movie,
                Overview = movieInfo.Overview,
                ImdbId = movieInfo.ImdbId,
                PosterPath = PosterPathHelper.FixPosterPath(movieInfo.PosterPath),
                Title = movieInfo.Title,
                ReleaseDate = !string.IsNullOrEmpty(movieInfo.ReleaseDate)
                    ? DateTime.Parse(movieInfo.ReleaseDate)
                    : DateTime.MinValue,
                Status = movieInfo.Status,
                RequestedDate = DateTime.UtcNow,
                Approved = false,
                RequestedUserId = canRequestOnBehalf ? model.RequestOnBehalf : userDetails.Id,
                Background = movieInfo.BackdropPath,
                LangCode = model.LanguageCode,
                RequestedByAlias = model.RequestedByAlias,
                RootPathOverride = model.RootFolderOverride.GetValueOrDefault(),
                QualityOverride = model.QualityPathOverride.GetValueOrDefault()
            };

            var usDates = movieInfo.ReleaseDates?.Results?.FirstOrDefault(x => x.IsoCode == "US");
            requestModel.DigitalReleaseDate = usDates?.ReleaseDate
                ?.FirstOrDefault(x => x.Type == ReleaseDateType.Digital)?.ReleaseDate;

            var ruleResults = (await RunRequestRules(requestModel)).ToList();
            if (ruleResults.Any(x => !x.Success))
            {
                return new RequestEngineResult
                {
                    ErrorMessage = ruleResults.FirstOrDefault(x => x.Message.HasValue()).Message
                };
            }

            if (requestModel.Approved) // The rules have auto approved this
            {
                var requestEngineResult = await AddMovieRequest(requestModel, fullMovieName, model.RequestOnBehalf);
                if (requestEngineResult.Result)
                {
                    var result = await ApproveMovie(requestModel);
                    if (result.IsError)
                    {
                        Logger.LogWarning("Tried auto sending movie but failed. Message: {0}", result.Message);
                        return new RequestEngineResult
                        {
                            Message = result.Message,
                            ErrorMessage = result.Message,
                            Result = false
                        };
                    }

                    return requestEngineResult;
                }

                // If there are no providers then it's successful but movie has not been sent
            }

            return await AddMovieRequest(requestModel, fullMovieName, model.RequestOnBehalf);
        }


        /// <summary>
        /// Gets the requests.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <param name="position">The position.</param>
        /// <param name="orderFilter">The order/filter type.</param>
        /// <returns></returns>
        public async Task<RequestsViewModel<MovieRequests>> GetRequests(int count, int position,
            OrderFilterModel orderFilter)
        {
            var shouldHide = await HideFromOtherUsers();
            IQueryable<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests =
                    MovieRepository.GetWithUser(shouldHide
                        .UserId); //.Skip(position).Take(count).OrderByDescending(x => x.ReleaseDate).ToListAsync();
            }
            else
            {
                allRequests =
                    MovieRepository
                        .GetWithUser(); //.Skip(position).Take(count).OrderByDescending(x => x.ReleaseDate).ToListAsync();
            }

            switch (orderFilter.AvailabilityFilter)
            {
                case FilterType.None:
                    break;
                case FilterType.Available:
                    allRequests = allRequests.Where(x => x.Available);
                    break;
                case FilterType.NotAvailable:
                    allRequests = allRequests.Where(x => !x.Available);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (orderFilter.StatusFilter)
            {
                case FilterType.None:
                    break;
                case FilterType.Approved:
                    allRequests = allRequests.Where(x => x.Approved);
                    break;
                case FilterType.Processing:
                    allRequests = allRequests.Where(x => x.Approved && !x.Available);
                    break;
                case FilterType.PendingApproval:
                    allRequests = allRequests.Where(x => !x.Approved && !x.Available && !(x.Denied ?? false));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var total = allRequests.Count();

            var requests = await (OrderMovies(allRequests, orderFilter.OrderType)).Skip(position).Take(count)
                .ToListAsync();

            await CheckForSubscription(shouldHide, requests);
            return new RequestsViewModel<MovieRequests>
            {
                Collection = requests,
                Total = total
            };
        }

        public async Task<RequestsViewModel<MovieRequests>> GetRequests(int count, int position, string sortProperty, string sortOrder)
        {
            var shouldHide = await HideFromOtherUsers();
            IQueryable<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests =
                    MovieRepository.GetWithUser(shouldHide
                        .UserId);
            }
            else
            {
                allRequests =
                    MovieRepository
                        .GetWithUser();
            }

            var prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(sortProperty, true);

            if (sortProperty.Contains('.'))
            {
                // This is a navigation property currently not supported
                prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find("RequestedDate", true);
                //var properties = sortProperty.Split(new []{'.'}, StringSplitOptions.RemoveEmptyEntries);
                //var firstProp = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(properties[0], true);
                //var propType = firstProp.PropertyType;
                //var secondProp = TypeDescriptor.GetProperties(propType).Find(properties[1], true);
            }

            // TODO fix this so we execute this on the server
            var requests = sortOrder.Equals("asc", StringComparison.InvariantCultureIgnoreCase)
                ? allRequests.ToList().OrderBy(x => prop.GetValue(x)).ToList()
                : allRequests.ToList().OrderByDescending(x => prop.GetValue(x)).ToList();
            var total = requests.Count();
            requests = requests.Skip(position).Take(count).ToList();

            await CheckForSubscription(shouldHide, requests);
            return new RequestsViewModel<MovieRequests>
            {
                Collection = requests,
                Total = total
            };
        }

        public async Task<RequestsViewModel<MovieRequests>> GetRequestsByStatus(int count, int position, string sortProperty, string sortOrder, RequestStatus status)
        {
            var shouldHide = await HideFromOtherUsers();
            IQueryable<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests =
                    MovieRepository.GetWithUser(shouldHide
                        .UserId);
            }
            else
            {
                allRequests =
                    MovieRepository
                        .GetWithUser();
            }

            switch (status)
            {
                case RequestStatus.PendingApproval:
                    allRequests = allRequests.Where(x => !x.Approved && !x.Available && (!x.Denied.HasValue || !x.Denied.Value));
                    break;
                case RequestStatus.ProcessingRequest:
                    allRequests = allRequests.Where(x => x.Approved && !x.Available && (!x.Denied.HasValue || !x.Denied.Value));
                    break;
                case RequestStatus.Available:
                    allRequests = allRequests.Where(x => x.Available);
                    break;
                case RequestStatus.Denied:
                    allRequests = allRequests.Where(x => x.Denied.HasValue && x.Denied.Value && !x.Available);
                    break;
                default:
                    break;
            }

            var prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(sortProperty, true);

            if (sortProperty.Contains('.'))
            {
                // This is a navigation property currently not supported
                prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find("RequestedDate", true);
                //var properties = sortProperty.Split(new []{'.'}, StringSplitOptions.RemoveEmptyEntries);
                //var firstProp = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(properties[0], true);
                //var propType = firstProp.PropertyType;
                //var secondProp = TypeDescriptor.GetProperties(propType).Find(properties[1], true);
            }

            // TODO fix this so we execute this on the server
            var requests = sortOrder.Equals("asc", StringComparison.InvariantCultureIgnoreCase)
                ? allRequests.ToList().OrderBy(x => prop.GetValue(x)).ToList()
                : allRequests.ToList().OrderByDescending(x => prop.GetValue(x)).ToList();
            var total = requests.Count();
            requests = requests.Skip(position).Take(count).ToList();

            await CheckForSubscription(shouldHide, requests);
            return new RequestsViewModel<MovieRequests>
            {
                Collection = requests,
                Total = total
            };
        }

        public async Task<RequestsViewModel<MovieRequests>> GetUnavailableRequests(int count, int position, string sortProperty, string sortOrder)
        {
            var shouldHide = await HideFromOtherUsers();
            IQueryable<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests =
                    MovieRepository.GetWithUser(shouldHide
                        .UserId).Where(x => !x.Available && x.Approved);
            }
            else
            {
                allRequests =
                    MovieRepository
                        .GetWithUser().Where(x => !x.Available && x.Approved);
            }

            var prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(sortProperty, true);

            if (sortProperty.Contains('.'))
            {
                // This is a navigation property currently not supported
                prop = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find("RequestedDate", true);
                //var properties = sortProperty.Split(new []{'.'}, StringSplitOptions.RemoveEmptyEntries);
                //var firstProp = TypeDescriptor.GetProperties(typeof(MovieRequests)).Find(properties[0], true);
                //var propType = firstProp.PropertyType;
                //var secondProp = TypeDescriptor.GetProperties(propType).Find(properties[1], true);
            }

            var requests = (sortOrder.Equals("asc", StringComparison.InvariantCultureIgnoreCase)
                ? allRequests.ToList().OrderBy(x => prop.GetValue(x))
                : allRequests.ToList().OrderByDescending(x => prop.GetValue(x))).ToList();
            var total = requests.Count();
            requests = requests.Skip(position).Take(count).ToList();

            await CheckForSubscription(shouldHide, requests);
            return new RequestsViewModel<MovieRequests>
            {
                Collection = requests,
                Total = total
            };
        }

        public async Task<RequestEngineResult> UpdateAdvancedOptions(MediaAdvancedOptions options)
        {
            var request = await MovieRepository.Find(options.RequestId);
            if (request == null)
            {
                return new RequestEngineResult
                {
                    Result = false,
                    ErrorMessage = "Request does not exist"
                };
            }

            request.QualityOverride = options.QualityOverride;
            request.RootPathOverride = options.RootPathOverride;

            await MovieRepository.Update(request);

            return new RequestEngineResult
            {
                Result = true
            };
        }

        private IQueryable<MovieRequests> OrderMovies(IQueryable<MovieRequests> allRequests, OrderType type)
        {
            switch (type)
            {
                case OrderType.RequestedDateAsc:
                    return allRequests.OrderBy(x => x.RequestedDate);
                case OrderType.RequestedDateDesc:
                    return allRequests.OrderByDescending(x => x.RequestedDate);
                case OrderType.TitleAsc:
                    return allRequests.OrderBy(x => x.Title);
                case OrderType.TitleDesc:
                    return allRequests.OrderByDescending(x => x.Title);
                case OrderType.StatusAsc:
                    return allRequests.OrderBy(x => x.Status);
                case OrderType.StatusDesc:
                    return allRequests.OrderByDescending(x => x.Status);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public async Task<int> GetTotal()
        {
            var shouldHide = await HideFromOtherUsers();
            if (shouldHide.Hide)
            {
                return await MovieRepository.GetWithUser(shouldHide.UserId).CountAsync();
            }
            else
            {
                return await MovieRepository.GetWithUser().CountAsync();
            }
        }

        /// <summary>
        /// Gets the requests.
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<MovieRequests>> GetRequests()
        {
            var shouldHide = await HideFromOtherUsers();
            List<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests = await MovieRepository.GetWithUser(shouldHide.UserId).ToListAsync();
            }
            else
            {
                allRequests = await MovieRepository.GetWithUser().ToListAsync();
            }

            await CheckForSubscription(shouldHide, allRequests);

            return allRequests;
        }

        public async Task<MovieRequests> GetRequest(int requestId)
        {
            var request = await MovieRepository.GetWithUser().Where(x => x.Id == requestId).FirstOrDefaultAsync();
            await CheckForSubscription(new HideResult(), new List<MovieRequests> { request });

            return request;
        }

        private async Task CheckForSubscription(HideResult shouldHide, List<MovieRequests> movieRequests)
        {
            var requestIds = movieRequests.Select(x => x.Id);
            var sub = await _subscriptionRepository.GetAll().Where(s =>
                s.UserId == shouldHide.UserId && requestIds.Contains(s.RequestId) && s.RequestType == RequestType.Movie)
                .ToListAsync();
            foreach (var x in movieRequests)
            {
                x.PosterPath = PosterPathHelper.FixPosterPath(x.PosterPath);
                if (shouldHide.UserId == x.RequestedUserId)
                {
                    x.ShowSubscribe = false;
                }
                else
                {
                    x.ShowSubscribe = true;
                    var hasSub = sub.FirstOrDefault(r => r.RequestId == x.Id);
                    x.Subscribed = hasSub != null;
                }
            }
        }

        /// <summary>
        /// Searches the movie request.
        /// </summary>
        /// <param name="search">The search.</param>
        /// <returns></returns>
        public async Task<IEnumerable<MovieRequests>> SearchMovieRequest(string search)
        {
            var shouldHide = await HideFromOtherUsers();
            List<MovieRequests> allRequests;
            if (shouldHide.Hide)
            {
                allRequests = await MovieRepository.GetWithUser(shouldHide.UserId).ToListAsync();
            }
            else
            {
                allRequests = await MovieRepository.GetWithUser().ToListAsync();
            }

            var results = allRequests.Where(x => x.Title.Contains(search, CompareOptions.IgnoreCase)).ToList();
            await CheckForSubscription(shouldHide, results);

            return results;
        }

        public async Task<RequestEngineResult> ApproveMovieById(int requestId)
        {
            var request = await MovieRepository.Find(requestId);
            return await ApproveMovie(request);
        }

        public async Task<RequestEngineResult> DenyMovieById(int modelId, string denyReason)
        {
            var request = await MovieRepository.Find(modelId);
            if (request == null)
            {
                return new RequestEngineResult
                {
                    ErrorMessage = "Request does not exist"
                };
            }

            request.Denied = true;
            request.DeniedReason = denyReason;
            // We are denying a request
            await NotificationHelper.Notify(request, NotificationType.RequestDeclined);
            await MovieRepository.Update(request);
            await _mediaCacheService.Purge();

            return new RequestEngineResult
            {
                Result = true,
                Message = "Request successfully deleted",
            };
        }

        public async Task<RequestEngineResult> ApproveMovie(MovieRequests request)
        {
            if (request == null)
            {
                return new RequestEngineResult
                {
                    ErrorMessage = "Request does not exist"
                };
            }

            request.MarkedAsApproved = DateTime.Now;
            request.Approved = true;
            request.Denied = false;
            await MovieRepository.Update(request);

            var canNotify = await RunSpecificRule(request, SpecificRules.CanSendNotification, string.Empty);
            if (canNotify.Success)
            {
                await NotificationHelper.Notify(request, NotificationType.RequestApproved);
            }
            await _mediaCacheService.Purge();

            return await ProcessSendingMovie(request);
        }

        public async Task<RequestEngineResult> RequestCollection(int collectionId, CancellationToken cancellationToken)
        {
            var langCode = await DefaultLanguageCode(null);
            var collections = await Cache.GetOrAddAsync($"GetCollection{collectionId}{langCode}",
                () =>  MovieApi.GetCollection(langCode, collectionId, cancellationToken), DateTimeOffset.Now.AddDays(1));

            var results = new List<RequestEngineResult>();
            foreach (var collection in collections.parts)
            {
                results.Add(await RequestMovie(new MovieRequestViewModel
                {
                    TheMovieDbId = collection.id
                }));
            }


            if (results.All(x => x.IsError))
            {
                new RequestEngineResult { Result = false, ErrorMessage = $"The whole collection {collections.name} Is already monitored or requested!" };
            }

            return new RequestEngineResult { Result = true, Message = $"The collection {collections.name} has been successfully added!", RequestId = results.FirstOrDefault().RequestId};
        }

        private async Task<RequestEngineResult> ProcessSendingMovie(MovieRequests request)
        {
            if (request.Approved)
            {
                var result = await Sender.Send(request);
                if (result.Success && result.Sent)
                {
                    return new RequestEngineResult
                    {
                        Result = true
                    };
                }

                if (!result.Success)
                {
                    Logger.LogWarning("Tried auto sending movie but failed. Message: {0}", result.Message);
                    return new RequestEngineResult
                    {
                        Message = result.Message,
                        ErrorMessage = result.Message,
                        Result = false
                    };
                }

                // If there are no providers then it's successful but movie has not been sent
            }

            return new RequestEngineResult
            {
                Result = true
            };
        }

        /// <summary>
        /// Updates the movie request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        public async Task<MovieRequests> UpdateMovieRequest(MovieRequests request)
        {
            var allRequests = await MovieRepository.GetWithUser().ToListAsync();
            var results = allRequests.FirstOrDefault(x => x.Id == request.Id);

            results.Approved = request.Approved;
            results.Available = request.Available;
            results.Denied = request.Denied;
            results.DeniedReason = request.DeniedReason;
            results.ImdbId = request.ImdbId;
            results.IssueId = request.IssueId;
            results.Issues = request.Issues;
            results.Overview = request.Overview;
            results.PosterPath = PosterPathHelper.FixPosterPath(request.PosterPath);
            results.QualityOverride = request.QualityOverride;
            results.RootPathOverride = request.RootPathOverride;

            await MovieRepository.Update(results);
            await _mediaCacheService.Purge();
            return results;
        }

        /// <summary>
        /// Removes the movie request.
        /// </summary>
        /// <param name="requestId">The request identifier.</param>
        /// <returns></returns>
        public async Task RemoveMovieRequest(int requestId)
        {
            var request = await MovieRepository.GetAll().FirstOrDefaultAsync(x => x.Id == requestId);
            await MovieRepository.Delete(request);
            await _mediaCacheService.Purge();
        }

        public async Task RemoveAllMovieRequests()
        {
            var request = MovieRepository.GetAll();
            await MovieRepository.DeleteRange(request);
            await _mediaCacheService.Purge();
        }

        public async Task<bool> UserHasRequest(string userId)
        {
            return await MovieRepository.GetAll().AnyAsync(x => x.RequestedUserId == userId);
        }

        public async Task<RequestEngineResult> ReProcessRequest(int requestId, CancellationToken cancellationToken)
        {
            var request = await MovieRepository.Find(requestId);
            if (request == null)
            {
                return new RequestEngineResult
                {
                    Result = false,
                    ErrorMessage = "Request does not exist"
                };
            }

            return await ProcessSendingMovie(request);
        }

        public async Task<RequestEngineResult> MarkUnavailable(int modelId)
        {
            var request = await MovieRepository.Find(modelId);
            if (request == null)
            {
                return new RequestEngineResult
                {
                    ErrorMessage = "Request does not exist"
                };
            }

            request.Available = false;
            await MovieRepository.Update(request);
            await _mediaCacheService.Purge();

            return new RequestEngineResult
            {
                Message = "Request is now unavailable",
                Result = true
            };
        }

        public async Task<RequestEngineResult> MarkAvailable(int modelId)
        {
            var request = await MovieRepository.Find(modelId);
            if (request == null)
            {
                return new RequestEngineResult
                {
                    ErrorMessage = "Request does not exist"
                };
            }

            request.Available = true;
            request.MarkedAsAvailable = DateTime.Now;
            await NotificationHelper.Notify(request, NotificationType.RequestAvailable);
            await MovieRepository.Update(request);
            await _mediaCacheService.Purge();

            return new RequestEngineResult
            {
                Message = "Request is now available",
                Result = true
            };
        }

        private async Task<RequestEngineResult> AddMovieRequest(MovieRequests model, string movieName, string requestOnBehalf)
        {
            await MovieRepository.Add(model);

            var result = await RunSpecificRule(model, SpecificRules.CanSendNotification, requestOnBehalf);
            if (result.Success)
            {
                await NotificationHelper.NewRequest(model);
            }

            await _mediaCacheService.Purge();

            await _requestLog.Add(new RequestLog
            {
                UserId = requestOnBehalf.HasValue() ? requestOnBehalf : (await GetUser()).Id,
                RequestDate = DateTime.UtcNow,
                RequestId = model.Id,
                RequestType = RequestType.Movie,
            });

            return new RequestEngineResult { Result = true, Message = $"{movieName} has been successfully added!", RequestId = model.Id };
        }

        public async Task<RequestQuotaCountModel> GetRemainingRequests(OmbiUser user)
        {
            if (user == null)
            {
                user = await GetUser();

                // If user is still null after attempting to get the logged in user, return null.
                if (user == null)
                {
                    return null;
                }
            }

            int limit = user.MovieRequestLimit ?? 0;

            if (limit <= 0)
            {
                return new RequestQuotaCountModel()
                {
                    HasLimit = false,
                    Limit = 0,
                    Remaining = 0,
                    NextRequest = DateTime.Now,
                };
            }

            IQueryable<RequestLog> log = _requestLog.GetAll().Where(x => x.UserId == user.Id && x.RequestType == RequestType.Movie);

            int count = limit - await log.CountAsync(x => x.RequestDate >= DateTime.UtcNow.AddDays(-7));

            DateTime oldestRequestedAt = await log.Where(x => x.RequestDate >= DateTime.UtcNow.AddDays(-7))
                                            .OrderBy(x => x.RequestDate)
                                            .Select(x => x.RequestDate)
                                            .FirstOrDefaultAsync();

            return new RequestQuotaCountModel()
            {
                HasLimit = true,
                Limit = limit,
                Remaining = count,
                NextRequest = DateTime.SpecifyKind(oldestRequestedAt.AddDays(7), DateTimeKind.Utc),
            };
        }
    }
}