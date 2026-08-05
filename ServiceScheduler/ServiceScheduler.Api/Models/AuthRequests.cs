namespace ServiceScheduler.Api.Models;

public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Password, string Role = "Advisor");
