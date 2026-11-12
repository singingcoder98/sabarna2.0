using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Sabarna.Web.Services;   // saptarshi: EmailService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Controllers;

/// <summary>
/// The contact page: a form plus our address, phone, email and picking-up hours.
/// </summary>
public class ContactController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;   // saptarshi

    public ContactController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
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

        // saptarshi: changed 12-Nov-2026 — the message is e-mailed to the owner instead of being
        // acknowledged and thrown away. Storing it in the database is still on the list (task 3);
        // this is the half that stops a customer's question from disappearing today.
        var sent = await _email.SendContactMessageToOwnerAsync(
            form.Name.Trim(),
            form.Email.Trim(),
            (form.Phone ?? string.Empty).Trim(),
            form.Subject.Trim(),
            form.Message.Trim());

        if (sent)
        {
            TempData["Toast"] = $"Thank you, {form.Name.Trim()} — your message has reached us. "
                              + "We reply to every note within one working day.";
        }
        else
        {
            // No mail configured, or the server refused: say the truth and give a way that works.
            var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
            var phone = string.IsNullOrWhiteSpace(settings?.Phone) ? "our phone number" : settings!.Phone;

            TempData["Error"] = $"Sorry, {form.Name.Trim()} — our message box is not accepting mail right now. "
                              + $"Please call or WhatsApp us on {phone} and we will answer straight away.";
        }

        return RedirectToAction(nameof(Index));
    }
}
