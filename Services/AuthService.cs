using System.Security.Cryptography;
using System.Text;
using Sabarna.Web.Data;
using Sabarna.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Sabarna.Web.Services;

/// <summary>
/// Everything about "proving you are the shop owner":
///   step 1 — check the password against the stored ASP.NET Core Identity hash,
///   step 2 — create and later check a 6-digit one-time code (valid for 5 minutes).
///
/// Only a SHA-256 hash of the code is stored. Six digits is a small space, which is why the
/// code expires quickly, allows five tries, and is single-use.
/// </summary>
public class AuthService
{
    public const int OtpLifetimeMinutes = 5;
    public const int MaxOtpAttempts = 5;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AdminUser> _hasher;

    public AuthService(AppDbContext db, IPasswordHasher<AdminUser> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1 — email + password
    // ---------------------------------------------------------------------------------------------

    /// <summary>Finds an active admin by email (case-insensitive). Returns null when there is none.</summary>
    public async Task<AdminUser> FindActiveAdminAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalised = email.Trim().ToLowerInvariant();
        return await _db.AdminUsers.FirstOrDefaultAsync(a => a.Email.ToLower() == normalised && a.IsActive);
    }

    /// <summary>True when the plain password matches the stored hash.</summary>
    public bool VerifyPassword(AdminUser admin, string password)
    {
        if (admin == null || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(admin, admin.PasswordHash, password);
        return result == PasswordVerificationResult.Success
               || result == PasswordVerificationResult.SuccessRehashNeeded;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2 — the one-time code
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates a fresh 6-digit code, invalidates any older unused codes and returns the code so the
    /// caller can show it (in development) or e-mail it (once the mail provider is wired up).
    /// </summary>
    public async Task<string> CreateOtpAsync(AdminUser admin)
    {
        // Codes are generated from a cryptographic RNG, not Random.
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        var previous = await _db.OtpChallenges
            .Where(o => o.AdminId == admin.Id && !o.Used)
            .ToListAsync();

        foreach (var old in previous)
        {
            old.Used = true;
        }

        _db.OtpChallenges.Add(new OtpChallenge
        {
            AdminId = admin.Id,
            CodeHash = HashCode(code),
            Attempts = 0,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpLifetimeMinutes),
            Used = false,
            CreatedAt = DateTime.UtcNow
        });

        // Tidy up anything that expired more than a day ago — keeps the table tiny.
        var stale = await _db.OtpChallenges
            .Where(o => o.ExpiresAt < DateTime.UtcNow.AddDays(-1))
            .ToListAsync();
        _db.OtpChallenges.RemoveRange(stale);

        await _db.SaveChangesAsync();
        return code;
    }

    /// <summary>
    /// Checks a code for the newest live challenge. Returns false for an expired code, a wrong
    /// code, a code that was already used, or once five wrong tries have been made.
    /// </summary>
    public async Task<bool> VerifyOtpAsync(int adminId, string code)
    {
        if (adminId <= 0 || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var challenge = await _db.OtpChallenges
            .Where(o => o.AdminId == adminId && !o.Used && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (challenge == null)
        {
            return false;
        }

        challenge.Attempts++;

        if (challenge.Attempts > MaxOtpAttempts)
        {
            challenge.Used = true;          // burn the code: too many guesses
            await _db.SaveChangesAsync();
            return false;
        }

        if (!string.Equals(challenge.CodeHash, HashCode(code.Trim()), StringComparison.OrdinalIgnoreCase))
        {
            await _db.SaveChangesAsync();
            return false;
        }

        challenge.Used = true;              // a code works exactly once
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>SHA-256 of the code, lowercase hex. Used for both create and verify.</summary>
    public static string HashCode(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
