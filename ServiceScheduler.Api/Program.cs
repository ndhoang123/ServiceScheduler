using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ServiceScheduler.Api.Data;
using ServiceScheduler.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<SchedulerDbContext>(opt =>
    opt.UseInMemoryDatabase("ServiceSchedulerDb")
       .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
builder.Services.AddScoped<ISchedulingService, SchedulingService>();

var app = builder.Build();

app.MapControllers();
app.Run();

public partial class Program { }
