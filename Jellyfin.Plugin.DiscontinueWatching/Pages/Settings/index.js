export default function (view, params) {
  view.addEventListener('viewshow', e => {
    import(window.ApiClient.getUrl('web/configurationpage?name=shared.js')).then(shared => {
      shared.setPage('Settings');

      // Load configuration
      shared.loadConfiguration().then(config => {
        document.getElementById('daysThreshold').value = config.DaysThreshold || 180;
      });

      // Set up config update listener
      shared.setOnConfigUpdatedListener('settings', config => {
        console.log('[DiscontinueWatching] Updating settings DOM');
        document.getElementById('daysThreshold').value = config.DaysThreshold || 180;
      });

      // Handle form submission
      shared.keyedEventListener(document.querySelector('#DiscontinueWatchingSettingsForm'), 'submit', function (e) {
        e.preventDefault();

        const config = shared.getConfig();
        config.DaysThreshold = parseInt(document.getElementById('daysThreshold').value, 10);

        shared.saveConfiguration(config);

        return false;
      });
    });
  });
}
