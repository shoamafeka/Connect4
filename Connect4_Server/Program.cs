using Connect4_Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Razor Pages + Web API
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Register IHttpClientFactory
builder.Services.AddHttpClient();

// Session (required for HttpContext.Session.GetInt32/SetInt32)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Error handling + HTTPS/static
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session BEFORE authorization/endpoints
app.UseSession();

app.UseAuthorization();

// Map endpoints
app.MapControllers();
app.MapRazorPages();

app.Run();
