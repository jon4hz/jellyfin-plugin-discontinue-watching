using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.Services;

/// <summary>
/// Manages the user-specific denylist for items to hide from Continue Watching.
/// </summary>
public class DenylistManager(ILogger<DenylistManager> logger)
{
    private readonly ILogger<DenylistManager> _logger = logger;

    /// <summary>
    /// Adds an item to the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to add to the denylist.</param>
    public void AddToUserDenylist(Guid userId, string itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return;
        }

        var itemList = config.UserDenylists.GetOrAdd(userId, _ => new System.Collections.ObjectModel.Collection<string>());

        lock (itemList)
        {
            if (!itemList.Contains(itemId))
            {
                itemList.Add(itemId);
                DiscontinueWatchingPlugin.Instance?.SaveConfiguration();
                _logger.LogInformation("Added item {ItemId} to denylist for user {UserId}", itemId, userId);
            }
            else
            {
                _logger.LogDebug("Item {ItemId} already exists in denylist for user {UserId}", itemId, userId);
            }
        }
    }

    /// <summary>
    /// Removes an item from the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to remove from the denylist.</param>
    public void RemoveFromUserDenylist(Guid userId, string itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return;
        }

        if (config.UserDenylists.TryGetValue(userId, out var itemList))
        {
            lock (itemList)
            {
                if (itemList.Remove(itemId))
                {
                    // Remove the user entry if the list is empty
                    if (itemList.Count == 0)
                    {
                        config.UserDenylists.TryRemove(userId, out _);
                    }

                    DiscontinueWatchingPlugin.Instance?.SaveConfiguration();
                    _logger.LogInformation("Removed item {ItemId} from denylist for user {UserId}", itemId, userId);
                }
                else
                {
                    _logger.LogDebug("Item {ItemId} was not in denylist for user {UserId}", itemId, userId);
                }
            }
        }
        else
        {
            _logger.LogDebug("User {UserId} has no denylist entries", userId);
        }
    }

    /// <summary>
    /// Gets all items in the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A list of item IDs in the user's denylist.</returns>
    public IReadOnlyList<string> GetUserDenylist(Guid userId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return Array.Empty<string>();
        }

        if (config.UserDenylists.TryGetValue(userId, out var itemList))
        {
            lock (itemList)
            {
                return itemList.ToArray();
            }
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Checks if an item is in the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to check.</param>
    /// <returns>True if the item is in the denylist; otherwise, false.</returns>
    public bool IsItemInUserDenylist(Guid userId, string itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            return false;
        }

        if (config.UserDenylists.TryGetValue(userId, out var itemList))
        {
            lock (itemList)
            {
                return itemList.Contains(itemId);
            }
        }

        return false;
    }
}
