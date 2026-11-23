using SchoolManagementSystem.Components;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Modules.Enrollment.Application;

var builder = WebApplication.CreateBuilder(args);


var connectionString =
    builder.Configuration.GetConnectionString("SchoolDatabase")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:SchoolDatabase is missing.");

builder.Services.AddDbContextFactory<SchoolDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register application services.
// Scoped = one instance per Blazor circuit (user session).
// LearnerService is stateless and creates its own DbContext per operation
// via the factory, so scoped lifetime is safe — no shared mutable state.
builder.Services.AddScoped<SchoolManagementSystem.Modules.Academics.Application.SchoolInfoService>();
builder.Services.AddScoped<ILearnerService, LearnerService>();
builder.Services.AddScoped<ISectionService, SectionService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();