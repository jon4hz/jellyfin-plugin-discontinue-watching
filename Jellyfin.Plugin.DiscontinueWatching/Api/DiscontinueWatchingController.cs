using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions;
using Jellyfin.Plugin.DiscontinueWatching.Api.Extensions;
using Jellyfin.Plugin.DiscontinueWatching.Api.ModelBinders;
using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
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
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscontinueWatchingController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="denylistManager">The denylist manager.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="dtoService">Instance of the <see cref="IDtoService"/> interface.</param>
    /// <param name="sessionManager">Instance of the <see cref="ISessionManager"/> interface.</param>
    ///
    public DiscontinueWatchingController(
        ILogger<DiscontinueWatchingController> logger,
        DenylistManager denylistManager,
        IUserManager userManager,
        ILibraryManager libraryManager,
        IDtoService dtoService,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _denylistManager = denylistManager;
        _userManager = userManager;
        _libraryManager = libraryManager;
        _dtoService = dtoService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Gets all items in the user's denylist.
    /// </summary>
    /// <returns>A list of item IDs in the user's denylist.</returns>
    /// <response code="200">List of denylisted item IDs.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("items")]
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
    /// Adds an item to the user's denylist.
    /// </summary>
    /// <param name="itemId">The item ID to add to the denylist.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Item successfully added to denylist.</response>
    /// <response code="400">Invalid item ID.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpPost("Items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
        return Ok();
    }

    /// <summary>
    /// Removes an item from the user's denylist.
    /// </summary>
    /// <param name="itemId">The item ID to remove from the denylist.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Item successfully removed from denylist.</response>
    /// <response code="400">Invalid item ID.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpDelete("Items/{itemId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
        return Ok();
    }

    /// <summary>
    /// Overrides for the "Users/{userId}/Items/Resume" endpoint
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="limit">The item limit.</param>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="parentId">Specify this to localize the search to a specific item or folder. Omit to use the root.</param>
    /// <param name="fields">Optional. Specify additional fields of information to return in the output. This allows multiple, comma delimited. Options: Budget, Chapters, DateCreated, Genres, HomePageUrl, IndexOptions, MediaStreams, Overview, ParentId, Path, People, ProviderIds, PrimaryImageAspectRatio, Revenue, SortName, Studios, Taglines.</param>
    /// <param name="mediaTypes">Optional. Filter by MediaType. Allows multiple, comma delimited.</param>
    /// <param name="enableUserData">Optional. Include user data.</param>
    /// <param name="imageTypeLimit">Optional. The max number of images to return, per image type.</param>
    /// <param name="enableImageTypes">Optional. The image types to include in the output.</param>
    /// <param name="excludeItemTypes">Optional. If specified, results will be filtered based on item type. This allows multiple, comma delimited.</param>
    /// <param name="includeItemTypes">Optional. If specified, results will be filtered based on the item type. This allows multiple, comma delimited.</param>
    /// <param name="enableTotalRecordCount">Optional. Enable the total record count.</param>
    /// <param name="enableImages">Optional. Include image information in output.</param>
    /// <param name="excludeActiveSessions">Optional. Whether to exclude the currently active sessions.</param>
    /// <response code="200">Items returned.</response>
    /// <returns>A <see cref="QueryResult{BaseItemDto}"/> with the items that are resumable.</returns>
    [HttpGet("Override/Users/{userId}/Items/Resume")]
    [Obsolete("Kept for backwards compatibility")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<QueryResult<BaseItemDto>> GetResumeItemsLegacy(
        [FromRoute, Required] Guid userId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? parentId,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] ItemFields[] fields,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] MediaType[] mediaTypes,
        [FromQuery] bool? enableUserData,
        [FromQuery] int? imageTypeLimit,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] ImageType[] enableImageTypes,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] excludeItemTypes,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] includeItemTypes,
        [FromQuery] bool enableTotalRecordCount = true,
        [FromQuery] bool? enableImages = true,
        [FromQuery] bool excludeActiveSessions = false)
    => GetResumeItems(
        userId,
        startIndex,
        limit,
        searchTerm,
        parentId,
        fields,
        mediaTypes,
        enableUserData,
        imageTypeLimit,
        enableImageTypes,
        excludeItemTypes,
        includeItemTypes,
        enableTotalRecordCount,
        enableImages,
        excludeActiveSessions);

    /// <summary>
    /// Override for the "UserItems/Resume" endpoint
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="limit">The item limit.</param>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="parentId">Specify this to localize the search to a specific item or folder. Omit to use the root.</param>
    /// <param name="fields">Optional. Specify additional fields of information to return in the output. This allows multiple, comma delimited. Options: Budget, Chapters, DateCreated, Genres, HomePageUrl, IndexOptions, MediaStreams, Overview, ParentId, Path, People, ProviderIds, PrimaryImageAspectRatio, Revenue, SortName, Studios, Taglines.</param>
    /// <param name="mediaTypes">Optional. Filter by MediaType. Allows multiple, comma delimited.</param>
    /// <param name="enableUserData">Optional. Include user data.</param>
    /// <param name="imageTypeLimit">Optional. The max number of images to return, per image type.</param>
    /// <param name="enableImageTypes">Optional. The image types to include in the output.</param>
    /// <param name="excludeItemTypes">Optional. If specified, results will be filtered based on item type. This allows multiple, comma delimited.</param>
    /// <param name="includeItemTypes">Optional. If specified, results will be filtered based on the item type. This allows multiple, comma delimited.</param>
    /// <param name="enableTotalRecordCount">Optional. Enable the total record count.</param>
    /// <param name="enableImages">Optional. Include image information in output.</param>
    /// <param name="excludeActiveSessions">Optional. Whether to exclude the currently active sessions.</param>
    /// <response code="200">Items returned.</response>
    /// <returns>A <see cref="QueryResult{BaseItemDto}"/> with the items that are resumable.</returns>
    [HttpGet("Override/UserItems/Resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<QueryResult<BaseItemDto>> GetResumeItems(
        [FromQuery] Guid? userId,
        [FromQuery] int? startIndex,
        [FromQuery] int? limit,
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? parentId,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] ItemFields[] fields,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] MediaType[] mediaTypes,
        [FromQuery] bool? enableUserData,
        [FromQuery] int? imageTypeLimit,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] ImageType[] enableImageTypes,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] excludeItemTypes,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] includeItemTypes,
        [FromQuery] bool enableTotalRecordCount = true,
        [FromQuery] bool? enableImages = true,
        [FromQuery] bool excludeActiveSessions = false)
    {
        var requestUserId = GetUserId();
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Unable to determine user ID from request");
            return Unauthorized();
        }
        var user = _userManager.GetUserById(requestUserId);
        if (user is null)
        {
            return NotFound();
        }

        var parentIdGuid = parentId ?? Guid.Empty;
        var dtoOptions = new DtoOptions { Fields = fields }
            .AddClientFields(User)
            .AddAdditionalDtoOptions(enableImages, enableUserData, imageTypeLimit, enableImageTypes);

        var ancestorIds = Array.Empty<Guid>();

        var excludeFolderIds = user.GetPreferenceValues<Guid>(PreferenceKind.LatestItemExcludes);
        if (parentIdGuid.IsEmpty() && excludeFolderIds.Length > 0)
        {
            ancestorIds = _libraryManager.GetUserRootFolder().GetChildren(user, true)
                .Where(i => i is Folder)
                .Where(i => !excludeFolderIds.Contains(i.Id))
                .Select(i => i.Id)
                .ToArray();
        }

        var excludeItemIds = Array.Empty<Guid>();
        if (excludeActiveSessions)
        {
            excludeItemIds = _sessionManager.Sessions
                .Where(s => s.UserId.Equals(requestUserId) && s.NowPlayingItem is not null)
                .Select(s => s.NowPlayingItem.Id)
                .ToArray();
        }

        // Add denylist items to excluded items
        var denylistedItems = _denylistManager.GetUserDenylist(requestUserId)
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .Where(id => id != Guid.Empty);
        excludeItemIds = excludeItemIds.Concat(denylistedItems).ToArray();

        var itemsResult = _libraryManager.GetItemsResult(new InternalItemsQuery(user)
        {
            OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            IsResumable = true,
            StartIndex = startIndex,
            Limit = limit,
            ParentId = parentIdGuid,
            Recursive = true,
            DtoOptions = dtoOptions,
            MediaTypes = mediaTypes,
            IsVirtualItem = false,
            CollapseBoxSetItems = false,
            EnableTotalRecordCount = enableTotalRecordCount,
            AncestorIds = ancestorIds,
            IncludeItemTypes = includeItemTypes,
            ExcludeItemTypes = excludeItemTypes,
            SearchTerm = searchTerm,
            ExcludeItemIds = excludeItemIds
        });

        var returnItems = _dtoService.GetBaseItemDtos(itemsResult.Items, dtoOptions, user);

        return new QueryResult<BaseItemDto>(
            startIndex,
            itemsResult.TotalRecordCount,
            returnItems);
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
