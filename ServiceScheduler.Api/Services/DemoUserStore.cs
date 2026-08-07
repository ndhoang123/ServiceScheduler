using Microsoft.AspNetCore.Identity;
using ServiceScheduler.Api.Services.Interface;

namespace ServiceScheduler.Api.Services;

// Seeded credentials — replace with a real identity provider in production
public sealed class DemoUserStore : IUserCredentialStore
{
    private sealed record DemoUser(string PasswordHash, string Role);

    private readonly IPasswordHasher<string> _hasher = new PasswordHasher<string>();
    private readonly Dictionary<string, DemoUser> _users;

    public DemoUserStore()
    {
        _users = new Dictionary<string, DemoUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["advisor"]  = new(_hasher.HashPassword("advisor",  "Advisor123!"),  "ServiceAdvisor"),
            ["admin"]    = new(_hasher.HashPassword("admin",    "Admin123!"),    "Admin"),
            ["customer"] = new(_hasher.HashPassword("customer", "Customer123!"), "Customer"),
        };
    }

    public bool TryValidate(string username, string password, out string role)
    {
        role = string.Empty;
        if (!_users.TryGetValue(username, out var user)) return false;
        var result = _hasher.VerifyHashedPassword(username, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return false;
        role = user.Role;
        return true;
    }
}
