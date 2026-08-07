using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSchedulerValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGci..."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSchedulerAuthentication(builder.Configuration);
builder.Services.AddSchedulerDatabase(builder.Configuration);
builder.Services.AddSchedulerServices(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ensure DbFile/ directory and SQLite schema exist on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
    Directory.CreateDirectory(Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)!);
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
