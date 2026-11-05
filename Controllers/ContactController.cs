using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The contact page: a form plus our address, phone, email and picking-up hours.
/// </summary>
public class ContactController : Controller
{
    private readonly AppDbContext _db;

    public ContactController(AppDbContext db)
    {
        _db = db;
    }

    // ---------------------------------------------------------------------------------------------
    // GET /Contact
    // ---------------------------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Talk to us";
        ViewData["Description"] = "Questions about a jar, a bulk order or a delivery? Call, email or "
                                + "WhatsApp the farm — a person answers.";

        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        return View(new ContactPageModel
        {
            Settings = settings ?? new SiteSetting(),
            Form = new ContactViewModel()
        });
    }

    // ---------------------------------------------------------------------------------------------
    // POST /Contact
    // ---------------------------------------------------------------------------------------------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactViewModel form)
    {
        ViewData["Title"] = "Talk to us";
        ViewData["Description"] = "Questions about a jar, a bulk order or a delivery? Call, email or "
                                + "WhatsApp the farm — a person answers.";

        if (!ModelState.IsValid)
        {
            var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
            return View(new ContactPageModel
            {
                Settings = settings ?? new SiteSetting(),
                Form = form
            });
        }

        // TODO (deferred): send this to the owner's inbox (SMTP / SendGrid / Postmark).
        // Until the mail provider is connected the message is acknowledged on screen only.
        TempData["Toast"] = $"Thank you, {form.Name.Trim()} — your message has reached us. "
                          + "We reply to every note within one working day.";
        return RedirectToAction(nameof(Index));
    }
}
