using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.EventHandlers;

/// <summary>
/// Handles playback start events to remove items from denylist when resumed.
/// </summary>
public class PlaybackStartConsumer : IEventConsumer<PlaybackStartEventArgs>
{
    private readonly ILogger<PlaybackStartConsumer> _logger;
    private readonly ISessionManager _sessionManager;
    private readonly DenylistManager _denylistManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackStartConsumer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="denylistManager">The denylist manager.</param>
    public PlaybackStartConsumer(
        ILogger<PlaybackStartConsumer> logger,
        ISessionManager sessionManager,
        DenylistManager denylistManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _denylistManager = denylistManager;
    }

    /// <inheritdoc />
    public Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.Session == null || eventArgs.Item == null)
            {
                return Task.CompletedTask;
            }

            var userId = eventArgs.Session.UserId;

            // the gid looks like this: c7693687-4572-b346-5467-989d3cf84d39, so we remove the dashes
            var itemId = eventArgs.Item.Id.ToString();
            itemId = itemId.Replace("-", string.Empty, StringComparison.Ordinal);


            // Check if this item is in the user's denylist
            if (_denylistManager.IsItemInUserDenylist(userId, itemId))
            {
                _logger.LogInformation(
                    "User {UserId} started playback of item {ItemId} which was in denylist. Removing from denylist.",
                    userId,
                    itemId);

                // Remove the item from the denylist since user is watching it again
                _denylistManager.RemoveFromUserDenylist(userId, itemId);
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId} started playback of item {ItemId} which is not in denylist.",
                    userId,
                    itemId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback started event");
        }

        return Task.CompletedTask;
    }
}
