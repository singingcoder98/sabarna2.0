using Sabarna.Web.Models;

namespace Sabarna.Web.Services;

/// <summary>
/// saptarshi: 16-Nov-2026 — everything the shop says about a product in the search results.
///
/// One record per jar: its clean URL, its meta description, its image alt text, and the long
/// description in English plus Bengali. Kept in code (not typed into the admin panel) for two
/// reasons: the words are reviewed together in one screen, and a build can carry them to the live
/// database without the owner re-typing thirteen products.
///
/// Grouped by shelf because that is how the keywords cluster: daler bori / Bengali bori, Bengali
/// achar / kuler achar / amsi achar, Sundarban honey. Keywords appear once or twice in a natural
/// sentence; nothing is repeated to the point of stuffing, which Google treats as spam.
/// </summary>
public sealed record SeoCopy(
    string Slug,
    string MetaDescription,
    string AltText,
    string Description,
    string DescriptionBn);

public static class ProductContent
{
    public static SeoCopy? For(Product product) =>
        ByName.TryGetValue(product.Name, out var copy) ? copy : null;

    /// <summary>All thirteen jars, keyed by the exact name used in /Admin/Products.</summary>
    public static IReadOnlyDictionary<string, SeoCopy> All => ByName;

    private static readonly Dictionary<string, SeoCopy> ByName = new(StringComparer.Ordinal)
    {
        // ---------------------------------------------------------------- bori (daler bori) -----
        ["Biuli Dal Bori — 250 g"] = new SeoCopy(
            Slug: "biuli-daler-bori-250g",
            MetaDescription: "Sun-dried biuli daler bori from a Madhyamgram, Kolkata kitchen. Hand-shaped urad dal bori, never fried in the packet, no preservatives. 250 g.",
            AltText: "Biuli dal bori — Bengali urad dal bori drying in the sun, 250 g pack",
            Description: "Biuli dal bori is the Bengali bori at its most traditional: urad dal soaked overnight, " +
                         "ground on a stone, shaped by hand at dawn and left on the roof of our Madhyamgram kitchen " +
                         "for three days of sun. Nothing is fried before it reaches you and no preservative goes in " +
                         "— the drying is the preservative. Every batch is made in the steady winter sun, so the " +
                         "bori you open is one week's work, not a warehouse's. Two or three pieces in a dalna, or " +
                         "fried on their own with hot rice, and a Bengali lunch is complete. 250 g, roughly forty pieces.",
            DescriptionBn: "বিউলির ডালের বড়ি — সারারাত ভিজিয়ে রাখা বিউলির ডাল পাথরে বেটে, ভোরে হাতে গড়ে, " +
                           "মধ্যমগ্রামের রান্নাঘরের ছাদে তিন দিন রোদে শুকিয়ে তৈরি। প্যাকেটে তেল-ভাজা বড়ি নেই, " +
                           "কোনও প্রিজারভেটিভও নেই — রোদই এখানে সংরক্ষক। শীতের রোদে এক ব্যাচে তৈরি, তাই এক সপ্তাহের " +
                           "কাজের বড়ি হাতে আসে। ডালনায় দু-তিনটে বড়ি, বা ভেজে গরম ভাতের সঙ্গে — বাঙালির দুপুর সম্পূর্ণ। ২৫০ গ্রাম।"),

        ["Matar Dal Bori — 250 g"] = new SeoCopy(
            Slug: "matar-dal-bori-250g",
            MetaDescription: "Matar dal bori — light, hand-shaped yellow pea bori sun-dried in Madhyamgram. No preservatives, no colour. 250 g pack, made in small batches.",
            AltText: "Matar dal bori — Bengali yellow pea dal bori in a glass jar, 250 g",
            Description: "Matar dal bori is the lighter of our four boris: yellow peas ground and shaped by hand, then " +
                         "sun-dried on the roof until it rattles in the packet. It is a little sweeter than urad bori " +
                         "and it softens faster, which is why it belongs in a light dalna or a poppy-seed gravy rather " +
                         "than a heavy curry. No preservative, no colour, nothing fried before packing — three days of " +
                         "Madhyamgram sun does the work. A gentle bori, the one we cook for children or for a weak " +
                         "stomach. 250 g, from one winter batch.",
            DescriptionBn: "মটরের ডালের বড়ি — মটরডাল বেটে হাতে গড়ে, ছাদে রোদে শুকিয়ে তৈরি হালকা বড়ি। বিউলির " +
                           "বড়ির চেয়ে একটু মিষ্টি এবং তাড়াতাড়ি নরম হয়, তাই হালকা ডালনা বা পোস্তর ঝোলে ভালো লাগে। " +
                           "কোনও প্রিজারভেটিভ নেই, রং নেই, প্যাকেটে ভাজা নেই। শীতে এক ব্যাচে তৈরি। বাচ্চাদের বা " +
                           "পেট গরম থাকলে এই বড়িই আমাদের প্রথম পছন্দ। ২৫০ গ্রাম।"),

        ["Moshla Bori — 250 g"] = new SeoCopy(
            Slug: "moshla-bori-250g",
            MetaDescription: "Moshla bori with cumin, black pepper and green chilli kneaded inside. Sun-dried Bengali bori, no preservatives, from Madhyamgram. 250 g.",
            AltText: "Moshla bori — spiced Bengali lentil bori with cumin and chilli, 250 g pack",
            Description: "Moshla bori has the spice kneaded in, not sprinkled on: cumin, black pepper and green chilli " +
                         "go into the ground dal before it is shaped, so every piece carries the masala to the middle. " +
                         "Sun-dried for three days on our roof in Madhyamgram, packed unfried, with no preservative and " +
                         "no artificial colour. Fry a few in hot mustard oil and eat them with plain rice and a slice of " +
                         "lime — that is a whole meal in a Bengali household. They are also good crushed into a chorchori. " +
                         "250 g, made in small winter batches.",
            DescriptionBn: "মসলার বড়ি — জিরে, গোলমরিচ আর কাঁচালঙ্কা ডালের মধ্যে মাখা থাকে, উপরে ছড়ানো নয়। " +
                           "তিন দিন ছাদে রোদে শুকিয়ে, না ভেজে প্যাক করা হয়; প্রিজারভেটিভ বা কৃত্রিম রং নেই। " +
                           "গরম সরিষার তেলে ভেজে ভাত-লেবুর সঙ্গে খেলেই বাঙালি পেট ভরে যায়। চচ্চড়িতেও ভেঙে দিতে " +
                           "পারে। শীতে অল্প ব্যাচে তৈরি। ২৫০ গ্রাম।"),

        ["Musur Dal Bori — 250 g"] = new SeoCopy(
            Slug: "musur-dal-bori-250g",
            MetaDescription: "Musur dal bori — masoor dal bori, sun-dried till crisp in Madhyamgram. Iron-rich Bengali bori, no preservatives or colour. 250 g.",
            AltText: "Musur dal bori — red masoor dal bori from a Bengali kitchen, 250 g pack",
            Description: "Musur dal bori is the red one — masoor dal ground, shaped by hand and sun-dried until crisp. " +
                         "Darker and earthier than urad bori, it is the one our grandmothers reached for when someone at " +
                         "home was recovering, because masoor dal carries iron. It also fries the fastest of the four, " +
                         "which makes it the weeknight bori. No preservative, no colour, nothing fried in the packet: " +
                         "three days of sun on a Madhyamgram roof is the only processing it gets. 250 g, from a single " +
                         "winter batch.",
            DescriptionBn: "মুসুর ডালের বড়ি — লাল মুসুরডাল বেটে হাতে গড়ে, রোদে শুকিয়ে তৈরি। বিউলির বড়ির চেয়ে " +
                           "গাঢ় ও মাটির গন্ধযুক্ত; বাড়িতে কেউ অসুস্থ হলে ঠাকুমারা এই বড়িই বানাতেন, কারণ মুসুরডালে " +
                           "আয়রন বেশি। চারটের মধ্যে সবচেয়ে তাড়াতাড়ি ভাজা হয়, তাই রোজকার বড়ি। প্যাকেটে ভাজা নেই, " +
                           "প্রিজারভেটিভ নেই। ২৫০ গ্রাম, শীতের এক ব্যাচের।"),

        // ---------------------------------------------------------------- amsi achar -----------
        ["Amsi Pickle (Sweet & Sour) — 500 g"] = new SeoCopy(
            Slug: "amsi-achar-500g",
            MetaDescription: "Amsi achar (amshotto) — Bengali sweet and sour raw mango pickle, sun-cured in mustard oil with no preservatives. 500 g jar.",
            AltText: "Amsi achar — Bengali sweet and sour mango pickle in a 500 g glass jar",
            Description: "Amsi achar, or amshotto, is raw mango cooked down with sugar, salt and roasted spice until it " +
                         "turns dark, thick and glossy. It is the sweet-and-sour Bengali achar: eaten with paratha, with " +
                         "khichuri, or straight off the spoon at four in the afternoon. Ours is made once a summer in a " +
                         "small batch and sun-cured, with mustard oil and no preservative, no colour and no citric acid. " +
                         "The fruit comes from the same orchards every year, so the taste does not drift. 500 g jar — a " +
                         "whole season of Bengali lunches.",
            DescriptionBn: "আমসত্ত্ব — কাঁচা আম চিনি, নুন আর ভাজা মশলা দিয়ে অল্প আঁচে জ্বাল দেওয়া, যতক্ষণ না " +
                           "গাঢ় ও চটচটে হয়। এটাই বাঙালির টক-মিষ্টি আচার — পরোটার সঙ্গে, খিচুড়ির সঙ্গে, বা " +
                           "বিকেলে চামচে করে। প্রতি গ্রীষ্মে অল্প ব্যাচে, সরিষার তেলে, রোদে শুকিয়ে তৈরি; " +
                           "প্রিজারভেটিভ, রং বা সাইট্রিক অ্যাসিড নেই। ৫০০ গ্রাম — গোটা মরশুমের আচার।"),

        ["Amsi Pickle (Sweet & Sour) — 250 g"] = new SeoCopy(
            Slug: "amsi-achar-250g",
            MetaDescription: "Amsi achar 250 g — sweet and sour Bengali mango pickle, handmade without preservatives. The trial jar before the big one.",
            AltText: "Amsi achar — Bengali sweet and sour mango pickle in a 250 g jar",
            Description: "The same amsi achar, in the jar we sell most of. Raw mango cooked down with sugar, salt and " +
                         "roasted spice until it is dark and sticky, then sun-cured with mustard oil — handmade, in one " +
                         "small batch each summer, with no preservative and no colour. Sweet first, sour after, and the " +
                         "heat comes from the spice, not from chilli. 250 g is the honest trial size: enough for a " +
                         "fortnight of parathas, small enough that a first order is not a gamble.",
            DescriptionBn: "একই আমসত্ত্ব, আমাদের সবচেয়ে বিক্রি হওয়া ছোট জারে। কাঁচা আম, চিনি, নুন আর ভাজা " +
                           "মশলা দিয়ে জ্বাল, তারপর সরিষার তেলে রোদে শুকানো — প্রতি গ্রীষ্মে অল্প ব্যাচে, হাতে তৈরি, " +
                           "প্রিজারভেটিভ ছাড়া। ২৫০ গ্রাম — প্রথমবারের জন্য ঠিক মাপ।"),

        // ---------------------------------------------------------------- aam (mango) achar ----
        ["Mango Pickle (Spicy) — 500 g"] = new SeoCopy(
            Slug: "aam-achar-500g",
            MetaDescription: "Aam achar — spicy Bengali mango pickle with mustard oil and panch phoron, sun-cured, no preservatives. 500 g jar from Kolkata.",
            AltText: "Aam achar — spicy Bengali mango pickle in a 500 g glass jar",
            Description: "Aam achar, the spicy Bengali mango pickle: raw mango cut into chunks, mustard oil, panch phoron, " +
                         "dried red chilli and salt, then left in the sun for days until the oil clears and the pieces " +
                         "turn soft at the edges. Nothing else goes in — no preservative, no colour, no vinegar shortcut. " +
                         "It is made once a year in the hot fortnight, in batches small enough to fill by hand. Sharp, " +
                         "oily, and built for plain rice and dal; one spoonful covers a whole plate. 500 g jar, from " +
                         "this season's mangoes.",
            DescriptionBn: "আমের আচার — কাঁচা আম টুকরো করে, সরিষার তেল, পাঁচফোড়ন, শুকনো লঙ্কা আর নুন দিয়ে " +
                           "কয়েক দিন রোদে রাখা, যতক্ষণ না তেল পরিষ্কার হয়। প্রিজারভেটিভ নেই, রং নেই, ভিনেগারের " +
                           "শর্টকাট নেই। গরমের ওই ক'টা দিনে বছরে একবার, হাতে ভরা যায় এমন ছোট ব্যাচে তৈরি। " +
                           "ভাত-ডালের সঙ্গে এক চামচই যথেষ্ট। ৫০০ গ্রাম।"),

        ["Mango Pickle (Spicy) — 250 g"] = new SeoCopy(
            Slug: "aam-achar-250g",
            MetaDescription: "Aam achar 250 g — spicy Bengali mango pickle, mustard oil and panch phoron, no preservatives. Made in small batches in Kolkata.",
            AltText: "Aam achar — spicy Bengali mango pickle in a 250 g jar",
            Description: "The spicy aam achar in the small jar, for a first taste or a small household. Raw mango, mustard " +
                         "oil, panch phoron, dried red chilli and salt — five things, sun-cured, bottled by hand, with no " +
                         "preservative and no colour. The oil goes cloudy in the cold months; that is the mustard oil " +
                         "doing what real mustard oil does, and it clears again in summer. 250 g, from the same batch as " +
                         "the big jar, so you can decide before you commit.",
            DescriptionBn: "ঝাল আমের আচার ছোট জারে — প্রথমবার চেখে দেখার জন্য, বা ছোট সংসারের জন্য। কাঁচা আম, " +
                           "সরিষার তেল, পাঁচফোড়ন, শুকনো লঙ্কা আর নুন — এই পাঁচটিই, রোদে শুকিয়ে, হাতে ভরা, " +
                           "প্রিজারভেটিভ ছাড়া। শীতে তেল ঘোলা হয়, গরমে আবার পরিষ্কার — আসল সরিষার তেলের লক্ষণ। ২৫০ গ্রাম।"),

        // ---------------------------------------------------------------- kuler achar ----------
        ["Kul Pickle (Sweet & Sour) — 500 g"] = new SeoCopy(
            Slug: "kuler-achar-500g",
            MetaDescription: "Kuler achar — wild ber pickle from around Madhyamgram, sweet and sour, handmade without preservatives. Short season, 500 g jar.",
            AltText: "Kuler achar — Bengali wild ber (kul) pickle in a 500 g glass jar",
            Description: "Kuler achar is the short-season pickle: wild ber from the trees around the fields near " +
                         "Madhyamgram, cooked sweet-and-sour in mustard oil with salt and roasted spice. The kul season " +
                         "lasts a few weeks in winter, so we make one batch and stop when the fruit stops — when the jar " +
                         "is gone, it is gone until next year. Handmade, sun-cured, no preservative and no colour. Sharp, " +
                         "sticky, and almost impossible to stop eating with rice. 500 g. This is the jar our Kolkata " +
                         "customers ask for by name.",
            DescriptionBn: "কুলের আচার — মধ্যমগ্রামের ধারের গাছ থেকে পাওয়া বুনো কুল, সরিষার তেলে মিষ্টি-টক করে " +
                           "রান্না, নুন আর ভাজা মশলা দিয়ে। শীতের মাত্র কয়েক সপ্তাহ কুলের মরশুম, তাই এক ব্যাচ " +
                           "বানিয়ে থেমে যাই — ফুরোলে আর পাওয়া যায় না। হাতে তৈরি, রোদে শুকানো, প্রিজারভেটিভ নেই। " +
                           "ভাতের সঙ্গে শুরু করলে থামানো মুশকিল। ৫০০ গ্রাম।"),

        ["Kul Pickle (Sweet & Sour) — 250 g"] = new SeoCopy(
            Slug: "kuler-achar-250g",
            MetaDescription: "Kuler achar 250 g — Bengali wild ber pickle, sweet and sour, no preservatives, made in a short winter season.",
            AltText: "Kuler achar — Bengali wild ber pickle in a 250 g jar",
            Description: "Kuler achar in the small jar — the same wild ber pickle, cooked sweet-and-sour in mustard oil " +
                         "with salt and roasted spice, sun-cured and bottled by hand with no preservative and no colour. " +
                         "Kul is a winter fruit and a short one; we make what the trees give and then stop until next " +
                         "December. 250 g is enough for one person's winter of rice, or for the plate you keep for " +
                         "guests who ask what kuler achar actually tastes like.",
            DescriptionBn: "ছোট জারে কুলের আচার — একই বুনো কুল, সরিষার তেলে মিষ্টি-টক, রোদে শুকিয়ে হাতে ভরা, " +
                           "প্রিজারভেটিভ বা রং ছাড়া। কুল শীতের ফল আর মরশুম ছোট; গাছ যা দেয় তাই বানাই, তারপর " +
                           "আগামী ডিসেম্বর পর্যন্ত থামি। ২৫০ গ্রাম — এক জনের শীতভর ভাতের জন্য।"),

        // ---------------------------------------------------------------- mixed achar -----------
        ["Mixed Pickle — 500 g"] = new SeoCopy(
            Slug: "mixed-achar-500g",
            MetaDescription: "Mixed Bengali achar — raw mango, lemon, carrot and green chilli cured in mustard oil. Handmade without preservatives. 500 g.",
            AltText: "Mixed Bengali achar with mango, lemon, carrot and chilli in a 500 g jar",
            Description: "Mixed achar gathers what the season gives: raw mango, lemon, carrot and green chilli, cut by " +
                         "hand and cured in mustard oil with salt and panch phoron. Every jar tastes slightly different " +
                         "because every batch follows the market and not a formula. Sun-cured for a week, with no " +
                         "preservative, no colour and no vinegar. One spoon covers a plate of rice, and the oil left at " +
                         "the bottom is the best thing you can pour over a bowl of dal. 500 g, filled by hand in our " +
                         "Madhyamgram kitchen.",
            DescriptionBn: "মিক্সড আচার — কাঁচা আম, লেবু, গাজর আর কাঁচালঙ্কা হাতে কেটে সরিষার তেল, নুন আর " +
                           "পাঁচফোড়নে মাখিয়ে রোদে রাখা। প্রতিবার স্বাদ একটু আলাদা, কারণ ব্যাচ বাজারের সঙ্গে চলে, " +
                           "কোনো ফর্মুলা নয়। এক চামচেই ভাত, আর তলার তেলটা ডালে ঢেলে দিলে সবচেয়ে ভালো। " +
                           "প্রিজারভেটিভ বা রং নেই। ৫০০ গ্রাম।"),

        ["Mixed Pickle — 250 g"] = new SeoCopy(
            Slug: "mixed-achar-250g",
            MetaDescription: "Mixed achar 250 g — Bengali pickle with mango, lemon, carrot and chilli, handmade without preservatives.",
            AltText: "Mixed Bengali achar in a 250 g jar with mango, lemon and chilli",
            Description: "The mixed achar in the small jar: raw mango, lemon, carrot and green chilli, cured in mustard " +
                         "oil with salt and panch phoron, sun-dried for a week and bottled by hand with no preservative " +
                         "and no colour. It is the jar that suits a tiffin box, an office desk or a single person's " +
                         "kitchen, where a big jar would go to waste. 250 g, from the same batch as the large one.",
            DescriptionBn: "ছোট জারে মিক্সড আচার — কাঁচা আম, লেবু, গাজর আর কাঁচালঙ্কা, সরিষার তেলে, রোদে " +
                           "শুকিয়ে হাতে ভরা; প্রিজারভেটিভ বা রং নেই। টিফিনবাক্স, অফিসের টেবিল বা একজনের " +
                           "রান্নাঘরের জন্য ঠিক মাপ। ২৫০ গ্রাম।"),

        // ---------------------------------------------------------------- honey ----------------
        ["Pure Honey — 100 g"] = new SeoCopy(
            Slug: "sundarban-honey-100g",
            MetaDescription: "Sundarban honey, raw and unheated, from mangrove forests of Bengal. Pure forest honey, never blended or heated. 100 g jar.",
            AltText: "Sundarban honey — raw dark forest honey in a 100 g glass jar",
            Description: "Sundarban honey comes from the mangrove forests at the edge of the Bay of Bengal, where the " +
                         "hives are wild and the flowers are salt-tolerant — khalsi, goran and keora. It is collected by " +
                         "honey-gathering families we have bought from for years, strained through cloth and bottled raw: " +
                         "never heated, never blended, nothing added, nothing taken away. That is why it turns grainy in " +
                         "winter — proof of raw honey, not a fault. Darker and more mineral than garden honey. 100 g is " +
                         "the trial jar, deliberately small.",
            DescriptionBn: "সুন্দরবনের মধু — বঙ্গোপসাগরের ধারে ম্যানগ্রোভ জঙ্গল থেকে, যেখানে মৌচাক বুনো আর ফুল " +
                           "লবণ-সহিষ্ণু: খলসি, গরান, কেওড়া। বছরের পর বছর যাঁদের কাছ থেকে কিনি, সেই মৌ-সংগ্রাহক " +
                           "পরিবারগুলোর হাত থেকে আসে; কাপড়ে ছেঁকে, না গরম করে, না মিশিয়ে বোতলবন্দি — " +
                           "খাঁটি মধু। শীতে দানা বাঁধে, সেটাই আসল মধুর প্রমাণ। ১০০ গ্রাম — প্রথমে চেখে দেখার মাপ।")
    };
}
