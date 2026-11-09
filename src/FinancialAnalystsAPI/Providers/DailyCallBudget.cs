namespace FinancialAnalystsAPI.Providers;

/// <summary>
/// In-process count of provider calls per UTC day. A singleton, because HttpClientFactory recycles handlers.
/// It resets on restart, so it is a safety net against runaway loops rather than an exact quota tracker.
/// </summary>
public sealed class DailyCallBudget(int limit, TimeProvider time)
{
    private readonly Lock _gate = new();
    private DateOnly _day;
    private int _used;

    public int Limit => limit;

    public int Used
    {
        get { lock (_gate) { Roll(); return _used; } }
    }

    public bool TryConsume()
    {
        lock (_gate)
        {
            Roll();
            if (_used >= limit) return false;
            _used++;
            return true;
        }
    }

    private void Roll()
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        if (today == _day) return;
        _day = today;
        _used = 0;
    }
}

/// <summary>
/// Innermost handler on the provider client, so every attempt (including resilience retries) is counted.
/// </summary>
public sealed class DailyCallBudgetHandler(DailyCallBudget budget) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!budget.TryConsume())
            throw new ProviderQuotaExceededException($"Daily provider call budget of {budget.Limit} is used up.");

        return base.SendAsync(request, cancellationToken);
    }
}
