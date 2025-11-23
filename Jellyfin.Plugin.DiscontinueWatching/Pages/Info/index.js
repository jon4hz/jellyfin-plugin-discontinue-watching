export default function (view, params) {
  view.addEventListener('viewshow', e => {
    import(window.ApiClient.getUrl('web/configurationpage?name=shared.js')).then(shared => {
      shared.setPage('Info');
      console.log('[DiscontinueWatching] Info page loaded');
    });
  });
}
