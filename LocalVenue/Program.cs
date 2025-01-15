using System.Net.Http.Headers;
using AutoMapper;
using LocalVenue;
using LocalVenue.Core;
using LocalVenue.Core.Entities;
using LocalVenue.Core.Services;
using LocalVenue.Services;
using LocalVenue.Services.Interfaces;
using LocalVenue.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Shared.WebComponents;

var builder = WebApplication.CreateBuilder(args);

// Let builder read appsettings.json and environment variables (azure webapp settings)
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        "appsettings.Development.json",
        optional: false,
        reloadOnChange: true
    );
}
else
{
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System
            .Text
            .Json
            .Serialization
            .ReferenceHandler
            .Preserve;
    });
builder.Services.AddAutoMapper(typeof(Program)); //AutoMapper configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LocalVenue API", Version = "v1" });
    c.EnableAnnotations();
});

// Database context setup starts
string connectionString =
    builder.Configuration.GetConnectionString("VenueContext")
    ?? throw new ArgumentNullException("VenueContext");

builder.Services.AddDbContextFactory<VenueContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.Parse("8.0-mysql"));
});

builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IShowService, ShowService>();
builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

// Database context setup ends

// TMDB HTTP requests setup start
builder.Services.AddScoped<IActorService, ActorService>();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

builder
    .Services.AddHttpClient(
        "TmdbClient",
        client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration.GetSection("TMDB").GetSection("Url").Value
                    ?? throw new ArgumentNullException("TMDB.Url")
            );
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                builder.Configuration.GetSection("TMDB").GetSection("Token").Value
                    ?? throw new ArgumentNullException("TMDB.Token")
            );
        }
    )
    .AddPolicyHandler(GetRetryPolicy());

// TMDB HTTP requests setup ends

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(
        CookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/404";
            options.Cookie.Name = "CookieAuth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.MaxAge = TimeSpan.FromDays(1);
            options.Cookie.IsEssential = true;
        }
    );

builder
    .Services.AddIdentity<Customer, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;

        options.Password.RequiredLength = 3;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredUniqueChars = 0;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<VenueContext>()
    .AddApiEndpoints();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

//test push in GitHub with organization
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VenueContext>();
    var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
    var retryCount = 5;
    var delay = TimeSpan.FromSeconds(10);

    for (int i = 0; i < retryCount; i++)
    {
        try
        {
            dbContext.Database.Migrate(); //Auto perform migrations
            DbSeeder.UpsertSeed(dbContext, mapper); //Auto seed database
            break;
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            if (i == retryCount - 1)
                throw;
            Thread.Sleep(delay);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LocalVenue API V1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "LocalVenue API";
});

app.UseAntiforgery();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<BlazorCookieLoginMiddleware>();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
