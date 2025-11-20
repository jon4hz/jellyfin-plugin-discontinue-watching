using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.DiscontinueWatching.Api;

/// <summary>
/// API controller for managing the Continue Watching denylist.
/// </summary>
[ApiController]
[Route("DiscontinueWatching")]
[Authorize]
public class DiscontinueWatchingController : ControllerBase
{
    private readonly ILogger<DiscontinueWatchingController> _logger;
    private readonly DenylistManager _denylistManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscontinueWatchingController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="denylistManager">The denylist manager.</param>
    public DiscontinueWatchingController(
        ILogger<DiscontinueWatchingController> logger,
        DenylistManager denylistManager)
    {
        _logger = logger;
        _denylistManager = denylistManager;
    }

    /// <summary>
    /// Adds an item to the user's denylist.
    /// </summary>
    /// <param name="itemId">The item ID to add to the denylist.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Item successfully added to denylist.</response>
    /// <response code="400">Invalid item ID.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpPost("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult AddItemToDenylist([FromRoute, Required] string itemId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Unable to determine user ID from request");
            return Unauthorized();
        }

        _logger.LogDebug("Adding item {ItemId} to denylist for user {UserId}", itemId, userId);
        _denylistManager.AddToUserDenylist(userId, itemId);
        return NoContent();
    }

    /// <summary>
    /// Gets all items in the user's denylist.
    /// </summary>
    /// <returns>A list of item IDs in the user's denylist.</returns>
    /// <response code="200">List of denylisted item IDs.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<IReadOnlyList<Guid>> GetDenylistedItems()
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Unable to determine user ID from request");
            return Unauthorized();
        }

        _logger.LogDebug("Retrieving denylist for user {UserId}", userId);
        var items = _denylistManager.GetUserDenylist(userId);
        return Ok(items);
    }

    /// <summary>
    /// Removes an item from the user's denylist.
    /// </summary>
    /// <param name="itemId">The item ID to remove from the denylist.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Item successfully removed from denylist.</response>
    /// <response code="400">Invalid item ID.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpDelete("{itemId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult RemoveItemFromDenylist([FromRoute, Required] string itemId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Unable to determine user ID from request");
            return Unauthorized();
        }

        _logger.LogDebug("Removing item {ItemId} from denylist for user {UserId}", itemId, userId);
        _denylistManager.RemoveFromUserDenylist(userId, itemId);
        return NoContent();
    }

    /// <summary>
    /// Get the user ID from the current user claims.
    /// </summary>
    /// <returns>The user ID.</returns>
    private Guid GetUserId()
    {
        if (HttpContext.User.Identity is ClaimsIdentity identity)
        {
            var claim = identity.FindFirst("Jellyfin-UserId");
            if (claim != null && !string.IsNullOrEmpty(claim.Value))
            {
                if (Guid.TryParse(claim.Value, out var userId))
                {
                    return userId;
                }
            }
        }

        return Guid.Empty;
    }
}
