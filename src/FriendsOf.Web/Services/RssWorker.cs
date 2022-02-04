using System.ComponentModel;

namespace FriendsOf.Web.Services;

public class RssWorker : IHostedService, IAsyncDisposable
{
    private readonly RssService _rssService;
    private readonly IConfiguration _configuration;
    private Timer? _timer;

    public RssWorker(RssService rssService, IConfiguration configuration)
    {
        _rssService = rssService;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var refreshFeedsInMinutes = Convert.ToInt32(_configuration["refreshFeedsInMinutes"]);
        _timer = new Timer(RefreshFeeds, null, TimeSpan.Zero, TimeSpan.FromMinutes(refreshFeedsInMinutes));

        return Task.CompletedTask;
    }

    private void RefreshFeeds(object? state)
    {
        _rssService.DownloadFeeds().ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is IAsyncDisposable timer)
        {
            await timer.DisposeAsync();
        }

        _timer = null;
    }
}