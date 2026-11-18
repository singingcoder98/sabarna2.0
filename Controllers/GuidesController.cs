using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;

namespace Sabarna.Web.Controllers;

/// <summary>
/// saptarshi: 16-Nov-2026 — /guides and /guides/{slug}.
///
/// A shop page answers "what is it and how much". A guide answers "why", which is the question that
/// arrives about six weeks before the order. Six guides, written from the kitchen, each one pointing
/// at the two or three jars it belongs to.
/// </summary>
public class GuidesController : Controller
{
    private readonly AppDbContext _db;

    public GuidesController(AppDbContext db)
    {
        _db = db;
    }

    // GET /guides
    [HttpGet]
    [Route("guides")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Guides — achar, bori and honey";
        ViewData["Description"] = "Short, practical guides from a Bengali kitchen: storing achar for a "
                                + "year, frying dal bori, why raw honey crystallises, using mustard oil.";
        ViewData["Breadcrumbs"] = "Guides";

        return View(Guides.All);
    }

    // GET /guides/kuler-achar-short-season
    [HttpGet]
    [Route("guides/{slug}")]
    public async Task<IActionResult> Show(string slug)
    {
        var guide = Guides.BySlug(slug);

        if (guide == null)
        {
            return NotFound();
        }

        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync() ?? new SiteSetting();

        // The jars this guide talks about, in the order the guide mentions them.
        var names = guide.RelatedProducts.ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Active && names.Contains(p.Name))
            .ToListAsync();

        var ordered = names
            .Select(name => products.FirstOrDefault(p => p.Name == name))
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();

        ViewData["Title"] = guide.Title;
        ViewData["Description"] = guide.MetaDescription;
        ViewData["Breadcrumbs"] = guide.Title;
        ViewData["OgType"] = "article";   // saptarshi: 16-Nov-2026 — a guide is a written piece, not a shop page

        return View(new Guides.GuidePage(guide, ordered, settings));
    }
}
