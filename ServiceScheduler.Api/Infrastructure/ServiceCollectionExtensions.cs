using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Options;
using ServiceScheduler.Api.Services;
using ServiceScheduler.Api.Services.Interface;
using ServiceScheduler.Api.Validators;
using System.Text;

namespace ServiceScheduler.Api.Infrastructure;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSchedulerDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<SchedulerDbContext>(opt =>
            opt.UseSqlite(config.GetConnectionString("DefaultConnection")));
        return services;
    }

    internal static IServiceCollection AddSchedulerAuthentication(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var key = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });
        services.AddAuthorization();
        return services;
    }

    internal static IServiceCollection AddSchedulerServices(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SchedulingOptions>(config.GetSection(SchedulingOptions.Section));
        services.AddScoped<ISchedulingService, SchedulingService>();
        // swap DemoUserStore for a real identity provider without touching any other code
        services.AddSingleton<IUserCredentialStore, DemoUserStore>();
        return services;
    }

    internal static IServiceCollection AddSchedulerValidation(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<BookAppointmentRequestValidator>();
        return services;
    }
}
