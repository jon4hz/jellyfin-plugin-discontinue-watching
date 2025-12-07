# jellyfin-plugin-discontinue-watching

## ✨ About

This plugin lets you remove items from the "Continue Watching" list in Jellyfin without resetting the watch progress.
The plugin can also automatically hide items after a configurable period of inactivity.

> It implements [this feature request](https://features.jellyfin.org/posts/517/add-an-option-to-remove-an-item-from-continue-watching)

## 📱 Supported Devices

This plugin works by injecting custom JavaScript into Jellyfin's web interface. It is compatible with:

- ✅ **Jellyfin Web**
- ✅ **Jellyfin Android App**
- ✅ **Jellyfin iOS App**
- ✅ **Jellyfin Desktop Apps**
- ❌ **Other 3rd party apps**

## 📦 Installation

### Requirements

This plugin requires the following plugins to be installed:

- [Jellyfin-JavaScript-Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector)
- [jellyfin-plugin-file-transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) (optional, but recommended)

### Install Plugin

1. Open your Jellyfin server's admin dashboard
2. Navigate to **Plugins** → **Catalog**
3. Click the **Add Repository** button
4. Add this repository URL:
   ```
   https://raw.githubusercontent.com/jon4hz/jellyfin-plugin-discontinue-watching/main/manifest.json
   ```
5. Find **Discontinue Watching** in the plugin catalog and install it
6. Restart your Jellyfin server
7. Enable the plugin in **Plugins** → **My Plugins**

## ⚙️ Custom Routes

This plugin has a custom route that simulate the "Resume" endpoints but filters out unwanted items.
Configure your reverse proxy accordingly to make every client support this plugin.

### Treafik Example

```yaml
---
http:
  middlewares:
    discontinue-watching:
      replacePathRegex:
        regex: '^/UserItems/Resume'
        replacement: '/DiscontinueWatching/Override/UserItems/Resume'
---
http:
  middlewares:
    discontinue-watching-old:
      replacePathRegex:
        regex: '^/Users/([^/]+)/Items/Resume'
        replacement: '/DiscontinueWatching/Override/Users/$1/Items/Resume'
```

### Nginx Example

```nginx
location ~ ^/UserItems/Resume {
    rewrite ^/UserItems/Resume /DiscontinueWatching/Override/UserItems/Resume break;
    proxy_pass http://jellyfin_backend;
}

location ~ ^/Users/([^/]+)/Items/Resume {
    rewrite ^/Users/([^/]+)/Items/Resume /DiscontinueWatching/Override/Users/$1/Items/Resume break;
    proxy_pass http://jellyfin_backend;
}
```

## 🔌 Integrate this plugin!

This plugin can be easily integrated in other 3rd party clients.

### API Routes

```

GET /DiscontinueWatching

# returns an array of item ID which should be hidden from Continue Watching for the current user

```

```

POST /DiscontinueWatching/{itemId}

# adds the specified item ID to the denylist for the current user

```

With these routes, other clients can remove items from Continue Watching by calling the POST route.
The items returned by the GET route should be hidden from Continue Watching list.

## 🛠️ Development

### Building

```

make build

```

### Contributing

All kind of contributions are welcome! Feel free to open issues or submit pull requests.
If you want to contribute code, please make sure to install the pre-commit hooks:

```

pre-commit install

```

## 🥂 Credis

- [KefinTweaks](https://github.com/ranaldsgift/KefinTweaks) - for giving me the idea to write a dedicated plugin for this feature. (and also for helping with some javascript struggles)
- [jellyfin-plugin-streamyfin](https://github.com/streamyfin/jellyfin-plugin-streamyfin) - from whom I've borrowed some code snippets for the configuration page.

## 📜 License

GPLv3
