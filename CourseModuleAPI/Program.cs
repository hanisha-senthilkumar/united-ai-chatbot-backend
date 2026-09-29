using CourseModuleAPI.Data;
using CourseModuleAPI.Interfaces;
using CourseModuleAPI.Repositories;
using CourseModuleAPI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var configuration = builder.Configuration;

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

// HttpClient for scraper
builder.Services.AddHttpClient("scraper", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "CourseModuleScraper/1.0");
});

// DI - repositories
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IModuleRepository, ModuleRepository>();

// DI - services
builder.Services.AddScoped<ICourseScraperService, CourseScraperService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IChatService, ChatService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations and seed/scrape if needed
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        // Check if courses exist
        var courseRepo = services.GetRequiredService<ICourseRepository>();
        var scraper = services.GetRequiredService<ICourseScraperService>();

        var any = courseRepo.GetAllAsync().GetAwaiter().GetResult();
        if (!any.Any())
        {
            var added = scraper.ScrapeAndSaveAsync().GetAwaiter().GetResult();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            logger.LogInformation("Initial scrape added {Count} courses.", added);
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogError(ex, "An error occurred during migration or initial scraping.");
    }
}

// Global exception middleware
app.UseMiddleware<CourseModuleAPI.Middleware.ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
