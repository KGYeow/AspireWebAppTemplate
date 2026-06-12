using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Authentication;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Options;
using AspireWebAppTemplate.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Identity (core services only — UserManager, SignInManager, stores).
// Using AddIdentityCore instead of AddIdentity to avoid registering Identity's cookie auth
// scheme which conflicts with our internal service-to-service authentication.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Lockout.AllowedForNewUsers = false;
        options.Lockout.MaxFailedAccessAttempts = int.MaxValue;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Authentication: trust internal service-to-service headers from the Web frontend.
builder.Services.AddAuthentication(InternalAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, InternalAuthenticationHandler>(
        InternalAuthenticationHandler.SchemeName, options => { });
builder.Services.AddAuthorization();

// Memory cache (used by LoginService for single-use login tokens)
builder.Services.AddMemoryCache();

// Controllers
builder.Services.AddControllers();

// Application services
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRegisterService, RegisterService>();
builder.Services.AddScoped<ILdapAuthService, LdapAuthService>();
builder.Services.AddScoped<ILdapLoginService, LdapLoginService>();

// [LDAP] LDAP configuration — remove this block if LDAP is not needed
builder.Services.Configure<LdapSettings>(builder.Configuration.GetSection("LDAP"));

// Email sender (no-op — replace with real implementation when needed)
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser>, NoOpEmailSender>();

// EPPlus license configuration
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("AspireWebAppTemplate");

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Auto-migrate and seed on startup in development
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await SeedData.InitializeAsync(scope.ServiceProvider);
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "API service is running.");

app.MapDefaultEndpoints();

app.Run();
