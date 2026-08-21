using System.Reflection;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.ScheduledTasks;

/// <summary>
/// Scheduled task to clean up old items from Continue Watching.
/// </summary>
public class CleanupContinueWatchingTask : IScheduledTask
{
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;
    private readonly DenylistManager _denylistManager;
    private readonly ILogger<CleanupContinueWatchingTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupContinueWatchingTask"/> class.
    /// </summary>
    /// <param name="userManager">The user manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="denylistManager">The denylist manager.</param>
    /// <param name="logger">The logger.</param>
    public CleanupContinueWatchingTask(
        IUserManager userManager,
        IUserDataManager userDataManager,
        ILibraryManager libraryManager,
        DenylistManager denylistManager,
        ILogger<CleanupContinueWatchingTask> logger)
    {
        _userManager = userManager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
        _denylistManager = denylistManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Clean Up Continue Watching";

    /// <inheritdoc />
    public string Key => "DiscontinueWatchingCleanup";

    /// <inheritdoc />
    public string Description => "Removes items from Continue Watching that haven't been watched in the configured threshold period.";

    /// <inheritdoc />
    public string Category => "DiscontinueWatching";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Continue Watching cleanup task");

        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return;
        }

        var daysThreshold = config.DaysThreshold;
        var thresholdDate = DateTime.UtcNow.AddDays(-daysThreshold);

        _logger.LogInformation("Cleaning up items not watched since {ThresholdDate} (threshold: {Days} days)", thresholdDate, daysThreshold);

        var userIds = GetUserIds();
        var totalUsers = userIds.Count;
        var processedUsers = 0;
        var totalItemsHidden = 0;

        foreach (var userId in userIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Cleanup task cancelled");
                return;
            }

            try
            {
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    continue;
                }

                _logger.LogDebug("Processing user {UserId}: {UserName}", user.Id, user.Username);

                var itemsHidden = await ProcessUserContinueWatching(user.Id, thresholdDate, cancellationToken).ConfigureAwait(false);
                totalItemsHidden += itemsHidden;

                processedUsers++;
                var progressPercent = (double)processedUsers / totalUsers * 100;
                progress.Report(progressPercent);

                _logger.LogDebug("Processed user {UserId}, hidden {Count} items", user.Id, itemsHidden);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user {UserId}", userId);
            }
        }

        _logger.LogInformation("Continue Watching cleanup completed. Processed {UserCount} users, hidden {ItemCount} items", totalUsers, totalItemsHidden);
    }

    /// <summary>
    /// Gets user IDs using reflection to support multiple Jellyfin versions.
    /// The IUserManager interface changed between Jellyfin releases, causing
    /// MissingMethodException when compiled against one SDK version but run on another.
    /// This method tries multiple known APIs in order of preference.
    /// </summary>
    /// <returns>A list of user GUIDs.</returns>
    private List<Guid> GetUserIds()
    {
        var managerType = _userManager.GetType();

        // Try UsersIds property (returns IEnumerable<Guid>)
        var usersIdsProperty = managerType.GetProperty("UsersIds");
        if (usersIdsProperty != null)
        {
            try
            {
                var result = usersIdsProperty.GetValue(_userManager);
                if (result is IEnumerable<Guid> guids)
                {
                    _logger.LogDebug("Using UsersIds property to enumerate users");
                    return guids.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UsersIds property failed, trying alternatives");
            }
        }

        // Try GetUsers() method (used by some Jellyfin versions)
        var getUsersMethod = managerType.GetMethod("GetUsers", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (getUsersMethod != null)
        {
            try
            {
                var result = getUsersMethod.Invoke(_userManager, null);
                if (result is System.Collections.IEnumerable users)
                {
                    _logger.LogDebug("Using GetUsers() method to enumerate users");
                    var ids = new List<Guid>();
                    foreach (var user in users)
                    {
                        var idProp = user.GetType().GetProperty("Id");
                        if (idProp?.GetValue(user) is Guid id)
                        {
                            ids.Add(id);
                        }
                    }

                    return ids;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GetUsers() method failed, trying alternatives");
            }
        }

        // Try Users property (returns IEnumerable<User> or similar)
        var usersProperty = managerType.GetProperty("Users");
        if (usersProperty != null)
        {
            try
            {
                var result = usersProperty.GetValue(_userManager);
                if (result is System.Collections.IEnumerable users)
                {
                    _logger.LogDebug("Using Users property to enumerate users");
                    var ids = new List<Guid>();
                    foreach (var user in users)
                    {
                        var idProp = user.GetType().GetProperty("Id");
                        if (idProp?.GetValue(user) is Guid id)
                        {
                            ids.Add(id);
                        }
                    }

                    return ids;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Users property failed");
            }
        }

        _logger.LogError("Could not find any method to enumerate users on IUserManager. Available members: {Members}",
            string.Join(", ", managerType.GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name)));
        return new List<Guid>();
    }

    /// <summary>
    /// Process a single user's Continue Watching items.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="thresholdDate">The threshold date.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of items hidden.</returns>
    private Task<int> ProcessUserContinueWatching(Guid userId, DateTime thresholdDate, CancellationToken cancellationToken)
    {
        var itemsHidden = 0;

        try
        {
            // Get items that are in progress for this user
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return Task.FromResult(0);
            }

            // mimics what jellyfin does in GetTvResume and GetMovieResume
            var query = new InternalItemsQuery
            {
                User = user,
                IsResumable = true,
                Recursive = true,
                OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending), (ItemSortBy.SortName, SortOrder.Descending) },
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
                Limit = 50  // GetSpecialItemsLimit
            };

            var inProgressItems = _libraryManager.GetItemList(query);
            _logger.LogDebug("Found {Count} in-progress items for user {UserId}", inProgressItems.Count, userId);

            foreach (var item in inProgressItems)
            {
                _logger.LogDebug("Checking item {ItemId} for user {UserId}", item.Id, userId);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var userData = _userDataManager.GetUserData(user, item);

                // Check if item has been played and has a last played date
                if (userData != null && userData.PlaybackPositionTicks > 0 && userData.LastPlayedDate.HasValue)
                {
                    _logger.LogDebug(
                        "Item {ItemId} last played on {LastPlayed} for user {UserId}",
                        item.Id,
                        userData.LastPlayedDate.Value,
                        userId);

                    // Check if last played date is older than threshold
                    if (userData.LastPlayedDate.Value < thresholdDate)
                    {
                        // Get item ID without dashes (matching the format used in the API)
                        var itemId = item.Id.ToString().Replace("-", string.Empty, StringComparison.Ordinal);

                        // Check if item is already in denylist
                        if (!_denylistManager.IsItemInUserDenylist(userId, itemId))
                        {
                            _denylistManager.AddToUserDenylist(userId, itemId);
                            itemsHidden++;

                            _logger.LogDebug(
                                "Hidden item {ItemId} for user {UserId}. Last played: {LastPlayed}",
                                itemId,
                                userId,
                                userData.LastPlayedDate.Value);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Continue Watching for user {UserId}", userId);
            throw;
        }

        return Task.FromResult(itemsHidden);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run daily at 3 AM
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }
}
