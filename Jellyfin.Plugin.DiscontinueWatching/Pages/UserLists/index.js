const createUserDenylistEntry = (userId, itemIds, users, shared) => {
  const template = document.querySelector('#user-denylist-template');
  const clone = template.content.cloneNode(true);

  const detailsElement = clone.querySelector('details');
  const nameHeader = clone.querySelector('.user-name-header');
  const itemsList = clone.querySelector('.user-items-list');
  const arrowIcon = clone.querySelector('.arrow-icon');

  const userName = users[userId] || userId;
  nameHeader.textContent = userName + ' (' + itemIds.length + ' items)';

  detailsElement.addEventListener('toggle', function () {
    arrowIcon.style.transform = detailsElement.open ? 'rotate(90deg)' : 'rotate(0deg)';
  });

  itemIds.forEach(function (itemId) {
    const itemDiv = document.createElement('div');
    itemDiv.style.cssText =
      'display: flex; align-items: center; justify-content: space-between; padding: 0.5em; margin: 0.25em 0; background-color: rgba(255, 255, 255, 0.03); border-radius: 4px;';
    itemDiv.setAttribute('data-item-id', itemId);

    // Create a link to the item
    const itemLink = document.createElement('a');
    itemLink.href = `#!/details?id=${itemId}`;
    itemLink.target = '_blank';
    itemLink.style.cssText = 'font-family: monospace; color: inherit; text-decoration: none;';
    itemLink.textContent = itemId;
    itemLink.addEventListener('mouseenter', function () {
      itemLink.style.textDecoration = 'underline';
    });
    itemLink.addEventListener('mouseleave', function () {
      itemLink.style.textDecoration = 'none';
    });

    const removeBtn = document.createElement('button');
    removeBtn.setAttribute('is', 'emby-button');
    removeBtn.className = 'raised button-cancel';
    removeBtn.innerHTML = '<span>Remove</span>';
    removeBtn.addEventListener('click', function () {
      removeItemFromUserDenylist(userId, itemId, itemDiv, detailsElement, shared);
    });

    itemDiv.appendChild(itemLink);
    itemDiv.appendChild(removeBtn);
    itemsList.appendChild(itemDiv);
  });

  return clone;
};

const removeItemFromUserDenylist = (userId, itemId, itemDiv, detailsElement, shared) => {
  if (!confirm('Are you sure you want to remove this item from the denylist?')) {
    return;
  }

  Dashboard.showLoadingMsg();

  const config = shared.getConfig();

  // UserDenylistEntries is now a dictionary with userId as key
  if (config.UserDenylistEntries && config.UserDenylistEntries[userId]) {
    const itemList = config.UserDenylistEntries[userId];
    const index = itemList.indexOf(itemId);
    if (index > -1) {
      itemList.splice(index, 1);
    }

    // Remove the user entry if the list is empty
    const shouldRemoveEntry = itemList.length === 0;
    if (shouldRemoveEntry) {
      delete config.UserDenylistEntries[userId];
    }

    shared
      .saveConfiguration(config)
      .then(() => {
        Dashboard.hideLoadingMsg();

        // Remove the item from the DOM
        itemDiv.remove();

        // If this was the last item, remove the entire user section
        if (shouldRemoveEntry) {
          detailsElement.remove();

          // Check if there are any denylist entries left
          const container = document.getElementById('userDenylistsContainer');
          if (container && container.children.length === 0) {
            container.innerHTML = '<p>No denylisted items found.</p>';
          }
        } else {
          // Update the item count in the header
          const nameHeader = detailsElement.querySelector('.user-name-header');
          const userName = nameHeader.textContent.split(' (')[0];
          nameHeader.textContent = userName + ' (' + itemList.length + ' items)';
        }
      })
      .catch(error => {
        console.error('[DiscontinueWatching] Error removing item:', error);
        Dashboard.hideLoadingMsg();
        Dashboard.alert('Error removing item from denylist');
      });
  }
};

const loadUserDenylists = shared => {
  const container = document.getElementById('userDenylistsContainer');

  if (!container) {
    console.error('[DiscontinueWatching] userDenylistsContainer not found in DOM');
    return;
  }

  container.innerHTML = '<p>Loading user denylists...</p>';

  Promise.all([shared.loadConfiguration(), shared.loadUsers()])
    .then(function (results) {
      const config = results[0];
      const users = results[1];

      // UserDenylistEntries is now a dictionary
      const userDenylistEntries = config.UserDenylistEntries;

      if (!userDenylistEntries || Object.keys(userDenylistEntries).length === 0) {
        container.innerHTML = '<p>No denylisted items found.</p>';
        return;
      }

      container.innerHTML = '';

      // Iterate over the dictionary
      Object.keys(userDenylistEntries).forEach(function (userId) {
        const itemIds = userDenylistEntries[userId];

        if (!itemIds || itemIds.length === 0) {
          return;
        }

        container.appendChild(createUserDenylistEntry(userId, itemIds, users, shared));
      });
    })
    .catch(function (error) {
      console.error('[DiscontinueWatching] Error loading user denylists:', error);
      container.innerHTML = '<p style="color: red;">Error loading user denylists: ' + (error.message || error) + '</p>';
    });
};

export default function (view, params) {
  view.addEventListener('viewshow', e => {
    import(window.ApiClient.getUrl('web/configurationpage?name=shared.js')).then(shared => {
      shared.setPage('UserLists');

      // Load user denylists
      loadUserDenylists(shared);
    });
  });
}
