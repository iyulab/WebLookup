using System.Collections.Concurrent;
using System.Net;

namespace WebLookup.Http;

internal sealed class RateLimitHandler : DelegatingHandler
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, BackoffState> _backoffStates = new();
    private readonly Action<string, TimeSpan?>? _onRateLimited;
    private readonly int _maxRetries;

    public RateLimitHandler(
        HttpMessageHandler innerHandler,
        int maxRetries = 3,
        Action<string, TimeSpan?>? onRateLimited = null)
        : base(innerHandler)
    {
        _maxRetries = maxRetries;
        _onRateLimited = onRateLimited;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var providerKey = request.RequestUri?.Host ?? "unknown";
        var state = _backoffStates.GetOrAdd(providerKey, _ => new BackoffState());

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            var delay = state.GetCurrentDelay();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = ParseRetryAfter(response);
                _onRateLimited?.Invoke(providerKey, retryAfter);
                state.RecordFailure(retryAfter);

                if (attempt < _maxRetries)
                {
                    response.Dispose();
                    continue;
                }
            }
            else
            {
                state.Reset();
            }

            return response;
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        var retryAfterHeader = response.Headers.RetryAfter;
        if (retryAfterHeader is null)
            return null;

        if (retryAfterHeader.Delta.HasValue)
            return retryAfterHeader.Delta.Value;

        if (retryAfterHeader.Date.HasValue)
        {
            var delay = retryAfterHeader.Date.Value - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private sealed class BackoffState
    {
        private readonly Lock _lock = new();
        private int _consecutiveFailures;
        private TimeSpan? _retryAfterOverride;

        public TimeSpan GetCurrentDelay()
        {
            lock (_lock)
            {
                if (_retryAfterOverride.HasValue)
                {
                    var val = _retryAfterOverride.Value;
                    _retryAfterOverride = null;
                    return val;
                }

                if (_consecutiveFailures == 0)
                    return TimeSpan.Zero;

                var seconds = Math.Pow(2, _consecutiveFailures - 1);
                var delay = TimeSpan.FromSeconds(seconds);
                return delay > MaxBackoff ? MaxBackoff : delay;
            }
        }

        public void RecordFailure(TimeSpan? retryAfter)
        {
            lock (_lock)
            {
                _consecutiveFailures++;
                if (retryAfter.HasValue)
                    _retryAfterOverride = retryAfter;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _retryAfterOverride = null;
            }
        }
    }
}
