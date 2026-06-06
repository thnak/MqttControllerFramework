namespace MqttControllerFramework.RateLimiting.Strategies;

/// <summary>Per-client per-topic token bucket.</summary>
internal sealed class TokenBucket
{
    private readonly TokenBucketConfiguration _config;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private double _tokens;
    private DateTime _lastRefill;

    public TokenBucket(TokenBucketConfiguration config, TimeProvider time)
    {
        _config = config;
        _time = time;
        _tokens = config.Capacity;
        _lastRefill = time.GetUtcNow().UtcDateTime;
    }

    public bool TryConsume(int count)
    {
        lock (_lock)
        {
            Refill();
            if (_tokens < count) return false;
            _tokens -= count;
            return true;
        }
    }

    public double GetAvailableTokens()
    {
        lock (_lock) { Refill(); return _tokens; }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _tokens = _config.Capacity;
            _lastRefill = _time.GetUtcNow().UtcDateTime;
        }
    }

    public int CalculateRetryAfter(int needed)
    {
        lock (_lock)
        {
            Refill();
            if (_tokens >= needed) return 0;
            var shortfall = needed - _tokens;
            var intervals = Math.Ceiling(shortfall / _config.RefillRate);
            return (int)Math.Ceiling(intervals * _config.RefillIntervalMs / 1000.0);
        }
    }

    private void Refill()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var intervals = (now - _lastRefill).TotalMilliseconds / _config.RefillIntervalMs;
        if (intervals >= 1)
        {
            _tokens = Math.Min(_tokens + intervals * _config.RefillRate, _config.Capacity);
            _lastRefill = now;
        }
    }
}
