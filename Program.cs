using Microsoft.EntityFrameworkCore;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks(); // Add health checks

// Add PostgreSQL - use Railway's DATABASE_URL if available
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ??
                      builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<TodoContext>(options =>
    options.UseNpgsql(connectionString));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// Add health check endpoints
app.MapGet("/", () => "TodoApi is running!");
app.MapHealthChecks("/health");

app.MapControllers();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TodoContext>();
    try
    {
        context.Database.EnsureCreated();
        Console.WriteLine("Database created successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database creation failed: {ex.Message}");
    }
}

app.Run();