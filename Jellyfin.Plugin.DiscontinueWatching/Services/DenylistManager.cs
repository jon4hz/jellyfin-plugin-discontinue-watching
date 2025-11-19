using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.Services;

/// <summary>
/// Manages the user-specific denylist for items to hide from Continue Watching.
/// </summary>
public class DenylistManager
{
    private readonly ILogger<DenylistManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DenylistManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DenylistManager(ILogger<DenylistManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds an item to the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to add to the denylist.</param>
    public void AddToUserDenylist(Guid userId, Guid itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return;
        }

        if (!config.UserDenylists.TryGetValue(userId, out var denylist))
        {
            denylist = new HashSet<Guid>();
            config.UserDenylists[userId] = denylist;
        }

        if (denylist.Add(itemId))
        {
            DiscontinueWatchingPlugin.Instance?.SaveConfiguration();
            _logger.LogInformation("Added item {ItemId} to denylist for user {UserId}", itemId, userId);
        }
        else
        {
            _logger.LogDebug("Item {ItemId} already exists in denylist for user {UserId}", itemId, userId);
        }
    }

    /// <summary>
    /// Removes an item from the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to remove from the denylist.</param>
    public void RemoveFromUserDenylist(Guid userId, Guid itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return;
        }

        if (config.UserDenylists.TryGetValue(userId, out var denylist))
        {
            if (denylist.Remove(itemId))
            {
                DiscontinueWatchingPlugin.Instance?.SaveConfiguration();
                _logger.LogInformation("Removed item {ItemId} from denylist for user {UserId}", itemId, userId);
            }
            else
            {
                _logger.LogDebug("Item {ItemId} was not in denylist for user {UserId}", itemId, userId);
            }
        }
    }

    /// <summary>
    /// Gets all items in the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A list of item IDs in the user's denylist.</returns>
    public IReadOnlyList<Guid> GetUserDenylist(Guid userId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("Plugin configuration is not available");
            return Array.Empty<Guid>();
        }

        if (config.UserDenylists.TryGetValue(userId, out var denylist))
        {
            return denylist.ToList();
        }

        return Array.Empty<Guid>();
    }

    /// <summary>
    /// Checks if an item is in the user's denylist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="itemId">The item ID to check.</param>
    /// <returns>True if the item is in the denylist; otherwise, false.</returns>
    public bool IsItemInUserDenylist(Guid userId, Guid itemId)
    {
        var config = DiscontinueWatchingPlugin.Instance?.Configuration;
        if (config == null)
        {
            return false;
        }

        return config.UserDenylists.TryGetValue(userId, out var denylist) && denylist.Contains(itemId);
    }
}
