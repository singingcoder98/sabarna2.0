namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — the questions, answered in one sentence first.
///
/// Three kinds of search now want this shape:
///   • AEO (answer engines: Google's AI answers, Bing, featured snippets) — they lift a short,
///     direct answer that sits immediately under the question;
///   • GEO / LLM SEO (ChatGPT, Perplexity, Gemini) — they quote a page that states a fact plainly,
///     with a place, a number and a date in the same paragraph;
///   • ordinary search — FAQPage structured data, which is only allowed when the same text is
///     visible on the page, so the markup below is rendered from this one list on the home page.
///
/// Rule for whoever edits this: the first sentence must answer the question completely. The
/// second sentence may add the reason. Never start with "At Sabarna Pure, we believe…".
/// </summary>
public static class Faq
{
    public sealed record Item(string Question, string Answer);

    public static IReadOnlyList<Item> General { get; } = new List<Item>
    {
        new("Where is Sabarna Pure based?",
            "Sabarna Pure is a home kitchen in Vivekananda Nagar, Madhyamgram, Kolkata 700129, and it has " +
            "been selling since 2016. Everything is made there and posted anywhere in India."),

        new("Do your pickles contain preservatives?",
            "No. Salt, mustard oil and sunlight are the preservative — there is no vinegar, no citric acid, " +
            "no artificial colour and no preservative in any jar we sell. That is why a jar is dated by hand " +
            "on the day it is filled."),

        new("How long does a jar of achar keep?",
            "Twelve months from the filling date on the jar, and longer if it stays in a cool, dark cupboard " +
            "with the lid tight. Always use a dry spoon — water is the only thing that spoils a pickle."),

        new("Is kuler achar sweet or sour?",
            "Both. Kuler achar is sweet-and-sour: the wild ber is cooked with sugar and salt in mustard oil, " +
            "so it hits sweet first and turns sour at the end. Kul has a short winter season, so we make one " +
            "batch a year and stop when the fruit stops."),

        new("Why has my honey turned solid or grainy?",
            "That is raw honey crystallising, and it is proof the honey was never heated or blended — not a " +
            "fault. Stand the jar in warm water for a few minutes to loosen it; never boil it, because heat " +
            "removes what makes raw honey worth buying."),

        new("How do I fry dal bori so it does not break?",
            "Fry it in medium-hot mustard oil for about twenty seconds a side and take it out before it browns " +
            "darkly. Bori is sun-dried dal with no binder, so it needs steady heat rather than a hot pan."),

        new("Do you deliver outside Kolkata?",
            "Yes. Orders are couriered anywhere in India, and Kolkata, Barasat and Madhyamgram usually arrive " +
            "in two to four days. Packing happens on Mondays and Thursdays, with a tracking link the same evening."),

        new("Is everything on the shelf vegetarian?",
            "Yes — every jar and every packet is 100% vegetarian, which is why the green dot is printed on every " +
            "label. Payment is online only (UPI, cards, netbanking); there is no cash on delivery.")
    };

    /// <summary>
    /// saptarshi: 16-Nov-2026 — three questions per shelf, shown on every product page of that shelf.
    /// Product pages are where "how do I cook bori" and "does achar need a fridge" actually get
    /// asked, and a short answer there is what an AI answer engine quotes.
    /// </summary>
    public static IReadOnlyList<Item> ForCategory(string? category)
    {
        var key = (category ?? string.Empty).Trim().ToLowerInvariant();

        if (key.Contains("honey"))
        {
            return new List<Item>
            {
                new("Is Sundarban honey raw or heated?",
                    "Raw. The honey is strained through cloth and bottled — never heated, never blended and " +
                    "nothing added, which is why it still smells of the mangrove flowers it came from."),
                new("What does Sundarban honey taste like?",
                    "Darker and more mineral than garden honey, with a salty edge from the mangrove flowers " +
                    "(khalsi, goran, keora). It is sharper on the tongue and less sweet than clover honey."),
                new("Can I cook with raw honey?",
                    "Better not. Heat destroys the enzymes and aroma that make raw honey worth buying — stir it " +
                    "into warm water or warm milk after the pan is off the stove.")
            };
        }

        if (key.Contains("bori"))
        {
            return new List<Item>
            {
                new("How do I cook dal bori?",
                    "Fry it in medium-hot mustard oil for about twenty seconds a side, then add it to the dal or " +
                    "the dalna for the last five minutes. It soaks the gravy without falling apart."),
                new("How long does bori keep?",
                    "Six months in a sealed packet in a dry cupboard, and longer in the fridge. Bori is sun-dried " +
                    "with no water left in it, so it keeps without any preservative."),
                new("Is bori fried before packing?",
                    "No. Every bori we sell is raw and sun-dried — you fry it at home, which is why it still " +
                    "tastes of dal and not of old oil.")
            };
        }

        return new List<Item>
        {
            new("Does Bengali achar need to be kept in the fridge?",
                "No. Salt and mustard oil preserve it at room temperature for twelve months; a fridge makes the " +
                "oil solidify and the pieces go soft. Keep the jar in a dark cupboard with the lid tight."),
            new("How much achar should I eat at a time?",
                "A piece or two with a meal. It is a condiment, salty and oily by design — if you watch your salt, " +
                "take less and use a dry spoon so the rest keeps."),
            new("Is your achar made without preservatives?",
                "Yes. No preservative, no vinegar, no artificial colour and no citric acid — mango, kul or lemon, " +
                "salt, mustard oil and spice, then sun. The date on the jar is the day it was filled.")
        };
    }

    /// <summary>The FAQPage block for <c>application/ld+json</c>. Same words as the page shows.</summary>
    public static string SchemaJson() => SchemaJson(General);

    /// <summary>The FAQPage block for any list of questions.</summary>
    public static string SchemaJson(IReadOnlyList<Item> items)
    {
        var schema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = items.Select(item => new Dictionary<string, object>
            {
                ["@type"] = "Question",
                ["name"] = item.Question,
                ["acceptedAnswer"] = new Dictionary<string, object>
                {
                    ["@type"] = "Answer",
                    ["text"] = item.Answer
                }
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(schema);
    }
}
