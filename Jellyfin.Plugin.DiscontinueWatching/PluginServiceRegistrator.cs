using Jellyfin.Plugin.DiscontinueWatching.EventHandlers;
using Jellyfin.Plugin.DiscontinueWatching.ScheduledTasks;
using Jellyfin.Plugin.DiscontinueWatching.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.DiscontinueWatching;

/// <summary>
/// Register discontinue watching services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<DenylistManager>();
        serviceCollection.AddScoped<IEventConsumer<PlaybackStartEventArgs>, PlaybackStartConsumer>();
        serviceCollection.AddHostedService<PluginEntryPoint>();
        serviceCollection.AddTransient<IScheduledTask, CleanupContinueWatchingTask>();
        serviceCollection.AddTransient<IScheduledTask, CleanupDenylistTask>();
    }
}
