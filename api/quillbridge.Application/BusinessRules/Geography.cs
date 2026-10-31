using System.Text.RegularExpressions;

namespace quillbridge.Application.BusinessRules;

// works out the country of a head office written as "Leeds, UK", "Leeds, United Kingdom" or just "Leeds",
// and which SDR covers it
public static class Geography
{
    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UK"] = "UK", ["GB"] = "UK", ["United Kingdom"] = "UK", ["England"] = "UK", ["Scotland"] = "UK", ["Wales"] = "UK",
        ["IE"] = "IE", ["Ireland"] = "IE",
        ["US"] = "US", ["USA"] = "US", ["United States"] = "US",
        ["CA"] = "CA", ["Canada"] = "CA",
        ["DE"] = "DE", ["Germany"] = "DE", ["AT"] = "AT", ["Austria"] = "AT", ["CH"] = "CH", ["Switzerland"] = "CH",
        ["NL"] = "NL", ["Netherlands"] = "NL", ["BE"] = "BE", ["Belgium"] = "BE",
        ["SE"] = "SE", ["Sweden"] = "SE", ["DK"] = "DK", ["Denmark"] = "DK", ["NO"] = "NO", ["Norway"] = "NO",
        ["FI"] = "FI", ["Finland"] = "FI", ["IS"] = "IS", ["Iceland"] = "IS",
        ["IT"] = "IT", ["Italy"] = "IT", ["ES"] = "ES", ["Spain"] = "ES", ["PT"] = "PT", ["Portugal"] = "PT",
        ["FR"] = "FR", ["France"] = "FR", ["GR"] = "GR", ["Greece"] = "GR", ["MT"] = "MT", ["Malta"] = "MT"
    };

    // used only when the same file never names the city together with its country
    private static readonly Dictionary<string, string> KnownCities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["London"] = "UK", ["Manchester"] = "UK", ["Leeds"] = "UK", ["Birmingham"] = "UK", ["Edinburgh"] = "UK", ["Glasgow"] = "UK",
        ["Bristol"] = "UK", ["Cardiff"] = "UK", ["Sheffield"] = "UK", ["Nottingham"] = "UK", ["Newcastle"] = "UK",
        ["Dublin"] = "IE", ["Cork"] = "IE", ["Limerick"] = "IE", ["Galway"] = "IE",
        ["New York"] = "US", ["Boston"] = "US", ["Philadelphia"] = "US", ["Baltimore"] = "US", ["Pittsburgh"] = "US",
        ["Atlanta"] = "US", ["Miami"] = "US", ["Charlotte"] = "US", ["Raleigh"] = "US", ["Richmond"] = "US",
        ["Columbus"] = "US", ["Providence"] = "US", ["Albany"] = "US", ["Chicago"] = "US",
        ["Los Angeles"] = "US", ["San Diego"] = "US", ["San Francisco"] = "US", ["Seattle"] = "US", ["Portland"] = "US",
        ["Phoenix"] = "US", ["Denver"] = "US", ["Salt Lake City"] = "US", ["Boise"] = "US", ["Sacramento"] = "US",
        ["Toronto"] = "CA", ["Vancouver"] = "CA", ["Calgary"] = "CA", ["Ottawa"] = "CA", ["Montreal"] = "CA",
        ["Berlin"] = "DE", ["Munich"] = "DE", ["Hamburg"] = "DE", ["Cologne"] = "DE", ["Frankfurt"] = "DE", ["Stuttgart"] = "DE",
        ["Düsseldorf"] = "DE", ["Hanover"] = "DE", ["Nuremberg"] = "DE", ["Vienna"] = "AT", ["Graz"] = "AT", ["Linz"] = "AT",
        ["Innsbruck"] = "AT", ["Zurich"] = "CH", ["Basel"] = "CH", ["Bern"] = "CH", ["Geneva"] = "CH",
        ["Amsterdam"] = "NL", ["Rotterdam"] = "NL", ["Utrecht"] = "NL", ["Eindhoven"] = "NL", ["Groningen"] = "NL",
        ["Brussels"] = "BE", ["Ghent"] = "BE", ["Antwerp"] = "BE", ["Leuven"] = "BE",
        ["Stockholm"] = "SE", ["Gothenburg"] = "SE", ["Malmö"] = "SE", ["Uppsala"] = "SE",
        ["Copenhagen"] = "DK", ["Aarhus"] = "DK", ["Odense"] = "DK", ["Oslo"] = "NO", ["Bergen"] = "NO", ["Stavanger"] = "NO",
        ["Trondheim"] = "NO", ["Helsinki"] = "FI", ["Turku"] = "FI", ["Tampere"] = "FI",
        ["Milan"] = "IT", ["Rome"] = "IT", ["Turin"] = "IT", ["Bologna"] = "IT", ["Florence"] = "IT", ["Verona"] = "IT", ["Naples"] = "IT",
        ["Madrid"] = "ES", ["Barcelona"] = "ES", ["Valencia"] = "ES", ["Seville"] = "ES", ["Bilbao"] = "ES",
        ["Lisbon"] = "PT", ["Porto"] = "PT", ["Braga"] = "PT",
        ["Paris"] = "FR", ["Lyon"] = "FR", ["Marseille"] = "FR", ["Bordeaux"] = "FR", ["Nantes"] = "FR", ["Toulouse"] = "FR"
    };

    private static readonly HashSet<string> WesternUsCities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Los Angeles", "San Diego", "San Francisco", "San Jose", "Sacramento", "Seattle", "Portland", "Phoenix",
        "Denver", "Salt Lake City", "Boise", "Las Vegas", "Albuquerque", "Tucson", "Spokane", "Reno"
    };

    // last resort: the legal form in the company name
    private static readonly (string Pattern, string Country)[] LegalForms =
    [
        (@"\bB\.?V\.?$", "NL"), (@"\bS\.?p\.?A\.?$|\bS\.?r\.?l\.?$", "IT"), (@"\bS\.?L\.?$", "ES"),
        (@"\bOy\b", "FI"), (@"\bA/S$", "DK"), (@"\bAB$", "SE"), (@"\bSAS$", "FR"), (@"\bLda\.?$", "PT"), (@"\bplc$", "UK")
    ];

    // which countries each SDR covers (from the brief); US is split east/west
    private static readonly (string Sdr, string[] Countries)[] Regions =
    [
        ("Maya Brennan", ["US-East"]),
        ("Sam Ortega", ["US-West", "CA"]),
        ("Elif Demir", ["NL", "BE", "SE", "DK", "NO", "FI", "IS"]),
        ("Priya Raman", ["UK", "IE"]),
        ("Jonas Weber", ["DE", "AT", "CH"]),
        ("Luca Ferri", ["IT", "ES", "PT", "FR", "GR", "MT"])
    ];

    public static (string City, string? Country) SplitHq(string hq)
    {
        var comma = hq.LastIndexOf(',');

        if (comma < 0)
        {
            return (hq.Trim(), null);
        }

        var city = hq[..comma].Trim();
        var country = hq[(comma + 1)..].Trim();

        return (city, CountryNames.GetValueOrDefault(country));
    }

    // cities that appear with a country anywhere in the file teach us that city's country
    public static Dictionary<string, string> LearnCities(IEnumerable<string> hqs)
    {
        var learned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hq in hqs)
        {
            var (city, country) = SplitHq(hq);

            if (country is not null && city.Length > 0)
            {
                learned.TryAdd(city, country);
            }
        }

        return learned;
    }

    public static string? ResolveCountry(string hq, string company, IReadOnlyDictionary<string, string> learnedCities)
    {
        var (city, country) = SplitHq(hq);

        if (country is not null)
        {
            return country;
        }

        if (learnedCities.TryGetValue(city, out var learned) || KnownCities.TryGetValue(city, out learned))
        {
            return learned;
        }

        return LegalForms.FirstOrDefault(f => Regex.IsMatch(company.Trim(), f.Pattern, RegexOptions.IgnoreCase)).Country;
    }

    public static string? SdrForRegion(string? country, string hq)
    {
        if (country is null)
        {
            return null;
        }

        var region = country == "US"
            ? (WesternUsCities.Contains(SplitHq(hq).City) ? "US-West" : "US-East")
            : country;

        return Regions.FirstOrDefault(r => r.Countries.Contains(region)).Sdr;
    }
}
