using System.Globalization;
using System.Text.Json;

namespace BulkyBookWeb.Services;

public interface ICurrencyService
{
    /// <summary>Converts a base-currency amount to the current request culture's currency and formats it (e.g. $99.00 / ₦153,450.00).</summary>
    string Format(double baseAmount);
}

public class CurrencyService(IConfiguration configuration) : ICurrencyService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);
    private static readonly Lock Lock = new();
    private static Dictionary<string, double>? _liveRates;
    private static DateTime _lastFetchUtc = DateTime.MinValue;

    private readonly Dictionary<string, double> _fallbackRates = configuration.GetSection("Currency:Rates").GetChildren()
            .ToDictionary(c => c.Key, c => double.TryParse(c.Value, out var v) ? v : 1.0);

    public string Format(double baseAmount)
    {
        var culture = CultureInfo.CurrentCulture;
        return (baseAmount * GetRate(culture)).ToString("C", culture);
    }

    private double GetRate(CultureInfo culture)
    {
        string currency;
        try
        {
            currency = new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch
        {
            return 1.0; // culture without a region -> treat as base currency
        }

        var live = TryGetLiveRates();
        if (live != null && live.TryGetValue(currency, out var liveRate))
            return liveRate;

        return _fallbackRates.TryGetValue(currency, out var fallback) ? fallback : 1.0;
    }

    // fetch live rates (relative to USD) from a free no-key API, cached for 6h; returns null on any failure
    private static Dictionary<string, double>? TryGetLiveRates()
    {
        lock (Lock)
        {
            if (_liveRates != null && DateTime.UtcNow - _lastFetchUtc < CacheTtl)
                return _liveRates;

            try
            {
                var json = _http.GetStringAsync("https://open.er-api.com/v6/latest/USD").GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("result", out var result) && result.GetString() == "success" &&
                    doc.RootElement.TryGetProperty("rates", out var rates))
                {
                    var dict = new Dictionary<string, double>();
                    foreach (var prop in rates.EnumerateObject())
                    {
                        if (prop.Value.TryGetDouble(out var v)) dict[prop.Name] = v;
                    }
                    _liveRates = dict;
                    _lastFetchUtc = DateTime.UtcNow;
                }
            }
            catch
            {
                // provider unavailable -> fall back to configured rates
            }

            return _liveRates;
        }
    }
}
