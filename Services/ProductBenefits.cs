using Sabarna.Web.Models;

namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — "why this jar is worth eating", written for the shop window.
///
/// Three rules this file obeys, and they are not negotiable:
///   1. Say what the food actually contains (protein, iron, vitamin C, vitamin E) — those are facts
///      about food, and they may be stated.
///   2. Describe how a thing has traditionally been *used* ("grandmothers warmed mustard oil for
///      hair") — that is culture, and it may be told.
///   3. Never claim to cure, treat or prevent anything. Under FSSAI rules a food business may not
///      make a health or disease claim without approval, and a small farm does not have it. "Helps
///      with" and "good for" are marketing; "cures" is a notice from the regulator.
///
/// The wording is kept in one place so the product page and the "Health &amp; tradition" page can
/// never drift apart, and so the owner can read every claim the shop makes in a single screen.
/// </summary>
public static class ProductBenefits
{
    public sealed record Card(string Icon, string Title, string Text);

    public sealed record Panel(
        string Kicker,
        string Headline,
        string Intro,
        string Focus,
        IReadOnlyList<Card> Cards,
        string Traditional,
        string Fine);

    /// <summary>The panel for a category, without a product-specific line.</summary>
    public static Panel ForCategory(string? category)
    {
        var key = (category ?? string.Empty).Trim();

        if (key.Contains("honey", StringComparison.OrdinalIgnoreCase))
        {
            return Honey;
        }

        if (key.Contains("bori", StringComparison.OrdinalIgnoreCase))
        {
            return Bori;
        }

        return Pickle;
    }

    /// <summary>The panel for one product: the category's cards plus the line only that jar earns.</summary>
    public static Panel For(Product product)
    {
        var panel = ForCategory(product.Category);

        return Focuses.TryGetValue(product.Name, out var focus)
            ? panel with { Focus = focus }
            : panel;
    }

    // ---------------------------------------------------------------------------------------------
    // Honey
    // ---------------------------------------------------------------------------------------------
    private static readonly Panel Honey = new(
        Kicker: "Why raw honey",
        Headline: "Raw honey, used the way our grandmothers used it",
        Intro: "Unheated and unstrained beyond a cloth sieve, so the pollen, the enzymes and the " +
               "aroma stay in the jar. Heating honey is what makes it look clean and taste of nothing.",
        Focus: string.Empty,
        Cards: new[]
        {
            new Card("🌿", "Skin",
                "Honey holds moisture — it is a humectant, which is the plain reason it turns up in " +
                "every home face pack. A teaspoon in warm water as a rinse, or a little on dry lips " +
                "in winter, is the oldest trick in the house."),
            new Card("💇", "Hair",
                "Warm a spoon of honey with coconut or mustard oil, work it into the scalp for ten " +
                "minutes, then wash. Hair comes out softer and shinier. We are not claiming more " +
                "than that, because more than that is not true."),
            new Card("🍯", "Throat & stamina",
                "Stirred into warm — never boiling — water, it soothes a scratchy throat and gives " +
                "quick energy before a long day. Boiling water kills exactly what you paid for, " +
                "which is why raw honey is never cooked.")
        },
        Traditional: "In our village the honey jar was the winter cupboard: with ginger and tulsi for a " +
                     "cough, with warm milk at night, and — once it had cooled — on a small kitchen burn. " +
                     "It is food, not medicine. It is food that has been used this way for a very long time.",
        Fine: "Never give honey to a child under one year. Never boil it. If you are diabetic, honey is " +
              "still sugar — count the spoon and ask your doctor about the portion.");

    // ---------------------------------------------------------------------------------------------
    // Bori
    // ---------------------------------------------------------------------------------------------
    private static readonly Panel Bori = new(
        Kicker: "Why dal bori",
        Headline: "Dal, shaped by hand and dried in the sun",
        Intro: "Bori is lentils and nothing else. The dal is soaked, ground, shaped and laid on the roof " +
               "until it is hard enough to rattle in the packet. No preservative is needed, because " +
               "there is no water left in it to spoil.",
        Focus: string.Empty,
        Cards: new[]
        {
            new Card("💪", "Plant protein",
                "Bori is dal protein in a form that keeps. Two or three pieces in a dalna put real " +
                "protein on a vegetarian plate, the kind that costs nothing extra and needs no fridge."),
            new Card("🩸", "Iron & fibre",
                "Urad, masoor and matar dal all carry iron and fibre. The darker the bori, the more of " +
                "it — which is why musur (masoor) is the one our grandmothers cooked when somebody " +
                "looked pale."),
            new Card("🍲", "Light on the stomach",
                "A thin dal with bori is the Bengali \"pathya\" — the plate you cook when someone is " +
                "getting over something, or when the weather has turned. Easy to digest, warm, and no " +
                "spice needed.")
        },
        Traditional: "Bori was never a snack in our house, it was winter work. The dal was ground at " +
                     "night, shaped by hand at dawn and left on the roof for three days of sun. That is " +
                     "the whole recipe — and it is why there is nothing on the label except dal.",
        Fine: "Bori is dry protein: keep the packet sealed and it will keep for months. Fry at medium " +
              "heat — a bori that browns in seconds is telling you the oil is too hot, not that the bori " +
              "is old.");

    // ---------------------------------------------------------------------------------------------
    // Pickles
    // ---------------------------------------------------------------------------------------------
    private static readonly Panel Pickle = new(
        Kicker: "Why achar",
        Headline: "Fruit picked early, cured in mustard oil and sun",
        Intro: "A pickle is summer's job working through the year. Salt, mustard oil and sun do what a " +
               "fridge does today — no preservative is added, because the salt and the oil already are one.",
        Focus: string.Empty,
        Cards: new[]
        {
            new Card("🥭", "Vitamins from raw fruit",
                "Achar is made from fruit picked before it ripens, when vitamin C and vitamin A are at " +
                "their highest. That is the whole point of an amsi or a mango pickle — the fruit is " +
                "younger than anything you would eat fresh."),
            new Card("🦠", "The old way of preserving",
                "Salt-cured pickle is a living, fermented food. The sourness makes the mouth water and " +
                "the stomach ready, which is exactly why a piece of achar goes at the top of a Bengali " +
                "plate and not at the end of it."),
            new Card("💇", "Mustard oil, inside and out",
                "The kachi ghani mustard oil in the jar is the same oil Bengali grandmothers warmed for " +
                "hair and skin for generations. Eaten in small amounts it also brings vitamin E and " +
                "omega-3 to the plate.")
        },
        Traditional: "Achar was made once a year, in the hottest fortnight, and eaten until the next " +
                     "summer. The jars stood in the sun on the roof every morning and came in before the " +
                     "dew. Nothing about that has changed here, which is why our pickle tastes of the " +
                     "season and not of a factory.",
        Fine: "A pickle is a condiment, not a portion — a piece or two with a meal. It is salty and oily " +
              "by design; if you watch your salt, or acidity troubles you, take less. Always a dry spoon.");

    // ---------------------------------------------------------------------------------------------
    // The one line each jar earns on its own page.
    // ---------------------------------------------------------------------------------------------
    private static readonly Dictionary<string, string> Focuses = new(StringComparer.Ordinal)
    {
        ["Biuli Dal Bori — 250 g"] =
            "Biuli (urad) is the firmest of the four — it holds its shape in a gravy and carries the most protein per piece.",

        ["Matar Dal Bori — 250 g"] =
            "Matar is the lightest of the four: a little sweeter, and the easiest on a weak stomach.",

        ["Moshla Dal Bori — 250 g"] =
            "Moshla bori already has cumin, black pepper and green chilli kneaded inside — it needs nothing but hot oil and rice.",

        ["Musur Dal Bori — 250 g"] =
            "Musur (masoor) is the red one and the one with the most iron — the bori our grandmothers cooked when somebody looked pale.",

        ["Amsi Pickle (Sweet & Sour) — 500 g"] =
            "Amsi is raw mango at its sourest: the highest vitamin C on this shelf, and the pickle that makes plain rice taste like summer.",

        ["Amsi Pickle (Sweet & Sour) — 250 g"] =
            "The same sour raw-mango pickle in the small jar — vitamin C from fruit that never ripened, and enough for a fortnight of rice.",

        ["Mango Pickle (Spicy) — 500 g"] =
            "Spicy mango achar keeps the vitamin A of the raw fruit and brings it to you in mustard oil, which is where the vitamin E rides in.",

        ["Mango Pickle (Spicy) — 250 g"] =
            "The same spicy mango in the small jar — the one to start with if chilli and mustard oil are new to you.",

        ["Kul Pickle (Sweet & Sour) — 500 g"] =
            "Kul (wild ber) is the local fruit with the most vitamin C after amla, and the shortest season of anything we make — which is why it comes in small batches.",

        ["Kul Pickle (Sweet & Sour) — 250 g"] =
            "Kul in the small jar: the wild ber pickle, for anyone who wants to taste a Bengal winter before buying the big one.",

        ["Mixed Pickle — 500 g"] =
            "Mango, lemon, carrot and green chilli in one jar — a bit of every vitamin the season has, and no two spoons alike.",

        ["Mixed Pickle — 250 g"] =
            "The mixed jar in the small size — the one to keep on the office desk or send with a child's tiffin.",

        ["Pure Honey — 100 g"] =
            "Raw and unheated, so the enzymes and pollen stay in the jar. 100 g is deliberately small — taste it before you commit to a bigger one."
    };
}
