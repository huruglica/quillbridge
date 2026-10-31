namespace quillbridge.Application.Guides;

// keeps AI spend under the configured limit, also when a batch calls the API in parallel:
// each call reserves its worst-case cost first and settles the real cost afterwards
public class BudgetGuard
{
    private readonly object _lock = new();
    private decimal? _spent;
    private decimal _reserved;

    public bool IsLoaded => _spent.HasValue;

    public decimal Spent
    {
        get
        {
            lock (_lock)
            {
                return _spent ?? 0;
            }
        }
    }

    // spend survives restarts because it is summed from the stored guides
    public void Load(decimal spentSoFar)
    {
        lock (_lock)
        {
            _spent ??= spentSoFar;
        }
    }

    public bool TryReserve(decimal amount, decimal limit)
    {
        lock (_lock)
        {
            if ((_spent ?? 0) + _reserved + amount > limit)
            {
                return false;
            }

            _reserved += amount;

            return true;
        }
    }

    public void Settle(decimal reserved, decimal actual)
    {
        lock (_lock)
        {
            _reserved -= reserved;
            _spent = (_spent ?? 0) + actual;
        }
    }
}
