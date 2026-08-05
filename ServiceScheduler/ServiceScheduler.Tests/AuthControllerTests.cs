using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using ServiceScheduler.Api.Controllers;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Models;

namespace ServiceScheduler.Tests;

public class AuthControllerTests
{
    private static SchedulerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SchedulerDbContext(options);
    }

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]           = "TestSuperSecretKey-Min32Characters!!",
                ["Jwt:Issuer"]        = "TestIssuer",
                ["Jwt:Audience"]      = "TestAudience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

    [Fact]
    public async Task Register_NewUser_ReturnsOk()
    {
        var controller = new AuthController(CreateDb(), CreateConfig());

        var result = await controller.Register(new RegisterRequest("alice", "Password1!", "Advisor"));

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        var db = CreateDb();
        var controller = new AuthController(db, CreateConfig());
        await controller.Register(new RegisterRequest("alice", "Password1!", "Advisor"));

        var result = await controller.Register(new RegisterRequest("alice", "Other1!", "Advisor"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtToken()
    {
        var db = CreateDb();
        var controller = new AuthController(db, CreateConfig());
        await controller.Register(new RegisterRequest("bob", "Password1!", "Advisor"));

        var result = await controller.Login(new LoginRequest("bob", "Password1!"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var token = ok.Value?.GetType().GetProperty("token")?.GetValue(ok.Value) as string;
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var db = CreateDb();
        var controller = new AuthController(db, CreateConfig());
        await controller.Register(new RegisterRequest("carol", "Password1!", "Advisor"));

        var result = await controller.Login(new LoginRequest("carol", "WrongPass!"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsUnauthorized()
    {
        var controller = new AuthController(CreateDb(), CreateConfig());

        var result = await controller.Login(new LoginRequest("ghost", "Password1!"));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
