using Sabarna.Web.Models;

namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — the guides.
///
/// Why these exist: a shop page can only rank for "buy kuler achar". A guide ranks for the sentences
/// people actually type before they are ready to buy — "why did my honey turn solid", "why does my
/// bori break in the pan", "how do I store achar". Those searches are where a small brand beats a
/// big one, because a big one has no reason to answer them.
///
/// Every guide is content, not markup: the text lives here so it is reviewed in one place, and the
/// Article + BreadcrumbList schema is generated from exactly the words on the page.
/// </summary>
public static class Guides
{
    public sealed record Section(string Heading, IReadOnlyList<string> Paragraphs);

    public sealed record Guide(
        string Slug,
        string Title,
        string MetaDescription,
        string Kicker,
        string Intro,
        IReadOnlyList<Section> Sections,
        IReadOnlyList<string> RelatedProducts,
        string Published,
        string ReadTime);

    public static IReadOnlyList<Guide> All { get; } = new List<Guide>
    {
        new(
            Slug: "why-raw-honey-crystallises",
            Title: "Why raw honey turns solid — and why that is a good sign",
            MetaDescription: "Raw Sundarban honey crystallises in winter because it was never heated or blended. Here is what the grains mean and how to loosen the jar.",
            Kicker: "Honey",
            Intro: "A jar of honey that has gone grainy in the cupboard has not spoiled. It has done the one " +
                   "thing that proves it was never heated, never blended and never stretched with syrup.",
            Sections: new[]
            {
                new Section("What is actually happening",
                    new[]
                    {
                        "Honey is mostly two sugars: glucose and fructose. Glucose settles into crystals more " +
                        "readily than fructose, so the honey closest to the glucose ratio of 1:1 crystallises " +
                        "fastest. Cold weather speeds it up; a warm room slows it down. Nothing has gone wrong.",
                        "Factory honey is heated to about 70°C and passed through a fine filter before bottling. " +
                        "That dissolves the crystals and makes a clear syrup that never sets — and takes the " +
                        "enzymes, the pollen and most of the aroma with it. A honey that stays liquid forever in " +
                        "a Bengali winter was almost certainly heated."
                    }),
                new Section("What the grains tell you",
                    new[]
                    {
                        "Sundarban honey crystallises faster than garden honey because it is high in glucose, " +
                        "which comes from the mangrove flowers — khalsi, goran, keora. Ours usually starts " +
                        "setting in December and is properly grainy by February.",
                        "If a jar you bought elsewhere crystallises only in one corner, or forms a hard rock at " +
                        "the bottom with liquid on top, that is a different story: it usually means the honey " +
                        "was blended from two sources with different sugar ratios."
                    }),
                new Section("How to loosen it without spoiling it",
                    new[]
                    {
                        "Stand the jar in a bowl of warm — not hot — water for ten minutes and stir. The water " +
                        "should be comfortable on your wrist, around 40°C. Never microwave honey and never boil " +
                        "it: above about 45°C the enzymes that make raw honey worth buying begin to break down.",
                        "If you like grainy honey, eat it as it is. Spread on a hot paratha it melts into exactly " +
                        "what it should be."
                    })
            },
            RelatedProducts: new[] { "Pure Honey — 100 g" },
            Published: "2026-11-16",
            ReadTime: "3 min read"),

        new(
            Slug: "fry-dal-bori-without-breaking",
            Title: "Frying dal bori without breaking it: five rules",
            MetaDescription: "Why bori breaks in the pan, what heat to use, and how to fry Bengali daler bori so it stays whole and crisp. From a Madhyamgram kitchen.",
            Kicker: "Bori",
            Intro: "Bori is sun-dried dal with no binder and no preservative — which is exactly why it breaks " +
                   "when the oil is too hot. Five things fix it.",
            Sections: new[]
            {
                new Section("1. The oil is almost always too hot",
                    new[]
                    {
                        "Medium heat, not high. Drop a small crumb in: it should rise slowly with a gentle ring " +
                        "of bubbles. If it dances and browns in three seconds, take the pan off the flame for a " +
                        "minute before the next batch.",
                        "Bori is dry and porous. In very hot oil the outside seals and browns while the inside is " +
                        "still raw, and the piece cracks along the weakest edge to let the steam out."
                    }),
                new Section("2. Do not crowd the pan",
                    new[]
                    {
                        "Four or five pieces at a time in a small kadai. Bori needs room to puff; packed together " +
                        "it steams instead of frying and turns leathery."
                    }),
                new Section("3. Twenty seconds a side is usually enough",
                    new[]
                    {
                        "Take it out when it is pale gold, not brown. It keeps cooking in its own heat for a few " +
                        "seconds, and it will soften again in the dal. A dark bori tastes of the oil, not of the " +
                        "dal."
                    }),
                new Section("4. Salt the dal, never the bori",
                    new[]
                    {
                        "Salting before frying draws moisture to the surface and makes the piece break up. If " +
                        "you want it as a snack, sprinkle salt the moment it comes out of the oil."
                    }),
                new Section("5. Store it dry, use it within six months",
                    new[]
                    {
                        "Bori has no water left in it, so it keeps — but it also absorbs moisture from the air. " +
                        "A sealed jar in a dry cupboard, away from the stove, is all it needs. If a piece smells " +
                        "musty, the batch has taken damp and should go."
                    })
            },
            RelatedProducts: new[] { "Biuli Dal Bori — 250 g", "Musur Dal Bori — 250 g", "Moshla Bori — 250 g" },
            Published: "2026-11-16",
            ReadTime: "3 min read"),

        new(
            Slug: "kuler-achar-short-season",
            Title: "Kuler achar and the six weeks it exists for",
            MetaDescription: "Why kuler achar is the shortest season in a Bengali kitchen, what the wild ber tastes like, and why the jar runs out every year.",
            Kicker: "Pickles",
            Intro: "Kul — the wild ber that grows on thorny trees along the fields — is available for about six " +
                   "weeks in winter. Kuler achar is what a Bengali kitchen does with that window.",
            Sections: new[]
            {
                new Section("The fruit decides, not the shop",
                    new[]
                    {
                        "Kul ripens from December into January and is gone by the time the mango trees flower. " +
                        "There is no second crop and no cold storage that keeps the taste: a frozen kul loses the " +
                        "firmness that makes the pickle worth eating.",
                        "So we make one batch — what the trees around the fields near Madhyamgram give us — and " +
                        "then stop. If you find kuler achar on sale in July, ask which year's fruit it is from."
                    }),
                new Section("Sweet first, sour after",
                    new[]
                    {
                        "The ber is cooked with sugar, salt and roasted spice in mustard oil. The sugar goes in " +
                        "early and the sourness comes from the fruit itself, which is why the pickle tastes sweet " +
                        "on the first bite and sharp at the end — quite different from an amsi, where the sourness " +
                        "is cooked down with the sugar.",
                        "It is also the stickiest pickle we make. That is not extra syrup; it is the kul breaking " +
                        "down in the sun over a week."
                    }),
                new Section("How to eat it",
                    new[]
                    {
                        "With plain rice and a little ghee, or on a paratha at tea. A spoon of the oil from the " +
                        "top poured over hot rice is the part regulars ask about.",
                        "Because the season is short, we suggest buying the 500 g jar if you already know you like " +
                        "it, and the 250 g if this is your first winter."
                    })
            },
            RelatedProducts: new[] { "Kul Pickle (Sweet & Sour) — 500 g", "Kul Pickle (Sweet & Sour) — 250 g" },
            Published: "2026-11-16",
            ReadTime: "3 min read"),

        new(
            Slug: "store-bengali-achar-for-a-year",
            Title: "How to store Bengali achar so it lasts a whole year",
            MetaDescription: "Dry spoon, no fridge, a dark cupboard and a film of mustard oil: how a jar of Bengali achar keeps for twelve months without preservatives.",
            Kicker: "Pickles",
            Intro: "A jar of achar goes off for one reason in nine cases out of ten, and it is not the recipe. " +
                   "It is water — from a wet spoon, a wet finger or a wet lid.",
            Sections: new[]
            {
                new Section("The dry spoon rule",
                    new[]
                    {
                        "Every time you take a piece out, use a spoon that has not touched dal, curd or your " +
                        "mouth. One drop of water in a jar of pickle is enough to grow a white film in a week.",
                        "If a white film does appear on the surface, it is usually harmless yeast. Skim it off " +
                        "with a dry spoon, make sure the remaining pickle is covered by oil, and use that jar " +
                        "first. A green or black growth is different — throw the jar away."
                    }),
                new Section("Never the fridge",
                    new[]
                    {
                        "Mustard oil sets solid at cold temperatures, and a pickle that freezes and thaws goes " +
                        "soft and loses its bite. A dark cupboard at room temperature, away from the stove, is " +
                        "the right place — exactly where it sat in a Bengali kitchen before refrigerators.",
                        "The one exception is a very humid coastal flat in the monsoon. There, keep the jar in the " +
                        "coolest cupboard you have and check the surface once a month."
                    }),
                new Section("Keep the oil line",
                    new[]
                    {
                        "Pickle keeps under oil, not next to air. If the level drops below the top pieces after " +
                        "you have eaten half the jar, pour a spoon of fresh mustard oil over the surface — warm " +
                        "it slightly first so it spreads.",
                        "Keep the lid tight. A loose lid lets the aroma out and the moisture in, and the top layer " +
                        "dries into a leathery skin."
                    }),
                new Section("How long is it really good for",
                    new[]
                    {
                        "Twelve months from the date on the jar, and it is at its best between the second and " +
                        "eighth month, when the spice has settled. Every jar carries the date it was filled, " +
                        "written by hand on the day."
                    })
            },
            RelatedProducts: new[] { "Mixed Pickle — 500 g", "Amsi Pickle (Sweet & Sour) — 500 g", "Mango Pickle (Spicy) — 500 g" },
            Published: "2026-11-16",
            ReadTime: "4 min read"),

        new(
            Slug: "amsi-vs-mango-achar",
            Title: "Amsi or aam achar: which Bengali pickle goes with what",
            MetaDescription: "Amsi (amshotto) is sweet and slow-cooked; aam achar is sharp and spicy. A plain guide to which Bengali pickle belongs on which plate.",
            Kicker: "Pickles",
            Intro: "Both are made from raw mango, both are Bengali, and they are not interchangeable. One is a " +
                   "condiment and the other is almost a sweet.",
            Sections: new[]
            {
                new Section("Amsi (amshotto) — sweet, thick, dark",
                    new[]
                    {
                        "Amsi is raw mango cooked down with sugar, salt and roasted spice until it is thick and " +
                        "glossy. It is closer to a preserve than a pickle: sweet at the front, sour at the back, " +
                        "with almost no chilli heat.",
                        "Put it with paratha, with khichuri, on a slice of bread, or with a cup of tea at four in " +
                        "the afternoon. Do not put it with rice and dal — the sweetness fights the dal."
                    }),
                new Section("Aam achar — sharp, oily, spicy",
                    new[]
                    {
                        "Aam achar is chunks of raw mango cured in mustard oil with panch phoron, dried red " +
                        "chilli and salt, then left in the sun. It is meant for rice: a spoonful on the edge of " +
                        "the plate, eaten with a mouthful of dal and rice.",
                        "The oil is part of the dish. Pour a little over the rice; it carries the chilli and the " +
                        "mustard flavour."
                    }),
                new Section("And the other two",
                    new[]
                    {
                        "Kuler achar is the seasonal sweet-and-sour one — wild ber, six weeks of winter, eaten " +
                        "with rice or on toast. Mixed pickle is the everyday jar: mango, lemon, carrot and chilli, " +
                        "one spoon for a whole plate.",
                        "If you are choosing your first jar and you are not sure, take amsi for breakfast and " +
                        "aam achar for lunch. That is how most Bengali households split it."
                    })
            },
            RelatedProducts: new[] { "Amsi Pickle (Sweet & Sour) — 250 g", "Mango Pickle (Spicy) — 250 g", "Mixed Pickle — 250 g" },
            Published: "2026-11-16",
            ReadTime: "3 min read"),

        new(
            Slug: "mustard-oil-hair-and-skin",
            Title: "Mustard oil for hair and skin: the Bengali way, done properly",
            MetaDescription: "How kachi ghani mustard oil has been used on hair and skin in Bengali homes, how to warm it, and the one rule about sensitive skin.",
            Kicker: "Tradition",
            Intro: "Before it was a cooking fat, mustard oil was what a Bengali household put on its hair on a " +
                   "Sunday and on its skin in winter. The method matters more than the brand.",
            Sections: new[]
            {
                new Section("For hair",
                    new[]
                    {
                        "Warm two spoons of kachi ghani mustard oil until it is comfortable on the wrist — never " +
                        "smoking hot. Part the hair and work it into the scalp with the fingertips, not the nails, " +
                        "for about five minutes, then along the lengths.",
                        "Leave it for an hour, or overnight with a towel on the pillow, then wash with a mild " +
                        "shampoo. Twice a month is enough in summer; once a week in winter, when the scalp dries.",
                        "The sharp smell is part of it. If it bothers you, warm the oil with a few fenugreek seeds " +
                        "and let it cool before using — that is the old way of mellowing it."
                    }),
                new Section("For skin",
                    new[]
                    {
                        "Mustard oil is warming, which is why it is a winter oil. A few drops massaged into dry " +
                        "elbows, knees and heels before a bath is the traditional use, and it works because the " +
                        "oil is rich in vitamin E and holds moisture in.",
                        "On the face, be careful: pure mustard oil is strong. Most people use a few drops mixed " +
                        "into a plainer carrier oil, or use it only on the body."
                    }),
                new Section("The one rule",
                    new[]
                    {
                        "Patch-test first. Put a drop on the inside of your elbow, wait a day, and see. Mustard " +
                        "oil is a common irritant for sensitive skin, and none of this is medical advice — if your " +
                        "skin or scalp is already inflamed, ask a doctor before you massage anything into it.",
                        "And buy kachi ghani (cold-pressed), not refined. Refined mustard oil has been heated and " +
                        "treated; the smell and most of the vitamin E are gone, which defeats the purpose."
                    })
            },
            RelatedProducts: new[] { "Mixed Pickle — 500 g", "Mango Pickle (Spicy) — 500 g" },
            Published: "2026-11-16",
            ReadTime: "4 min read")
    };

    /// <summary>What the guide page needs: the words, the jars it points at, and the shop's details.</summary>
    public sealed record GuidePage(Guide Guide, IReadOnlyList<Product> Related, SiteSetting Settings);

    public static Guide? BySlug(string? slug) =>
        All.FirstOrDefault(g => string.Equals(g.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
