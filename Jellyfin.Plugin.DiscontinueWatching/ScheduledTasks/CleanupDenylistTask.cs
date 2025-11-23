using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.ScheduledTasks;

/// <summary>
/// Scheduled task to clean up non-existent items from user denylists.
/// </summary>
public class CleanupDenylistTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly DenylistManager _denylistManager;
    private readonly ILogger<CleanupDenylistTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CleanupDenylistTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="denylistManager">The denylist manager.</param>
    /// <param name="logger">The logger.</param>
    public CleanupDenylistTask(
        ILibraryManager libraryManager,
        DenylistManager denylistManager,
        ILogger<CleanupDenylistTask> logger)
    {
        _libraryManager = libraryManager;
        _denylistManager = denylistManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Clean Up Denylist";

    /// <inheritdoc />
    public string Key => "DiscontinueWatchingDenylistCleanup";

    /// <inheritdoc />
    public string Description => "Removes items from user denylists that no longer exist on the server.";

    /// <inheritdoc />
    public string Category => "DiscontinueWatching";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting denylist cleanup task");

        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return Task.CompletedTask;
        }

        var userDenylistEntries = config.UserDenylistEntries.ToList();
        var totalEntries = userDenylistEntries.Count;

        if (totalEntries == 0)
        {
            _logger.LogInformation("No denylist entries found");
            return Task.CompletedTask;
        }

        var processedEntries = 0;
        var totalItemsRemoved = 0;

        foreach (var entry in userDenylistEntries)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Denylist cleanup task cancelled");
                return Task.CompletedTask;
            }

            try
            {
                _logger.LogDebug("Processing denylist for user {UserId}", entry.UserId);

                var itemsToRemove = new List<string>();

                // Check each item in the user's denylist
                foreach (var itemId in entry.ItemIds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Convert itemId back to GUID format (add dashes)
                    // ItemIds are stored without dashes, but GetItemById expects GUID format
                    if (!Guid.TryParse(itemId, out var itemGuid))
                    {
                        _logger.LogWarning("Invalid item ID format in denylist: {ItemId}", itemId);
                        itemsToRemove.Add(itemId);
                        continue;
                    }

                    // Check if item exists in library
                    var item = _libraryManager.GetItemById(itemGuid);
                    if (item == null)
                    {
                        _logger.LogDebug("Item {ItemId} no longer exists, marking for removal from denylist", itemId);
                        itemsToRemove.Add(itemId);
                    }
                    else
                    {
                        _logger.LogDebug("Item {ItemId} exists, no action needed", itemId);
                    }
                }

                // Remove non-existent items from the denylist
                foreach (var itemId in itemsToRemove)
                {
                    _denylistManager.RemoveFromUserDenylist(entry.UserId, itemId);
                    totalItemsRemoved++;
                    _logger.LogInformation("Removed non-existent item {ItemId} from denylist for user {UserId}", itemId, entry.UserId);
                }

                processedEntries++;
                var progressPercent = (double)processedEntries / totalEntries * 100;
                progress.Report(progressPercent);

                _logger.LogDebug("Processed denylist for user {UserId}, removed {Count} items", entry.UserId, itemsToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing denylist for user {UserId}", entry.UserId);
            }
        }

        _logger.LogInformation("Denylist cleanup completed. Processed {EntryCount} denylists, removed {ItemCount} non-existent items", totalEntries, totalItemsRemoved);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run daily at 4 AM (one hour after the Continue Watching cleanup task)
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
            }
        };
    }
}
