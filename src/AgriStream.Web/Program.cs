using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => { options.LoginPath = "/Account/Login"; options.AccessDeniedPath = "/Account/Denied"; options.Cookie.Name = "AgriStream.Auth"; options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; options.SlidingExpiration = true; });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient("Functions", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<SqlAccountStore>();
builder.Services.AddSingleton<TelemetryTableService>();
builder.Services.AddSingleton<DroneBlobService>();
builder.Services.AddScoped<FunctionsClient>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
// The account table is created automatically when the SQL connection is configured.
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<SqlAccountStore>();
    await store.InitializeAsync();
}
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
