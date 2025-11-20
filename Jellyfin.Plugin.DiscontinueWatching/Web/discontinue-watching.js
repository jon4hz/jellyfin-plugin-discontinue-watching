/**
 * The discontinue-watching plugin brings "remove from continue watching" functionality to Jellyfin.
 */

(function () {
  'use strict';

  console.log('[DiscontinueWatching] Initializing...');

  // Add CSS for the remove button
  const style = document.createElement('style');
  style.textContent = `
    .discontinue-watching-button {
      position: absolute !important;
      top: 8px !important;
      right: 8px !important;
      z-index: 10 !important;
      background: rgba(0, 0, 0, 0.7) !important;
      border: none !important;
      border-radius: 50% !important;
      width: 32px !important;
      height: 32px !important;
      display: flex !important;
      align-items: center !important;
      justify-content: center !important;
      cursor: pointer !important;
      transition: all 0.2s ease !important;
      opacity: 0 !important;
      transform: scale(0.8) !important;
    }

    .cardOverlayContainer:hover .discontinue-watching-button {
      opacity: 1 !important;
      transform: scale(1) !important;
    }

    .discontinue-watching-button:hover {
      background: rgba(220, 38, 38, 0.9) !important;
      transform: scale(1.1) !important;
    }

    .discontinue-watching-button:disabled {
      opacity: 0.6 !important;
      cursor: not-allowed !important;
      transform: scale(0.9) !important;
    }

    .discontinue-watching-button .material-icons {
      color: white !important;
      font-size: 18px !important;
    }
  `;
  document.head.appendChild(style);

  // Store the denylist in memory
  let denylist = new Set();

  /**
   * Make API calls to the plugin backend
   */
  function callPluginAPI(action, data) {
    console.log(`[DiscontinueWatching] API Call - Action: ${action}`, data);

    // Check if ApiClient is available
    if (!window.ApiClient || !window.ApiClient.accessToken || !window.ApiClient.accessToken()) {
      console.error('[DiscontinueWatching] ApiClient not available or no access token');
      return Promise.reject(new Error('ApiClient not available'));
    }

    const baseUrl = window.ApiClient.serverAddress() || window.location.origin;

    switch (action) {
      case 'getDenylist':
        return fetch(`${baseUrl}/DiscontinueWatching`, {
          headers: {
            Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
          },
        })
          .then(response => {
            if (!response.ok) {
              throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
          })
          .catch(error => {
            console.error('[DiscontinueWatching] Error getting denylist:', error);
            throw error;
          });

      case 'addToDenylist':
        return fetch(`${baseUrl}/DiscontinueWatching/${data.itemId}`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `MediaBrowser Token="${window.ApiClient.accessToken()}"`,
          },
        })
          .then(response => {
            if (!response.ok) {
              throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response;
          })
          .catch(error => {
            console.error('[DiscontinueWatching] Error adding to denylist:', error);
            throw error;
          });

      default:
        console.warn(`[DiscontinueWatching] Unknown API action: ${action}`);
        return Promise.reject(new Error(`Unknown API action: ${action}`));
    }
  }

  /**
   * Load the denylist from the API
   */
  async function loadDenylist() {
    try {
      console.log('[DiscontinueWatching] Loading denylist');
      const items = await callPluginAPI('getDenylist');
      denylist = new Set(items);
    } catch (error) {
      console.error('[DiscontinueWatching] Error loading denylist:', error);
      denylist = new Set();
    }
  }

  /**
   * Filter out items from continue watching section
   */
  function filterContinueWatching() {
    const cards = document.querySelectorAll('.card[data-positionticks]');

    cards.forEach(card => {
      const itemId = card.getAttribute('data-id');
      if (itemId && denylist.has(itemId)) {
        card.style.display = 'none';
      }
    });
  }

  /**
   * Add remove button to a card overlay container
   */
  function addRemoveButton(overlayContainer) {
    // Check if button already exists
    if (overlayContainer && overlayContainer.querySelector('.discontinue-watching-button')) {
      return;
    }

    // Find the card parent to get the item ID
    const card = overlayContainer.closest('.card');
    if (!card) {
      console.warn('[DiscontinueWatching] Could not find card parent for overlay container');
      return;
    }

    // Only add button to cards with data-positionticks (in progress items)
    const positionTicks = card.getAttribute('data-positionticks');
    if (!positionTicks) {
      return; // Not a continue watching item, skip
    }

    const itemId = card.getAttribute('data-id');
    if (!itemId) {
      console.warn('[DiscontinueWatching] Could not find data-id on card element');
      return;
    }

    // Find the .cardOverlayButton-br container to position our button before it
    const buttonContainer = overlayContainer.querySelector('.cardOverlayButton-br');
    if (!buttonContainer) {
      console.warn('[DiscontinueWatching] Could not find .cardOverlayButton-br container');
      return;
    }

    // Create remove button
    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.className = 'discontinue-watching-button';
    removeButton.setAttribute('data-action', 'none');
    removeButton.setAttribute('data-id', itemId);
    removeButton.title = 'Remove from Continue Watching';

    // Create the close icon
    const removeIcon = document.createElement('span');
    removeIcon.className = 'material-icons';
    removeIcon.textContent = 'close';
    removeIcon.setAttribute('aria-hidden', 'true');

    removeButton.appendChild(removeIcon);

    // Add click event listener
    removeButton.addEventListener('click', async e => {
      e.preventDefault();
      e.stopPropagation();

      // Show loading state
      const originalIcon = removeIcon.textContent;
      removeIcon.textContent = 'hourglass_empty';
      removeButton.disabled = true;

      try {
        // Call API to add to denylist
        await callPluginAPI('addToDenylist', { itemId });
        denylist.add(itemId);

        // Remove the card from the DOM
        card.remove();

        console.log(`[DiscontinueWatching] Successfully removed item ${itemId} from continue watching`);
      } catch (error) {
        console.error('[DiscontinueWatching] Failed to remove from continue watching:', error);

        // Restore original state on error
        removeIcon.textContent = originalIcon;
        removeButton.disabled = false;

        // Show error message to user
        alert('Failed to remove from continue watching. Please try again.');
      }
    });

    // Add the button as a sibling before the .cardOverlayButton-br container
    buttonContainer.parentNode.insertBefore(removeButton, buttonContainer);

    console.log(`[DiscontinueWatching] Added remove button for item ${itemId}`);
  }

  /**
   * Process all existing overlay containers
   */
  function processExistingOverlayContainers() {
    const overlayContainers = document.querySelectorAll('.cardOverlayContainer');

    overlayContainers.forEach(overlayContainer => {
      const buttonContainer = overlayContainer.querySelector('.cardOverlayButton-br');
      if (buttonContainer) {
        addRemoveButton(overlayContainer);
      }
    });
  }

  /**
   * Set up MutationObserver to watch for new overlay containers
   */
  function setupObserver() {
    const observer = new MutationObserver(mutations => {
      let shouldFilter = false;

      mutations.forEach(mutation => {
        if (mutation.type === 'childList') {
          // Check for added nodes
          mutation.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) {
              // Check if the added node is an overlay container
              if (node.classList && node.classList.contains('cardOverlayContainer')) {
                const buttonContainer = node.querySelector('.cardOverlayButton-br');
                if (buttonContainer) {
                  addRemoveButton(node);
                }
              }

              // Check for overlay containers within the added node
              const overlayContainers = node.querySelectorAll && node.querySelectorAll('.cardOverlayContainer');
              if (overlayContainers && overlayContainers.length > 0) {
                overlayContainers.forEach(overlayContainer => {
                  const buttonContainer = overlayContainer.querySelector('.cardOverlayButton-br');
                  if (buttonContainer) {
                    addRemoveButton(overlayContainer);
                  }
                });
              }

              // Check if this node or its children contain cards with position ticks
              if (node.classList && node.classList.contains('card') && node.getAttribute('data-positionticks')) {
                shouldFilter = true;
              } else if (node.querySelectorAll) {
                const newCards = node.querySelectorAll('.card[data-positionticks]');
                if (newCards.length > 0) {
                  shouldFilter = true;
                }
              }
            }
          });
        }
      });

      // Filter after processing all mutations
      if (shouldFilter) {
        filterContinueWatching();
      }
    });

    // Start observing
    observer.observe(document.body, {
      childList: true,
      subtree: true,
    });

    // Process any existing overlay containers
    processExistingOverlayContainers();

    // Filter items already in denylist
    filterContinueWatching();
  }

  /**
   * Initialize the plugin
   */
  async function init() {
    try {
      // Wait for ApiClient to be available
      await waitForApiClient();

      await loadDenylist();

      // Setup observer and update UI
      setupObserver();
    } catch (error) {
      console.error('[DiscontinueWatching] ApiClient not available, initialization aborted:', error);
    }
  }

  /**
   * Wait for ApiClient to be available with authentication
   */
  function waitForApiClient() {
    return new Promise((resolve, reject) => {
      let retryCount = 0;
      const maxRetries = 30; // Wait up to 30 seconds

      const checkApiClient = () => {
        if (window.ApiClient && window.ApiClient.accessToken && window.ApiClient.accessToken()) {
          resolve();
          return;
        }

        retryCount++;
        if (retryCount >= maxRetries) {
          reject(new Error('ApiClient not available'));
          return;
        }

        setTimeout(checkApiClient, 1000);
      };

      checkApiClient();
    });
  }

  // Initialize when script loads
  init();
})();
