using System.Collections.ObjectModel;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscontinueWatching.Configuration;

/// <summary>
/// Represents a user's denylist entry for XML serialization.
/// </summary>
public class UserDenylistEntry
{
    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the list of denylisted item IDs.
    /// </summary>
    public Collection<string> ItemIds { get; } = new Collection<string>();
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        UserDenylistEntries = new Collection<UserDenylistEntry>();
    }

    /// <summary>
    /// Gets or sets the user-specific denylist entries.
    /// </summary>
    public Collection<UserDenylistEntry> UserDenylistEntries { get; }

    /// <summary>
    /// Gets or sets the number of days after which items should be removed from Continue Watching.
    /// </summary>
    public int DaysThreshold { get; set; } = 180;
}
