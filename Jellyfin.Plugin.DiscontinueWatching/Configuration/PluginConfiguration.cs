using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DiscontinueWatching.Configuration;

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
        UserDenylists = new Dictionary<Guid, HashSet<Guid>>();
    }

    /// <summary>
    /// Gets the user-specific denylists. Key is UserId, Value is set of ItemIds.
    /// </summary>
    public Dictionary<Guid, HashSet<Guid>> UserDenylists { get; }
}
