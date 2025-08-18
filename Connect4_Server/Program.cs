using Connect4_Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --------------------- Services registration ---------------------

// EF Core DbContext (SQL Server). Connection string: appsettings.json -> "DefaultConnection".
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Razor Pages (site) + Controllers (Web API endpoints).
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// HttpClient factory (used by NewGame page to call the API).
builder.Services.AddHttpClient();

// Session state:
// - Backed by in-memory cache (sufficient for this project).
// - Cookie is HttpOnly + Essential (required for session).
// - 30 minutes idle timeout.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// --------------------- HTTP request pipeline ---------------------

if (!app.Environment.IsDevelopment())
{
    // Generic error page in Production.
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: Enable session BEFORE hitting endpoints that read/write session.
app.UseSession();

// No authentication in this project; authorization remains a no-op.
app.UseAuthorization();

// --------------------- Endpoints ---------------------

// Web API routes (e.g., /api/GameApi/*)
app.MapControllers();

// Razor Pages (site UI)
app.MapRazorPages();

app.Run();
